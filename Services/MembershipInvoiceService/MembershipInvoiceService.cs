using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace Services.MembershipInvoiceService;

public partial class MembershipInvoiceService : IMembershipInvoiceService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment? _environment;
    private readonly IMemoryCache? _paymentCache;

    public MembershipInvoiceService(
        SportsCenterManagementContext context,
        IConfiguration configuration,
        IHostEnvironment? environment = null,
        IMemoryCache? paymentCache = null)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
        _paymentCache = paymentCache;
    }

    public async Task<(PayInvoiceResult Result, MembershipReceiptAPIViewModel? Receipt)> PayInvoiceAsync(
        int invoiceId,
        string staffAccountId,
        PayMembershipInvoiceAPIViewModel request)
    {
        return await ConfirmPaymentAsync(invoiceId, staffAccountId, request, "CASH");
    }

    public Task<(PayInvoiceResult Result, MembershipReceiptAPIViewModel? Receipt)> ConfirmBankTransferAsync(
        int invoiceId, string staffAccountId, ConfirmBankTransferAPIViewModel request)
    {
        return ConfirmPaymentAsync(invoiceId, staffAccountId, new PayMembershipInvoiceAPIViewModel
        {
            PaymentMethod = "BANK_TRANSFER", Amount = request.Amount, ConfirmReceivedCash = request.ConfirmReceivedTransfer
        }, "BANK_TRANSFER");
    }

    private async Task<(PayInvoiceResult Result, MembershipReceiptAPIViewModel? Receipt)> ConfirmPaymentAsync(
        int invoiceId, string staffAccountId, PayMembershipInvoiceAPIViewModel request, string expectedMethod, string? providerEventId = null, bool isDemo = false)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return (PayInvoiceResult.InvalidPaymentMethod, null);
        }

        string normalizedPaymentMethod = request.PaymentMethod.Trim().ToUpperInvariant();
        if (normalizedPaymentMethod != expectedMethod)
        {
            return (PayInvoiceResult.InvalidPaymentMethod, null);
        }

        if (!request.Amount.HasValue || request.Amount.Value <= 0m ||
            decimal.Truncate(request.Amount.Value) != request.Amount.Value || !request.ConfirmReceivedCash)
        {
            return (PayInvoiceResult.InvalidConfirmation, null);
        }

        string timeZoneId = _configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured.");
        TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone));

        string? memberAccountId = await _context.MembershipInvoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.MemberId)
            .FirstOrDefaultAsync();

        if (memberAccountId is null)
        {
            return (PayInvoiceResult.InvoiceNotFound, null);
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            Account? staff = await _context.Accounts
                .Include(account => account.Role)
                .Include(account => account.Receptionist)
                .Include(account => account.CenterManager)
                .AsNoTracking()
                .FirstOrDefaultAsync(account => account.Id == staffAccountId);

            if (staff is null)
            {
                return (PayInvoiceResult.StaffNotFound, null);
            }

            if (staff.Role is null || (staff.Role.Name != "Receptionist" && staff.Role.Name != "CenterManager"))
            {
                return (PayInvoiceResult.StaffRoleNotAllowed, null);
            }

            if (staff.DeletedAt is not null || !string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (PayInvoiceResult.StaffInactive, null);
            }

            if (staff.IsLocked)
            {
                return (PayInvoiceResult.StaffLocked, null);
            }

            string? staffFullName = staff.Receptionist?.FullName ?? staff.CenterManager?.FullName;

            IQueryable<Member> memberQuery = _context.Database.IsSqlServer()
                ? _context.Members.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Member] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {memberAccountId}")
                : _context.Members.Where(item => item.AccountId == memberAccountId);
            Member? member = await memberQuery
                .Include(m => m.Account)
                .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync();

            if (member is null || member.Account.Role.Name != "Member")
            {
                return (PayInvoiceResult.MemberNotFound, null);
            }

            if (member.Account.DeletedAt is not null ||
                !string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (PayInvoiceResult.MemberInactive, null);
            }

            if (member.Account.IsLocked)
            {
                return (PayInvoiceResult.MemberLocked, null);
            }

            // Refresh tracked state after the member lock, including requests sharing a scoped context.
            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<MembershipInvoice> entry in
                _context.ChangeTracker.Entries<MembershipInvoice>().Where(item => item.Entity.MemberId == memberAccountId).ToList())
            {
                await entry.ReloadAsync();
            }
            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<MemberSubscription> entry in
                _context.ChangeTracker.Entries<MemberSubscription>().Where(item => item.Entity.MemberId == memberAccountId).ToList())
            {
                await entry.ReloadAsync();
            }

            MembershipInvoice? invoice = await _context.MembershipInvoices
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice is null)
            {
                return (PayInvoiceResult.InvoiceNotFound, null);
            }

            if (invoice.MemberId != memberAccountId)
            {
                return (PayInvoiceResult.ConcurrencyConflict, null);
            }

            if (invoice.Subscription is null)
            {
                return (PayInvoiceResult.DataIntegrityViolation, null);
            }

            if (invoice.MemberId != invoice.Subscription.MemberId)
            {
                return (PayInvoiceResult.DataIntegrityViolation, null);
            }

            bool isValidPair =
                (invoice.Status == "PENDING_PAYMENT" && invoice.Subscription.Status == "PENDING_PAYMENT") ||
                (invoice.Status == "PAID" && invoice.Subscription.Status == "CONFIRMED") ||
                (invoice.Status == "CANCELED" && invoice.Subscription.Status == "CANCELED");

            if (!isValidPair)
            {
                return (PayInvoiceResult.DataIntegrityViolation, null);
            }

            if (invoice.Status == "PAID")
            {
                return (PayInvoiceResult.AlreadyPaid, null);
            }

            if (invoice.Status != "PENDING_PAYMENT")
            {
                return (PayInvoiceResult.InvalidInvoiceState, null);
            }

            today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone));
            if (invoice.PaymentMethod != expectedMethod)
            {
                return (PayInvoiceResult.InvalidPaymentMethod, null);
            }
            if (invoice.Amount <= 0m || invoice.Amount != decimal.Truncate(invoice.Amount) ||
                invoice.PaidAt.HasValue || invoice.PaidBy is not null)
            {
                return (PayInvoiceResult.DataIntegrityViolation, null);
            }
            if (request.Amount.Value != invoice.Amount)
            {
                return (PayInvoiceResult.AmountMismatch, null);
            }
            if (invoice.Subscription.IsSuspended || invoice.Subscription.DurationMonths <= 0)
            {
                return (PayInvoiceResult.InvalidSubscriptionState, null);
            }
            string normalizedKind = invoice.Subscription.Kind.Trim().ToUpperInvariant();
            if (normalizedKind != "REGISTER" && normalizedKind != "RENEW" &&
                normalizedKind != "UPGRADE" && normalizedKind != "DOWNGRADE" &&
                normalizedKind != "CHANGE")
            {
                return (PayInvoiceResult.UnsupportedOrderKind, null);
            }
            if (invoice.Amount != invoice.Subscription.PackagePrice)
            {
                return (PayInvoiceResult.DataIntegrityViolation, null);
            }

            MembershipPackage? package = await _context.MembershipPackages
                .FirstOrDefaultAsync(p => p.Id == invoice.Subscription.PackageId);

            if (package is null)
            {
                return (PayInvoiceResult.PackageNotFound, null);
            }

            if (!package.IsActive)
            {
                return (PayInvoiceResult.PackageInactive, null);
            }
            MemberSubscription? latestConfirmed = await _context.MemberSubscriptions
                .Where(s => s.MemberId == memberAccountId && s.Status == "CONFIRMED" && s.Id != invoice.Subscription.Id)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();
            DateTime nowUtc = DateTime.UtcNow;
            DateOnly businessToday = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone));
            DateOnly orderDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(invoice.CreatedAt, DateTimeKind.Utc), vnTimeZone));
            DateOnly expectedStart;
            string expectedKind;
            if (latestConfirmed is null)
            {
                expectedStart = orderDate;
                expectedKind = "REGISTER";
            }
            else if (latestConfirmed.EndDate < orderDate)
            {
                expectedStart = orderDate;
                expectedKind = latestConfirmed.PackageId == invoice.Subscription.PackageId ? "RENEW" : "REGISTER";
            }
            else
            {
                expectedStart = latestConfirmed.EndDate.AddDays(1);
                expectedKind = latestConfirmed.PackageId == invoice.Subscription.PackageId
                    ? "RENEW"
                    : invoice.Subscription.PackagePrice > latestConfirmed.PackagePrice
                        ? "UPGRADE"
                        : invoice.Subscription.PackagePrice < latestConfirmed.PackagePrice
                            ? "DOWNGRADE"
                            : "CHANGE";
            }
            DateOnly expectedEnd = invoice.Subscription.StartDate
                .AddMonths(invoice.Subscription.DurationMonths).AddDays(-1);
            if (invoice.Subscription.StartDate != expectedStart ||
                invoice.Subscription.EndDate != expectedEnd ||
                normalizedKind != expectedKind)
            {
                return (PayInvoiceResult.InvalidSubscriptionState, null);
            }

            bool coverageWasPlannedFromOrderDate = latestConfirmed is null || latestConfirmed.EndDate < orderDate;
            if (coverageWasPlannedFromOrderDate && expectedStart < businessToday)
            {
                invoice.Subscription.StartDate = businessToday;
                invoice.Subscription.EndDate = businessToday
                    .AddMonths(invoice.Subscription.DurationMonths)
                    .AddDays(-1);
            }
            invoice.Subscription.Status = "CONFIRMED";

            invoice.Status = "PAID";
            invoice.PaymentMethod = normalizedPaymentMethod;
            invoice.PaidAt = nowUtc;
            invoice.PaidBy = staffAccountId;

            _context.AuditLogs.Add(new AuditLog
            {
                Id = providerEventId ?? Guid.NewGuid().ToString(), AccountId = staffAccountId,
                Action = isDemo ? DemoPaymentAuditAction : "MEMBERSHIP_INVOICE_PAID", EntityType = "MembershipInvoice",
                EntityId = invoice.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Description = isDemo ? "SIMULATED PAYMENT: no money transferred; test membership activated."
                    : expectedMethod + " collection confirmed; invoice PAID and subscription CONFIRMED.",
                CreatedAt = nowUtc
            });
            await _context.SaveChangesAsync();

            List<string> benefits = Services.Utils.MembershipBenefits.Read(invoice.Subscription.Benefits)
                ?? new List<string>();

            MembershipReceiptAPIViewModel receipt = new MembershipReceiptAPIViewModel
            {
                IsDemo = isDemo,
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = invoice.Amount,
                PaymentMethod = invoice.PaymentMethod,
                InvoiceStatus = invoice.Status,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                PaidByStaffId = invoice.PaidBy,
                PaidByStaffName = staffFullName,
                SubscriptionId = invoice.Subscription.Id,
                SubscriptionStatus = invoice.Subscription.Status,
                IsSuspended = invoice.Subscription.IsSuspended,
                SuspensionReason = invoice.Subscription.SuspensionReason,
                Kind = invoice.Subscription.Kind,
                StartDate = invoice.Subscription.StartDate,
                EndDate = invoice.Subscription.EndDate,
                PackageId = invoice.Subscription.PackageId,
                PackageName = invoice.Subscription.PackageName,
                PackagePrice = invoice.Subscription.PackagePrice,
                DurationMonths = invoice.Subscription.DurationMonths,
                Benefits = benefits,
                MemberAccountId = member.AccountId,
                MemberCode = member.MemberCode,
                MemberFullName = member.FullName,
                MemberEmail = member.Account.Email,
                MemberPhone = member.Account.Phone
            };

            await transaction.CommitAsync();

            return (PayInvoiceResult.Success, receipt);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            if (IsDeadlockVictim(ex))
            {
                return (PayInvoiceResult.ConcurrencyConflict, null);
            }
            throw;
        }
    }

    public async Task<(GetReceiptResult Result, MembershipReceiptAPIViewModel? Receipt)> GetReceiptAsync(
        int invoiceId,
        string staffAccountId)
    {
        Account? staff = await _context.Accounts
            .Include(account => account.Role)
            .Include(account => account.Receptionist)
            .Include(account => account.CenterManager)
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == staffAccountId);

        if (staff is null)
        {
            return (GetReceiptResult.StaffNotFound, null);
        }

        if (staff.Role is null ||
            (staff.Role.Name != "Receptionist" &&
             staff.Role.Name != "CenterManager" &&
             staff.Role.Name != "Member"))
        {
            return (GetReceiptResult.StaffRoleNotAllowed, null);
        }

        if (staff.DeletedAt is not null || !string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return (GetReceiptResult.StaffInactive, null);
        }

        if (staff.IsLocked)
        {
            return (GetReceiptResult.StaffLocked, null);
        }

        MembershipInvoice? invoice = await _context.MembershipInvoices
            .AsNoTracking()
            .Include(i => i.Subscription)
            .Include(i => i.Member)
            .ThenInclude(m => m.Account)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice is null)
        {
            return (GetReceiptResult.InvoiceNotFound, null);
        }

        if (staff.Role.Name == "Member" && invoice.MemberId != staffAccountId)
        {
            return (GetReceiptResult.InvoiceNotFound, null);
        }

        if (invoice.Subscription is null ||
            invoice.MemberId != invoice.Subscription.MemberId)
        {
            return (GetReceiptResult.DataIntegrityViolation, null);
        }

        bool isValidPair =
            (invoice.Status == "PENDING_PAYMENT" && invoice.Subscription.Status == "PENDING_PAYMENT") ||
            (invoice.Status == "PAID" && invoice.Subscription.Status == "CONFIRMED") ||
            (invoice.Status == "CANCELED" && invoice.Subscription.Status == "CANCELED");

        if (!isValidPair)
        {
            return (GetReceiptResult.DataIntegrityViolation, null);
        }

        if (invoice.Status != "PAID")
        {
            return (GetReceiptResult.ReceiptNotAvailable, null);
        }

        if (!invoice.PaidAt.HasValue || string.IsNullOrWhiteSpace(invoice.PaidBy))
        {
            return (GetReceiptResult.DataIntegrityViolation, null);
        }

        Account? paidByStaff = await _context.Accounts
            .Include(a => a.Receptionist)
            .Include(a => a.CenterManager)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == invoice.PaidBy);

        string? paidByStaffName = paidByStaff?.Receptionist?.FullName ?? paidByStaff?.CenterManager?.FullName;

        List<string> benefits = Services.Utils.MembershipBenefits.Read(invoice.Subscription.Benefits)
            ?? new List<string>();

        MembershipReceiptAPIViewModel receipt = new MembershipReceiptAPIViewModel
        {
            IsDemo = await _context.AuditLogs.AsNoTracking().AnyAsync(item =>
                item.Id == "demo-payment-" + invoiceId && item.Action == DemoPaymentAuditAction),
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Amount = invoice.Amount,
            PaymentMethod = invoice.PaymentMethod,
            InvoiceStatus = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            PaidAt = invoice.PaidAt,
            PaidByStaffId = invoice.PaidBy,
            PaidByStaffName = paidByStaffName,
            SubscriptionId = invoice.Subscription.Id,
            SubscriptionStatus = invoice.Subscription.Status,
            IsSuspended = invoice.Subscription.IsSuspended,
            SuspensionReason = invoice.Subscription.SuspensionReason,
            Kind = invoice.Subscription.Kind,
            StartDate = invoice.Subscription.StartDate,
            EndDate = invoice.Subscription.EndDate,
            PackageId = invoice.Subscription.PackageId,
            PackageName = invoice.Subscription.PackageName,
            PackagePrice = invoice.Subscription.PackagePrice,
            DurationMonths = invoice.Subscription.DurationMonths,
            Benefits = benefits,
            MemberAccountId = invoice.Member.AccountId,
            MemberCode = invoice.Member.MemberCode,
            MemberFullName = invoice.Member.FullName,
            MemberEmail = invoice.Member.Account.Email,
            MemberPhone = invoice.Member.Account.Phone
        };

        return (GetReceiptResult.Success, receipt);
    }

    public async Task<List<MembershipReceiptAPIViewModel>> GetInvoicesAsync(
        string staffAccountId, string? memberId = null, string? status = null,
        string? search = null, string? paymentMethod = null)
    {
        PagedMembershipInvoiceAPIViewModel result = await QueryInvoicesAsync(
            staffAccountId, memberId, status, search, paymentMethod, null, 20, null, null);
        return result.Items;
    }

    public Task<PagedMembershipInvoiceAPIViewModel> SearchInvoicesAsync(
        string callerAccountId, string? memberId, string? status, string? search,
        string? paymentMethod, int page, int pageSize,
        DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }
        return QueryInvoicesAsync(
            callerAccountId, memberId, status, search, paymentMethod, page, pageSize, fromDate, toDate);
    }

    private async Task<PagedMembershipInvoiceAPIViewModel> QueryInvoicesAsync(
        string staffAccountId,
        string? memberId = null,
        string? status = null,
        string? search = null,
        string? paymentMethod = null, int? page = null, int pageSize = 20,
        DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        Account? caller = await _context.Accounts
            .Include(a => a.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == staffAccountId);

        if (caller is null ||
            caller.Role is null ||
            (caller.Role.Name != "Receptionist" &&
             caller.Role.Name != "CenterManager" &&
             caller.Role.Name != "Member"))
        {
            return new PagedMembershipInvoiceAPIViewModel { Page = page ?? 1, PageSize = pageSize };
        }

        if (caller.DeletedAt is not null || !string.Equals(caller.Status, "Active", StringComparison.OrdinalIgnoreCase) || caller.IsLocked)
        {
            return new PagedMembershipInvoiceAPIViewModel { Page = page ?? 1, PageSize = pageSize };
        }

        IQueryable<MembershipInvoice> query = _context.MembershipInvoices
            .AsNoTracking()
            .Include(i => i.Subscription)
            .Include(i => i.Member)
                .ThenInclude(m => m.Account)
            .Include(i => i.PaidByNavigation)
                .ThenInclude(p => p!.Receptionist)
            .Include(i => i.PaidByNavigation)
                .ThenInclude(p => p!.CenterManager);

        if (caller.Role.Name == "Member")
        {
            query = query.Where(i => i.MemberId == staffAccountId);
        }
        else if (!string.IsNullOrWhiteSpace(memberId))
        {
            string trimmedMemberId = memberId.Trim();
            query = query.Where(i => i.MemberId == trimmedMemberId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(i => i.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            string normalizedPaymentMethod = paymentMethod.Trim().ToUpperInvariant();
            query = query.Where(i => i.PaymentMethod == normalizedPaymentMethod);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(i =>
                i.InvoiceNumber.Contains(trimmedSearch) ||
                (i.Member.FullName != null && i.Member.FullName.Contains(trimmedSearch)) ||
                (i.Member.Account != null && i.Member.Account.Email.Contains(trimmedSearch)) ||
                (i.Member.Account != null && i.Member.Account.Phone != null && i.Member.Account.Phone.Contains(trimmedSearch)) ||
                i.Member.MemberCode.Contains(trimmedSearch));
        }

        if (fromDate.HasValue || toDate.HasValue)
        {
            // CreatedAt is stored in UTC; the counter filters by the centre's
            // own calendar day, so the bounds are converted the same way the
            // revenue report converts them.
            TimeZoneInfo centreTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                _configuration["BusinessSettings:TimeZoneId"]
                    ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured."));

            if (fromDate.HasValue)
            {
                DateTime fromUtc = TimeZoneInfo.ConvertTimeToUtc(
                    fromDate.Value.ToDateTime(TimeOnly.MinValue), centreTimeZone);
                query = query.Where(i => i.CreatedAt >= fromUtc);
            }

            if (toDate.HasValue)
            {
                DateTime toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(
                    toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), centreTimeZone);
                query = query.Where(i => i.CreatedAt < toUtcExclusive);
            }
        }

        int totalItems = await query.CountAsync();
        MembershipInvoiceSummaryAPIViewModel summary = new MembershipInvoiceSummaryAPIViewModel
        {
            PaidCount = await query.CountAsync(i => i.Status == "PAID"),
            PaidAmount = await query.Where(i => i.Status == "PAID").SumAsync(i => (decimal?)i.Amount) ?? 0m,
            PendingCount = await query.CountAsync(i => i.Status == "PENDING_PAYMENT"),
            PendingAmount = await query.Where(i => i.Status == "PENDING_PAYMENT").SumAsync(i => (decimal?)i.Amount) ?? 0m,
            CanceledCount = await query.CountAsync(i => i.Status == "CANCELED"),
            CanceledAmount = await query.Where(i => i.Status == "CANCELED").SumAsync(i => (decimal?)i.Amount) ?? 0m
        };
        query = query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id);
        if (page.HasValue)
        {
            query = query.Skip((page.Value - 1) * pageSize).Take(pageSize);
        }
        List<MembershipInvoice> invoices = await query.ToListAsync();
        string[] demoAuditIds = invoices.Select(item => "demo-payment-" + item.Id).ToArray();
        HashSet<string> demoPayments = (await _context.AuditLogs.AsNoTracking()
            .Where(item => demoAuditIds.Contains(item.Id) && item.Action == DemoPaymentAuditAction)
            .Select(item => item.Id).ToListAsync()).ToHashSet(StringComparer.Ordinal);

        List<MembershipReceiptAPIViewModel> results = new List<MembershipReceiptAPIViewModel>();
        foreach (MembershipInvoice invoice in invoices)
        {
            if (invoice.Subscription is null || invoice.Member is null || invoice.Member.Account is null)
            {
                continue;
            }

            string? paidByStaffName =
                invoice.PaidByNavigation?.Receptionist?.FullName ??
                invoice.PaidByNavigation?.CenterManager?.FullName;

            List<string> benefits;
            try
            {
                benefits = Services.Utils.MembershipBenefits.Read(invoice.Subscription.Benefits)
                    ?? new List<string>();
            }
            catch
            {
                benefits = new List<string>();
            }

            results.Add(new MembershipReceiptAPIViewModel
            {
                IsDemo = demoPayments.Contains("demo-payment-" + invoice.Id),
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = invoice.Amount,
                PaymentMethod = invoice.PaymentMethod,
                InvoiceStatus = invoice.Status,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                PaidByStaffId = invoice.PaidBy,
                PaidByStaffName = paidByStaffName,
                SubscriptionId = invoice.Subscription.Id,
                SubscriptionStatus = invoice.Subscription.Status,
                IsSuspended = invoice.Subscription.IsSuspended,
                SuspensionReason = invoice.Subscription.SuspensionReason,
                Kind = invoice.Subscription.Kind,
                StartDate = invoice.Subscription.StartDate,
                EndDate = invoice.Subscription.EndDate,
                PackageId = invoice.Subscription.PackageId,
                PackageName = invoice.Subscription.PackageName,
                PackagePrice = invoice.Subscription.PackagePrice,
                DurationMonths = invoice.Subscription.DurationMonths,
                Benefits = benefits,
                MemberAccountId = invoice.Member.AccountId,
                MemberCode = invoice.Member.MemberCode,
                MemberFullName = invoice.Member.FullName,
                MemberEmail = invoice.Member.Account.Email,
                MemberPhone = invoice.Member.Account.Phone
            });
        }

        return new PagedMembershipInvoiceAPIViewModel
        {
            Items = results,
            Page = page ?? 1,
            PageSize = pageSize,
            TotalItems = totalItems,
            Summary = summary
        };
    }

    public async Task<CancelInvoiceResult> CancelPendingInvoiceAsync(
        int invoiceId,
        string callerAccountId)
    {
        Account? caller = await _context.Accounts
            .Include(a => a.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == callerAccountId);

        if (caller is null)
        {
            return CancelInvoiceResult.CallerNotFound;
        }

        if (caller.Role is null ||
            (caller.Role.Name != "Receptionist" &&
             caller.Role.Name != "CenterManager" &&
             caller.Role.Name != "Member"))
        {
            return CancelInvoiceResult.CallerRoleNotAllowed;
        }

        if (caller.DeletedAt is not null ||
            !string.Equals(caller.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return CancelInvoiceResult.CallerInactive;
        }

        if (caller.IsLocked)
        {
            return CancelInvoiceResult.CallerLocked;
        }

        string? memberId = await _context.MembershipInvoices
            .AsNoTracking()
            .Where(item => item.Id == invoiceId)
            .Select(item => item.MemberId)
            .FirstOrDefaultAsync();
        if (memberId is null || (caller.Role.Name == "Member" && memberId != callerAccountId))
        {
            return CancelInvoiceResult.InvoiceNotFound;
        }

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead)
            : null;

        try
        {
            // Payment and cancellation serialize on the same member row.
            if (_context.Database.IsSqlServer())
            {
                Member? lockedMember = await _context.Members
                    .FromSqlInterpolated($"SELECT * FROM [dbo].[Member] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {memberId}")
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                if (lockedMember is null)
                {
                    return CancelInvoiceResult.InvalidInvoiceState;
                }
            }

            MembershipInvoice? invoice = await _context.MembershipInvoices
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice is null)
            {
                return CancelInvoiceResult.InvoiceNotFound;
            }

            if (invoice.MemberId != memberId)
            {
                return CancelInvoiceResult.InvoiceNotFound;
            }

            if (invoice.Status == "PAID" && invoice.Subscription?.Status == "CONFIRMED")
            {
                return CancelInvoiceResult.ConfirmedSubscriptionCannotBeCanceled;
            }

            if (invoice.Status != "PENDING_PAYMENT")
            {
                return CancelInvoiceResult.InvalidInvoiceState;
            }

            if (invoice.Subscription is null || invoice.Subscription.Status != "PENDING_PAYMENT" ||
                invoice.Subscription.MemberId != memberId)
            {
                return CancelInvoiceResult.InvalidInvoiceState;
            }

            DateTime nowUtc = DateTime.UtcNow;
            invoice.Status = "CANCELED";
            invoice.Subscription.Status = "CANCELED";

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                AccountId = callerAccountId,
                Action = "INVOICE_CANCELED",
                EntityType = "MembershipInvoice",
                EntityId = invoice.InvoiceNumber,
                Description = $"Hủy hóa đơn chờ thanh toán {invoice.InvoiceNumber} (gói \"{invoice.Subscription.PackageName}\").",
                CreatedAt = nowUtc
            });

            await _context.SaveChangesAsync();
            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }
            return CancelInvoiceResult.Success;
        }
        catch (Exception ex)
        {
            if (IsDeadlockVictim(ex))
            {
                return CancelInvoiceResult.ConcurrencyConflict;
            }
            throw;
        }
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
}
