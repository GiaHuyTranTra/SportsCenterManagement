using System.Threading.Tasks;
using APIViewModel.Receptionist;

namespace Services.ReceptionistService;

public interface IReceptionistService
{
    Task<PagedReceptionistResultAPIViewModel> GetReceptionistsAsync(
        int page,
        int pageSize,
        string? search,
        string? status);

    Task<ReceptionistDetailAPIViewModel?> GetReceptionistByIdAsync(string accountId);

    Task<bool> UpdateReceptionistAsync(string accountId, UpdateReceptionistAPIViewModel request);

    Task<bool> UpdateReceptionistStatusAsync(string accountId, UpdateReceptionistStatusAPIViewModel request);
}
