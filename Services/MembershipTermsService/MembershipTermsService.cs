using APIViewModel.MembershipTerms;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.MembershipTermsService;

public sealed class MembershipTermsService : IMembershipTermsService
{
    private readonly SportsCenterManagementContext _context;

    public MembershipTermsService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<CurrentMembershipTermsAPIViewModel> GetCurrentAsync(string memberId)
    {
        MembershipTermAcceptance? acceptance = await _context.MembershipTermAcceptances
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.MemberId == memberId &&
                item.TermsVersion == MembershipTermsCatalog.CurrentVersion &&
                item.ContentHash == MembershipTermsCatalog.CurrentHash);

        return new CurrentMembershipTermsAPIViewModel
        {
            Version = MembershipTermsCatalog.CurrentVersion,
            Title = MembershipTermsCatalog.Title,
            EffectiveFrom = MembershipTermsCatalog.EffectiveFrom,
            ContentHash = MembershipTermsCatalog.CurrentHash,
            Sections = MembershipTermsCatalog.Sections.Select(CloneSection).ToList(),
            RequiredAcknowledgements = MembershipTermsCatalog.RequiredAcknowledgements
                .Select(CloneAcknowledgement)
                .ToList(),
            Accepted = acceptance is not null,
            AcceptedAtUtc = acceptance?.AcceptedAtUtc
        };
    }

    public async Task<AcceptMembershipTermsResult> AcceptCurrentAsync(
        string memberId,
        string version,
        string contentHash)
    {
        if (!string.Equals(version?.Trim(), MembershipTermsCatalog.CurrentVersion, StringComparison.Ordinal) ||
            !string.Equals(contentHash?.Trim(), MembershipTermsCatalog.CurrentHash, StringComparison.OrdinalIgnoreCase))
        {
            return AcceptMembershipTermsResult.VersionMismatch;
        }

        Member? member = await _context.Members
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .FirstOrDefaultAsync(item => item.AccountId == memberId);
        if (member is null)
        {
            return AcceptMembershipTermsResult.MemberNotFound;
        }
        if (member.Account.Role.Name != "Member" ||
            !string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            member.Account.IsLocked ||
            member.Account.DeletedAt is not null)
        {
            return AcceptMembershipTermsResult.MemberNotEligible;
        }

        MembershipTermAcceptance acceptance = new()
        {
            MemberId = memberId,
            TermsVersion = MembershipTermsCatalog.CurrentVersion,
            ContentHash = MembershipTermsCatalog.CurrentHash,
            AcceptanceChannel = "MEMBER_WEB",
            AcceptedAtUtc = DateTime.UtcNow
        };
        _context.MembershipTermAcceptances.Add(acceptance);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.Entry(acceptance).State = EntityState.Detached;
            bool committedByAnotherRequest = await _context.MembershipTermAcceptances
                .AsNoTracking()
                .AnyAsync(item =>
                    item.MemberId == memberId &&
                    item.TermsVersion == MembershipTermsCatalog.CurrentVersion &&
                    item.ContentHash == MembershipTermsCatalog.CurrentHash);
            if (!committedByAnotherRequest) throw;
        }

        return AcceptMembershipTermsResult.Success;
    }

    public async Task<MemberTermsStatusAPIViewModel> GetMemberStatusAsync(string memberId)
    {
        MembershipTermAcceptance? acceptance = await _context.MembershipTermAcceptances
            .AsNoTracking()
            .Where(item => item.MemberId == memberId &&
                item.TermsVersion == MembershipTermsCatalog.CurrentVersion &&
                item.ContentHash == MembershipTermsCatalog.CurrentHash)
            .OrderByDescending(item => item.AcceptedAtUtc)
            .FirstOrDefaultAsync();

        return new MemberTermsStatusAPIViewModel
        {
            MemberId = memberId,
            CurrentVersion = MembershipTermsCatalog.CurrentVersion,
            Accepted = acceptance is not null,
            AcceptedVersion = acceptance?.TermsVersion,
            AcceptedAtUtc = acceptance?.AcceptedAtUtc
        };
    }

    private static MembershipTermsSectionAPIViewModel CloneSection(MembershipTermsSectionAPIViewModel section) => new()
    {
        Id = section.Id,
        Title = section.Title,
        Body = section.Body
    };

    private static MembershipTermsAcknowledgementAPIViewModel CloneAcknowledgement(
        MembershipTermsAcknowledgementAPIViewModel acknowledgement) => new()
    {
        Id = acknowledgement.Id,
        Label = acknowledgement.Label
    };
}
