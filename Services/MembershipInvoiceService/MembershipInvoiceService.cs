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

namespace Services.MembershipInvoiceService;

public class MembershipInvoiceService : IMembershipInvoiceService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IConfiguration _configuration;

    public MembershipInvoiceService(
        SportsCenterManagementContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(PayInvoiceResult Result, MembershipReceiptAPIViewModel? Receipt)> PayInvoiceAsync(
        int invoiceId,
        string staffAccountId,
        PayMembershipInvoiceAPIViewModel request)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return (PayInvoiceResult.InvalidPaymentMethod, null);
        }

        string normalizedPaymentMethod = request.PaymentMethod.Trim().ToUpperInvariant();
        if (normalizedPaymentMethod != "CASH" &&
            normalizedPaymentMethod != "BANK_TRANSFER" &&
            normalizedPaymentMethod != "CARD")
        {
            return (PayInvoiceResult.InvalidPaymentMethod, null);
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

            if (staff.Role.Name != "Receptionist" && staff.Role.Name != "CenterManager")
            {
                return (PayInvoiceResult.StaffRoleNotAllowed, null);
            }

            if (!string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (PayInvoiceResult.StaffInactive, null);
            }

            if (staff.IsLocked)
            {
                return (PayInvoiceResult.StaffLocked, null);
            }

            string? staffFullName = staff.Receptionist?.FullName ?? staff.CenterManager?.FullName;

            Member? member = await _context.Members
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Member] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {0}",
                    memberAccountId)
                .Include(m => m.Account)
                .ThenInclude(a => a.Role)
                .FirstOrDefaultAsync();

            if (member is null || member.Account.Role.Name != "Member")
            {
                return (PayInvoiceResult.MemberNotFound, null);
            }

            if (!string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (PayInvoiceResult.MemberInactive, null);
            }

            if (member.Account.IsLocked)
            {
                return (PayInvoiceResult.MemberLocked, null);
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

            DateOnly endDate = startDate.AddMonths(invoice.Subscription.DurationMonths).AddDays(-1);

            DateTime nowUtc = DateTime.UtcNow;
            invoice.Subscription.Status = "CONFIRMED";
            invoice.Subscription.Kind = kind;
            invoice.Subscription.StartDate = startDate;
            invoice.Subscription.EndDate = endDate;

            invoice.Status = "PAID";
            invoice.PaymentMethod = normalizedPaymentMethod;
            invoice.PaidAt = nowUtc;
            invoice.PaidBy = staffAccountId;

            await _context.SaveChangesAsync();

            List<string> benefits = JsonSerializer.Deserialize<List<string>>(invoice.Subscription.Benefits)
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
                SubscriptionId = invoice.Subscription.Id,
                SubscriptionStatus = invoice.Subscription.Status,
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
            if (IsDeadlockVictim(ex))
            {
                return (PayInvoiceResult.ConcurrencyConflict, null);
            }

            await transaction.RollbackAsync();
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

        if (staff.Role.Name != "Receptionist" && staff.Role.Name != "CenterManager")
        {
            return (GetReceiptResult.StaffRoleNotAllowed, null);
        }

        if (!string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
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

        List<string> benefits = JsonSerializer.Deserialize<List<string>>(invoice.Subscription.Benefits)
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
            PaidByStaffName = paidByStaffName,
            SubscriptionId = invoice.Subscription.Id,
            SubscriptionStatus = invoice.Subscription.Status,
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
