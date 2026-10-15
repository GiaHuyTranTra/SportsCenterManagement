using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Room;

namespace Services.RoomService;

public interface IRoomService
{
    Task<PagedResult<RoomViewModel>> GetRoomsPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive);

    Task<RoomViewModel?> GetRoomByIdAsync(int id);

    Task<RoomViewModel> CreateRoomAsync(
        string actorAccountId,
        CreateRoomAPIViewModel request);

    Task<RoomViewModel> UpdateRoomAsync(
        string actorAccountId,
        int id,
        UpdateRoomAPIViewModel request);

    Task<RoomViewModel> UpdateRoomStatusAsync(
        string actorAccountId,
        int id,
        bool isActive);
}
