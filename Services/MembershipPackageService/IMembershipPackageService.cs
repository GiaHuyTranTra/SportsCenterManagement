using APIViewModel.MembershipPackage;

namespace Services.MembershipPackageService;

public enum CreatePackageResult
{
    Success,
    DuplicateName,
    InvalidData
}

public enum UpdatePackageResult
{
    Success,
    NotFound,
    DuplicateName,
    InvalidData
}

public enum UpdatePackageStatusResult
{
    Success,
    NotFound
}

public enum DeletePackageResult
{
    Success,
    NotFound,
    PackageInUse
}

public interface IMembershipPackageService
{
    Task<List<ActiveMembershipPackageAPIViewModel>> GetActivePackagesAsync();

    Task<PagedMembershipPackageResultAPIViewModel> GetPackagesAsync(
        int page,
        int pageSize,
        string? search,
        string? status);

    Task<MembershipPackageDetailAPIViewModel?> GetPackageByIdAsync(int id);

    Task<(CreatePackageResult Result, MembershipPackageDetailAPIViewModel? Package)> CreatePackageAsync(
        MembershipPackageInputAPIViewModel request, string? actorAccountId = null);

    Task<(UpdatePackageResult Result, MembershipPackageDetailAPIViewModel? Package)> UpdatePackageAsync(
        int id,
        MembershipPackageInputAPIViewModel request, string? actorAccountId = null);

    Task<UpdatePackageStatusResult> UpdatePackageStatusAsync(int id, bool isActive, string? actorAccountId = null);

    Task<DeletePackageResult> DeletePackageAsync(int id, string? actorAccountId = null);

    Task<APIViewModel.MembershipPackageSport.PackageSportsViewModel> GetPackageSportsAsync(int packageId);

    Task<APIViewModel.MembershipPackageSport.PackageSportsViewModel> UpdatePackageSportsAsync(
        string actorAccountId,
        int packageId,
        List<int>? sportIds);
}
