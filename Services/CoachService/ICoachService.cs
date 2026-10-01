using System.Threading.Tasks;
using APIViewModel.Coach;

namespace Services.CoachService;

public interface ICoachService
{
    Task<PagedCoachResultAPIViewModel> GetCoachesAsync(
        int page,
        int pageSize,
        string? search,
        string? status);

    Task<CoachDetailAPIViewModel?> GetCoachByIdAsync(string accountId);

    Task<bool> UpdateCoachAsync(string accountId, UpdateCoachAPIViewModel request);

    Task<bool> UpdateCoachStatusAsync(string accountId, UpdateCoachStatusAPIViewModel request);
}
