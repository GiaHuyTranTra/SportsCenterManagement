using System.Text.Json;
using APIViewModel.MembershipPackage;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.MembershipPackageService;

public class MembershipPackageService : IMembershipPackageService
{
    private const int MaximumPageSize = 20;
    private const decimal MaximumPrice = 1000000000m;
    private readonly SportsCenterManagementContext _context;

    public MembershipPackageService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<List<ActiveMembershipPackageAPIViewModel>> GetActivePackagesAsync()
    {
        List<MembershipPackage> packages = await _context.MembershipPackages
            .AsNoTracking()
            .Where(package => package.IsActive)
            .OrderBy(package => package.Name)
            .ThenBy(package => package.Id)
            .ToListAsync();

        List<ActiveMembershipPackageAPIViewModel> results = packages
            .Select(package => new ActiveMembershipPackageAPIViewModel
            {
                Id = package.Id,
                Name = package.Name,
                Price = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = DeserializeBenefits(package.Benefits)
            })
            .ToList();

        return results;
    }

    public async Task<PagedMembershipPackageResultAPIViewModel> GetPackagesAsync(
        int page,
        int pageSize,
        string? search,
        string? status)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0 || pageSize > MaximumPageSize)
        {
            pageSize = MaximumPageSize;
        }

        IQueryable<MembershipPackage> query = _context.MembershipPackages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch = search.Trim();
            query = query.Where(package => package.Name.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim();
            if (string.Equals(normalizedStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(package => package.IsActive);
            }
            else if (string.Equals(normalizedStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(package => !package.IsActive);
            }
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / pageSize);

        List<MembershipPackage> pagedPackages = await query
            .OrderBy(package => package.Name)
            .ThenBy(package => package.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        int[] packageIds = pagedPackages
            .Select(package => package.Id)
            .ToArray();
        List<PackageSubscriberCount> subscriberCountRows = await _context.MemberSubscriptions
            .AsNoTracking()
            .Where(subscription => packageIds.Contains(subscription.PackageId))
            .GroupBy(subscription => subscription.PackageId)
            .Select(group => new PackageSubscriberCount
            {
                PackageId = group.Key,
                SubscriberCount = group
                    .Select(subscription => subscription.MemberId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync();
        Dictionary<int, int> subscriberCounts = subscriberCountRows.ToDictionary(
            item => item.PackageId,
            item => item.SubscriberCount);

        List<MembershipPackageListItemAPIViewModel> items = pagedPackages
            .Select(package => new MembershipPackageListItemAPIViewModel
            {
                Id = package.Id,
                Name = package.Name,
                Price = package.Price,
                DurationMonths = package.DurationMonths,
                Benefits = DeserializeBenefits(package.Benefits),
                SubscriberCount = subscriberCounts.GetValueOrDefault(package.Id),
                IsActive = package.IsActive,
                CreatedAt = package.CreatedAt
            })
            .ToList();

        return new PagedMembershipPackageResultAPIViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<MembershipPackageDetailAPIViewModel?> GetPackageByIdAsync(int id)
    {
        MembershipPackage? package = await _context.MembershipPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(currentPackage => currentPackage.Id == id);

        if (package is null)
        {
            return null;
        }

        return MapDetail(package);
    }

    public async Task<(CreatePackageResult Result, MembershipPackageDetailAPIViewModel? Package)> CreatePackageAsync(
        MembershipPackageInputAPIViewModel request)
    {
        string? normalizedName = NormalizeName(request.Name);
        List<string>? normalizedBenefits = ValidateAndNormalizeBenefits(request.Benefits);

        if (normalizedName is null ||
            !IsValidPrice(request.Price) ||
            !IsValidDuration(request.DurationMonths) ||
            normalizedBenefits is null)
        {
            return (CreatePackageResult.InvalidData, null);
        }

        bool duplicateName = await _context.MembershipPackages
            .AnyAsync(package => package.Name == normalizedName);

        if (duplicateName)
        {
            return (CreatePackageResult.DuplicateName, null);
        }

        MembershipPackage package = new MembershipPackage
        {
            Name = normalizedName,
            Price = request.Price,
            DurationMonths = request.DurationMonths,
            Benefits = SerializeBenefits(normalizedBenefits),
            IsActive = true
        };

        await _context.MembershipPackages.AddAsync(package);
        await _context.SaveChangesAsync();

        return (CreatePackageResult.Success, MapDetail(package));
    }

    public async Task<(UpdatePackageResult Result, MembershipPackageDetailAPIViewModel? Package)> UpdatePackageAsync(
        int id,
        MembershipPackageInputAPIViewModel request)
    {
        string? normalizedName = NormalizeName(request.Name);
        List<string>? normalizedBenefits = ValidateAndNormalizeBenefits(request.Benefits);

        if (normalizedName is null ||
            !IsValidPrice(request.Price) ||
            !IsValidDuration(request.DurationMonths) ||
            normalizedBenefits is null)
        {
            return (UpdatePackageResult.InvalidData, null);
        }

        MembershipPackage? package = await _context.MembershipPackages
            .FirstOrDefaultAsync(currentPackage => currentPackage.Id == id);

        if (package is null)
        {
            return (UpdatePackageResult.NotFound, null);
        }

        bool duplicateName = await _context.MembershipPackages
            .AnyAsync(currentPackage => currentPackage.Name == normalizedName && currentPackage.Id != id);

        if (duplicateName)
        {
            return (UpdatePackageResult.DuplicateName, null);
        }

        package.Name = normalizedName;
        package.Price = request.Price;
        package.DurationMonths = request.DurationMonths;
        package.Benefits = SerializeBenefits(normalizedBenefits);
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return (UpdatePackageResult.Success, MapDetail(package));
    }

    public async Task<UpdatePackageStatusResult> UpdatePackageStatusAsync(int id, bool isActive)
    {
        MembershipPackage? package = await _context.MembershipPackages
            .FirstOrDefaultAsync(currentPackage => currentPackage.Id == id);

        if (package is null)
        {
            return UpdatePackageStatusResult.NotFound;
        }

        package.IsActive = isActive;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return UpdatePackageStatusResult.Success;
    }

    public async Task<DeletePackageResult> DeletePackageAsync(int id)
    {
        MembershipPackage? package = await _context.MembershipPackages
            .FirstOrDefaultAsync(currentPackage => currentPackage.Id == id);

        if (package is null)
        {
            return DeletePackageResult.NotFound;
        }

        bool isPackageInUse = await _context.MemberSubscriptions
            .AnyAsync(subscription => subscription.PackageId == id);

        if (isPackageInUse)
        {
            return DeletePackageResult.PackageInUse;
        }

        _context.MembershipPackages.Remove(package);
        await _context.SaveChangesAsync();

        return DeletePackageResult.Success;
    }

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string normalizedName = name.Trim();
        return normalizedName.Length >= 2 && normalizedName.Length <= 80
            ? normalizedName
            : null;
    }

    private static bool IsValidPrice(decimal price)
    {
        return price >= 1m && price <= MaximumPrice;
    }

    private static bool IsValidDuration(int durationMonths)
    {
        return durationMonths == 1 || durationMonths == 3 || durationMonths == 12;
    }

    private static List<string>? ValidateAndNormalizeBenefits(List<string>? rawBenefits)
    {
        if (rawBenefits is null || rawBenefits.Count < 1 || rawBenefits.Count > 12)
        {
            return null;
        }

        List<string> normalizedBenefits = new List<string>();
        HashSet<string> uniqueBenefits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string? benefit in rawBenefits)
        {
            string normalizedBenefit = benefit?.Trim() ?? string.Empty;
            if (normalizedBenefit.Length == 0)
            {
                continue;
            }

            if (normalizedBenefit.Length > 200)
            {
                return null;
            }

            if (uniqueBenefits.Add(normalizedBenefit))
            {
                normalizedBenefits.Add(normalizedBenefit);
            }
        }

        return normalizedBenefits.Count == 0 ? null : normalizedBenefits;
    }

    private static string SerializeBenefits(List<string> benefits)
    {
        return JsonSerializer.Serialize(benefits);
    }

    private static List<string> DeserializeBenefits(string json)
    {
        List<string>? benefits = JsonSerializer.Deserialize<List<string>>(json);
        return benefits ?? new List<string>();
    }

    private static MembershipPackageDetailAPIViewModel MapDetail(MembershipPackage package)
    {
        return new MembershipPackageDetailAPIViewModel
        {
            Id = package.Id,
            Name = package.Name,
            Price = package.Price,
            DurationMonths = package.DurationMonths,
            Benefits = DeserializeBenefits(package.Benefits),
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private sealed class PackageSubscriberCount
    {
        public int PackageId { get; set; }

        public int SubscriberCount { get; set; }
    }
}
