using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewModel.Member;
using APIViewModel.MembershipInvoice;
using APIViewModel.MemberSubscription;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Services.EmailService;
using Services.PasswordHashService;

namespace Services.MemberSubscriptionService;

public class MemberSubscriptionService : IMemberSubscriptionService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IEmailService _emailService;

    public MemberSubscriptionService(
        SportsCenterManagementContext context,
        IConfiguration configuration,
        IPasswordHashService passwordHashService,
        IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _passwordHashService = passwordHashService;
        _emailService = emailService;
    }

    public async Task<(RegisterSubscriptionResult Result, MemberSubscriptionDetailAPIViewModel? Data)> RegisterOrRenewAsync(
        string accountId,
        RegisterMemberSubscriptionAPIViewModel request)
    {
        if (!request.PackageId.HasValue || request.PackageId.Value <= 0)
        {
            return (RegisterSubscriptionResult.InvalidPackageId, null);
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return (RegisterSubscriptionResult.InvalidPaymentMethod, null);
        }

        string normalizedPaymentMethod = request.PaymentMethod.Trim().ToUpperInvariant();
        if (normalizedPaymentMethod != "CASH" &&
            normalizedPaymentMethod != "BANK_TRANSFER" &&
            normalizedPaymentMethod != "CARD")
        {
            return (RegisterSubscriptionResult.InvalidPaymentMethod, null);
        }

        string timeZoneId = _configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured.");
        TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone));

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            Member? member = await _context.Members
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Member] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {0}",
                    accountId)
                .Include(m => m.Account)
                .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync();

            if (member is null || member.Account.Role.Name != "Member")
            {
                return (RegisterSubscriptionResult.MemberNotFound, null);
            }

            if (!string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (RegisterSubscriptionResult.MemberInactive, null);
            }

            if (member.Account.IsLocked)
            {
                return (RegisterSubscriptionResult.MemberLocked, null);
            }

            int packageId = request.PackageId.Value;
            MembershipPackage? package = await _context.MembershipPackages
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package is null)
            {
                return (RegisterSubscriptionResult.PackageNotFound, null);
            }

            if (!package.IsActive)
            {
                return (RegisterSubscriptionResult.PackageInactive, null);
            }

            bool hasPending = await _context.MemberSubscriptions
                .AnyAsync(s => s.MemberId == accountId && s.Status == "PENDING_PAYMENT");

            if (hasPending)
            {
                return (RegisterSubscriptionResult.PendingOrderExists, null);
            }

            MemberSubscription? latestConfirmed = await _context.MemberSubscriptions
                .Where(s => s.MemberId == accountId && s.Status == "CONFIRMED")
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            string kind;
            DateOnly startDate;

            if (latestConfirmed is null)
            {
                kind = "REGISTER";
                startDate = today;
            }
            else
            {
                kind = "RENEW";
                startDate = latestConfirmed.EndDate >= today ? latestConfirmed.EndDate.AddDays(1) : today;
            }

            DateOnly endDate = startDate.AddMonths(package.DurationMonths).AddDays(-1);

            DateTime nowUtc = DateTime.UtcNow;
            string invoiceNumber = "INV-" + nowUtc.ToString("yyyyMMdd") + "-" +
                Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

            MemberSubscription subscription = new MemberSubscription
            {
                MemberId = accountId,
                PackageId = package.Id,
                PackageName = package.Name,
                PackagePrice = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = package.Benefits,
                StartDate = startDate,
                EndDate = endDate,
                Kind = kind,
                Status = "PENDING_PAYMENT",
                CreatedAt = nowUtc
            };

            MembershipInvoice invoice = new MembershipInvoice
            {
                InvoiceNumber = invoiceNumber,
                MemberId = accountId,
                Amount = package.Price,
                Status = "PENDING_PAYMENT",
                PaymentMethod = normalizedPaymentMethod,
                CreatedBy = accountId,
                CreatedAt = nowUtc,
                Subscription = subscription
            };

            _context.MembershipInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            List<string> benefits = JsonSerializer.Deserialize<List<string>>(package.Benefits)
                ?? new List<string>();

            MemberSubscriptionDetailAPIViewModel data = new MemberSubscriptionDetailAPIViewModel
            {
                InvoiceId = invoice.Id,
                SubscriptionId = subscription.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                PackageId = package.Id,
                PackageName = package.Name,
                PackagePrice = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = benefits,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Kind = subscription.Kind,
                Status = subscription.Status,
                Amount = invoice.Amount,
                PaymentMethod = invoice.PaymentMethod,
                CreatedAt = subscription.CreatedAt
            };

            await transaction.CommitAsync();

            return (RegisterSubscriptionResult.Success, data);
        }
        catch (Exception ex)
        {
            if (IsDeadlockVictim(ex))
            {
                return (RegisterSubscriptionResult.ConcurrencyConflict, null);
            }

            await transaction.RollbackAsync();

            if (IsSpecificUniqueConstraintViolation(ex, "UQ_MemberSubscription_Pending_Member"))
            {
                return (RegisterSubscriptionResult.PendingOrderExists, null);
            }

            throw;
        }
    }

    public async Task<(CounterRegisterMemberResult Result, CounterRegisterMemberResponseAPIViewModel? Data)>
        RegisterMemberAtCounterAsync(
            string staffAccountId,
            CounterRegisterMemberAPIViewModel request)
    {
        (string? fullName, string? email, string? phone, string? paymentMethod) =
            NormalizeCounterRegistration(request);

        if (paymentMethod is null)
        {
            return (CounterRegisterMemberResult.InvalidPaymentMethod, null);
        }

        if (fullName is null || email is null || phone is null)
        {
            return (CounterRegisterMemberResult.InvalidData, null);
        }

        if (!request.PackageId.HasValue || request.PackageId.Value <= 0 ||
            !request.ExpectedPrice.HasValue || request.ExpectedPrice.Value <= 0m)
        {
            return (CounterRegisterMemberResult.InvalidData, null);
        }

        string timeZoneId = _configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured.");
        TimeZoneInfo businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, businessTimeZone));
        CounterRegisterMemberResponseAPIViewModel? response = null;

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            Account? staff = await _context.Accounts
                .Include(account => account.Role)
                .FirstOrDefaultAsync(account => account.Id == staffAccountId);
            if (staff is null)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.StaffNotFound, null);
            }

            if (staff.Role.Name != "CenterManager" && staff.Role.Name != "Receptionist")
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.StaffRoleNotAllowed, null);
            }

            if (!string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                staff.DeletedAt is not null)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.StaffInactive, null);
            }

            if (staff.IsLocked)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.StaffLocked, null);
            }

            Role? memberRole = await _context.Roles
                .FirstOrDefaultAsync(role => role.Name == "Member");
            if (memberRole is null)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.MemberRoleMissing, null);
            }

            MembershipPackage? package = await _context.MembershipPackages
                .FirstOrDefaultAsync(candidate => candidate.Id == request.PackageId.Value);
            if (package is null)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.PackageNotFound, null);
            }

            if (!package.IsActive)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.PackageInactive, null);
            }

            if (package.Price != request.ExpectedPrice.Value)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.PriceChanged, null);
            }

            bool duplicateEmail = await _context.Accounts
                .AnyAsync(account => account.Email.ToLower() == email);
            if (duplicateEmail)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.DuplicateEmail, null);
            }

            bool duplicatePhone = await _context.Accounts
                .AnyAsync(account => account.Phone == phone);
            if (duplicatePhone)
            {
                await transaction.RollbackAsync();
                return (CounterRegisterMemberResult.DuplicatePhone, null);
            }

            DateTime nowUtc = DateTime.UtcNow;
            string initialPassword = GenerateInitialPassword();
            string memberCode = await GenerateUniqueMemberCodeAsync();
            string invoiceNumber = await GenerateUniqueInvoiceNumberAsync(nowUtc);
            Account account = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                Phone = phone,
                PasswordHash = _passwordHashService.HashPassword(initialPassword),
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false,
                CreatedAt = nowUtc,
                RoleId = memberRole.Id,
                Role = memberRole
            };
            Member member = new Member
            {
                AccountId = account.Id,
                MemberCode = memberCode,
                FullName = fullName,
                DateOfBirth = request.DateOfBirth,
                CreatedAt = nowUtc,
                Account = account
            };
            account.Member = member;

            DateOnly endDate = today.AddMonths(package.DurationMonths).AddDays(-1);
            MemberSubscription subscription = new MemberSubscription
            {
                MemberId = account.Id,
                PackageId = package.Id,
                PackageName = package.Name,
                PackagePrice = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = package.Benefits,
                StartDate = today,
                EndDate = endDate,
                Kind = "REGISTER",
                Status = "PENDING_PAYMENT",
                IsSuspended = false,
                CreatedAt = nowUtc,
                Member = member,
                Package = package
            };
            MembershipInvoice invoice = new MembershipInvoice
            {
                InvoiceNumber = invoiceNumber,
                MemberId = account.Id,
                Amount = package.Price,
                Status = "PENDING_PAYMENT",
                PaymentMethod = paymentMethod,
                CreatedBy = staff.Id,
                CreatedAt = nowUtc,
                Member = member,
                Subscription = subscription,
                CreatedByNavigation = staff
            };

            _context.Accounts.Add(account);
            _context.MembershipInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            List<string> benefits = JsonSerializer.Deserialize<List<string>>(package.Benefits)
                ?? new List<string>();
            response = new CounterRegisterMemberResponseAPIViewModel
            {
                Member = MapMemberDetail(member),
                Receipt = new MembershipReceiptAPIViewModel
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Amount = invoice.Amount,
                    PaymentMethod = invoice.PaymentMethod,
                    InvoiceStatus = invoice.Status,
                    CreatedAt = invoice.CreatedAt,
                    PaidAt = null,
                    PaidByStaffId = null,
                    PaidByStaffName = null,
                    SubscriptionId = subscription.Id,
                    SubscriptionStatus = subscription.Status,
                    Kind = subscription.Kind,
                    StartDate = subscription.StartDate,
                    EndDate = subscription.EndDate,
                    PackageId = package.Id,
                    PackageName = package.Name,
                    PackagePrice = package.Price,
                    DurationMonths = package.DurationMonths,
                    Benefits = benefits,
                    MemberAccountId = member.AccountId,
                    MemberCode = member.MemberCode,
                    MemberFullName = member.FullName,
                    MemberEmail = account.Email,
                    MemberPhone = account.Phone
                },
                InitialPassword = initialPassword,
                EmailDelivery = "NOT_CONFIGURED"
            };

            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();

            if (IsDeadlockVictim(exception) || exception is DbUpdateConcurrencyException)
            {
                return (CounterRegisterMemberResult.ConcurrencyConflict, null);
            }

            if (IsSpecificUniqueConstraintViolation(exception, "UQ_MembershipInvoice_InvoiceNumber"))
            {
                return (CounterRegisterMemberResult.InvoiceNumberCollision, null);
            }

            if (IsSpecificUniqueConstraintViolation(exception, "UQ_MemberSubscription_Pending_Member"))
            {
                return (CounterRegisterMemberResult.PendingOrderExists, null);
            }

            throw;
        }

        if (response is null)
        {
            throw new InvalidOperationException("Counter registration completed without a response.");
        }

        if (_emailService.IsConfigured)
        {
            try
            {
                await _emailService.SendMemberWelcomeAsync(
                    response.Member.Email,
                    response.Member.FullName ?? string.Empty,
                    response.InitialPassword,
                    response.Receipt.PackageName,
                    response.Receipt.Amount);
                response.EmailDelivery = "SENT";
            }
            catch (Exception)
            {
                response.EmailDelivery = "FAILED";
            }
        }

        return (CounterRegisterMemberResult.Success, response);
    }

    public async Task<(CounterRegisterResult Result, MembershipReceiptAPIViewModel? Receipt, PendingOrderConflictResponseAPIViewModel? PendingInfo)> CounterRegisterOrRenewAsync(
        string staffAccountId,
        CounterRegisterSubscriptionAPIViewModel request)
    {
        if (string.IsNullOrWhiteSpace(request.MemberAccountId))
        {
            return (CounterRegisterResult.InvalidMemberAccountId, null, null);
        }

        if (!request.PackageId.HasValue || request.PackageId.Value <= 0)
        {
            return (CounterRegisterResult.InvalidPackageId, null, null);
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return (CounterRegisterResult.InvalidPaymentMethod, null, null);
        }

        string normalizedPaymentMethod = request.PaymentMethod.Trim().ToUpperInvariant();
        if (normalizedPaymentMethod != "CASH" &&
            normalizedPaymentMethod != "BANK_TRANSFER" &&
            normalizedPaymentMethod != "CARD")
        {
            return (CounterRegisterResult.InvalidPaymentMethod, null, null);
        }

        string timeZoneId = _configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured.");
        TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone));

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            Account? staff = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Receptionist)
                .Include(a => a.CenterManager)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == staffAccountId);

            if (staff is null)
            {
                return (CounterRegisterResult.StaffNotFound, null, null);
            }

            if (staff.Role.Name != "Receptionist" && staff.Role.Name != "CenterManager")
            {
                return (CounterRegisterResult.StaffRoleNotAllowed, null, null);
            }

            if (!string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (CounterRegisterResult.StaffInactive, null, null);
            }

            if (staff.IsLocked)
            {
                return (CounterRegisterResult.StaffLocked, null, null);
            }

            string? staffFullName = staff.Receptionist?.FullName ?? staff.CenterManager?.FullName;

            Member? member = await _context.Members
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Member] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {0}",
                    request.MemberAccountId)
                .Include(m => m.Account)
                .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync();

            if (member is null || member.Account.Role.Name != "Member")
            {
                return (CounterRegisterResult.MemberNotFound, null, null);
            }

            if (!string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (CounterRegisterResult.MemberInactive, null, null);
            }

            if (member.Account.IsLocked)
            {
                return (CounterRegisterResult.MemberLocked, null, null);
            }

            int packageId = request.PackageId.Value;
            MembershipPackage? package = await _context.MembershipPackages
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package is null)
            {
                return (CounterRegisterResult.PackageNotFound, null, null);
            }

            if (!package.IsActive)
            {
                return (CounterRegisterResult.PackageInactive, null, null);
            }

            MemberSubscription? pendingSubscription = await _context.MemberSubscriptions
                .Include(s => s.MembershipInvoice)
                .FirstOrDefaultAsync(s => s.MemberId == request.MemberAccountId && s.Status == "PENDING_PAYMENT");

            if (pendingSubscription is not null)
            {
                if (pendingSubscription.MembershipInvoice is null)
                {
                    return (CounterRegisterResult.DataIntegrityViolation, null, null);
                }

                PendingOrderConflictResponseAPIViewModel pendingInfo = new PendingOrderConflictResponseAPIViewModel
                {
                    PendingSubscriptionId = pendingSubscription.Id,
                    PendingInvoiceId = pendingSubscription.MembershipInvoice.Id,
                    InvoiceNumber = pendingSubscription.MembershipInvoice.InvoiceNumber,
                    PackageName = pendingSubscription.PackageName,
                    Amount = pendingSubscription.PackagePrice,
                    CreatedAt = pendingSubscription.CreatedAt
                };

                return (CounterRegisterResult.PendingOrderExists, null, pendingInfo);
            }

            MemberSubscription? latestConfirmed = await _context.MemberSubscriptions
                .Where(s => s.MemberId == request.MemberAccountId && s.Status == "CONFIRMED")
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            string kind;
            DateOnly startDate;

            if (latestConfirmed is null)
            {
                kind = "REGISTER";
                startDate = today;
            }
            else
            {
                kind = "RENEW";
                startDate = latestConfirmed.EndDate >= today ? latestConfirmed.EndDate.AddDays(1) : today;
            }

            DateOnly endDate = startDate.AddMonths(package.DurationMonths).AddDays(-1);

            DateTime nowUtc = DateTime.UtcNow;
            string invoiceNumber = "INV-" + nowUtc.ToString("yyyyMMdd") + "-" +
                Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

            MemberSubscription subscription = new MemberSubscription
            {
                MemberId = request.MemberAccountId,
                PackageId = package.Id,
                PackageName = package.Name,
                PackagePrice = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = package.Benefits,
                StartDate = startDate,
                EndDate = endDate,
                Kind = kind,
                Status = "CONFIRMED",
                CreatedAt = nowUtc
            };

            MembershipInvoice invoice = new MembershipInvoice
            {
                InvoiceNumber = invoiceNumber,
                MemberId = request.MemberAccountId,
                Amount = package.Price,
                Status = "PAID",
                PaymentMethod = normalizedPaymentMethod,
                CreatedBy = staffAccountId,
                PaidBy = staffAccountId,
                CreatedAt = nowUtc,
                PaidAt = nowUtc,
                Subscription = subscription
            };

            _context.MembershipInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            List<string> benefits = JsonSerializer.Deserialize<List<string>>(package.Benefits)
                ?? new List<string>();

            MembershipReceiptAPIViewModel receipt = new MembershipReceiptAPIViewModel
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = invoice.Amount,
                PaymentMethod = invoice.PaymentMethod,
                InvoiceStatus = invoice.Status,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                PaidByStaffId = invoice.PaidBy,
                PaidByStaffName = staffFullName,
                SubscriptionId = subscription.Id,
                SubscriptionStatus = subscription.Status,
                Kind = subscription.Kind,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                PackageId = package.Id,
                PackageName = package.Name,
                PackagePrice = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = benefits,
                MemberAccountId = member.AccountId,
                MemberCode = member.MemberCode,
                MemberFullName = member.FullName,
                MemberEmail = member.Account.Email,
                MemberPhone = member.Account.Phone
            };

            await transaction.CommitAsync();

            return (CounterRegisterResult.Success, receipt, null);
        }
        catch (Exception ex)
        {
            if (IsDeadlockVictim(ex))
            {
                return (CounterRegisterResult.ConcurrencyConflict, null, null);
            }

            await transaction.RollbackAsync();

            if (IsSpecificUniqueConstraintViolation(ex, "UQ_MemberSubscription_Pending_Member"))
            {
                MemberSubscription? pendingSubscription = await _context.MemberSubscriptions
                    .AsNoTracking()
                    .Include(s => s.MembershipInvoice)
                    .FirstOrDefaultAsync(s => s.MemberId == request.MemberAccountId && s.Status == "PENDING_PAYMENT");

                PendingOrderConflictResponseAPIViewModel? pendingInfo = null;
                if (pendingSubscription is not null && pendingSubscription.MembershipInvoice is not null)
                {
                    pendingInfo = new PendingOrderConflictResponseAPIViewModel
                    {
                        PendingSubscriptionId = pendingSubscription.Id,
                        PendingInvoiceId = pendingSubscription.MembershipInvoice.Id,
                        InvoiceNumber = pendingSubscription.MembershipInvoice.InvoiceNumber,
                        PackageName = pendingSubscription.PackageName,
                        Amount = pendingSubscription.PackagePrice,
                        CreatedAt = pendingSubscription.CreatedAt
                    };
                }

                return (CounterRegisterResult.PendingOrderExists, null, pendingInfo);
            }

            if (IsSpecificUniqueConstraintViolation(ex, "UQ_MembershipInvoice_InvoiceNumber"))
            {
                return (CounterRegisterResult.InvoiceNumberCollision, null, null);
            }

            throw;
        }
    }

    private static (
        string? FullName,
        string? Email,
        string? Phone,
        string? PaymentMethod) NormalizeCounterRegistration(
            CounterRegisterMemberAPIViewModel request)
    {
        string normalizedFullName = request.FullName?.Trim() ?? string.Empty;
        string normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedPhone = request.Phone?.Trim() ?? string.Empty;
        string normalizedPaymentMethod = request.PaymentMethod?.Trim().ToUpperInvariant()
            ?? string.Empty;
        bool validPhone = normalizedPhone.Length == 10 &&
            normalizedPhone[0] == '0' &&
            normalizedPhone.All(character => character >= '0' && character <= '9');
        bool validPaymentMethod = normalizedPaymentMethod == "CASH" ||
            normalizedPaymentMethod == "BANK_TRANSFER" ||
            normalizedPaymentMethod == "CARD";

        if (!validPaymentMethod)
        {
            return (null, null, null, null);
        }

        if (normalizedFullName.Length < 2 ||
            normalizedFullName.Length > 100 ||
            normalizedEmail.Length == 0 ||
            normalizedEmail.Length > 150 ||
            !new EmailAddressAttribute().IsValid(normalizedEmail) ||
            !validPhone ||
            !request.DateOfBirth.HasValue ||
            request.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return (null, null, null, normalizedPaymentMethod);
        }

        return (
            normalizedFullName,
            normalizedEmail,
            normalizedPhone,
            normalizedPaymentMethod);
    }

    private async Task<string> GenerateUniqueMemberCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = $"MB{DateTime.UtcNow:yyMMdd}{RandomNumberGenerator.GetInt32(1000, 10000)}";
            if (!await _context.Members.AnyAsync(member => member.MemberCode == candidate))
            {
                return candidate;
            }
        }

        return "MB" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }

    private async Task<string> GenerateUniqueInvoiceNumberAsync(DateTime nowUtc)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = "INV-" + nowUtc.ToString("yyyyMMdd") + "-" +
                Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            if (!await _context.MembershipInvoices
                    .AnyAsync(invoice => invoice.InvoiceNumber == candidate))
            {
                return candidate;
            }
        }

        return "INV-" + nowUtc.ToString("yyyyMMdd") + "-" +
            Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }

    private static string GenerateInitialPassword()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(12);
        return "Tt9!" + Convert.ToHexString(randomBytes);
    }

    private static MemberDetailAPIViewModel MapMemberDetail(Member member)
    {
        return new MemberDetailAPIViewModel
        {
            AccountId = member.AccountId,
            MemberCode = member.MemberCode,
            FullName = member.FullName,
            DateOfBirth = member.DateOfBirth,
            AvatarUrl = member.AvatarUrl,
            Email = member.Account.Email,
            Phone = member.Account.Phone,
            Status = member.Account.Status,
            CreatedAt = member.CreatedAt,
            UpdatedAt = member.Account.UpdatedAt
        };
    }

    private static bool IsDeadlockVictim(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is SqlException sqlEx && sqlEx.Number == 1205)
            {
                return true;
            }
            current = current.InnerException;
        }

        return false;
    }

    private static bool IsSpecificUniqueConstraintViolation(Exception ex, string constraintOrIndexName)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                if (sqlEx.Message.Contains(constraintOrIndexName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            current = current.InnerException;
        }

        return false;
    }
}
