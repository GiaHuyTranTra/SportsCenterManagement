using APIViewModel;

namespace Services.Flow2PhucService;

public interface IFlow2PhucService
{
    Task<PagedResult<RoomItemDto>> GetRoomsAsync(int page, int pageSize, string? search, bool? isActive);

    Task<RoomItemDto> CreateRoomAsync(CreateRoomRequestDto request);

    Task<RoomItemDto> UpdateRoomAsync(int id, UpdateRoomRequestDto request);

    Task<RoomItemDto> UpdateRoomStatusAsync(int id, bool isActive);

    Task<SessionResponseDto> AssignCoachAsync(int sessionId, string coachId, string actorId);
}