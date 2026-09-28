using SportsCenterManagement.DTOs.Package;
using Services.Interfaces;

namespace Services.Implementations;

public class MembershipPackageService : IMembershipPackageService
{
	public Task<List<PackageResponseDto>> GetActivePackagesAsync()
	{
		return Task.FromResult(new List<PackageResponseDto>());
	}
}
