using SportsCenterManagement.DTOs.Package;

namespace Services.Interfaces;

public interface IMembershipPackageService
{
	Task<List<PackageResponseDto>> GetActivePackagesAsync();
}
