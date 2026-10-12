using System.Globalization;
using APIViewModel;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.Flow2PhucService;

public class Flow2PhucService : IFlow2PhucService
{
    private readonly SportsCenterManagementContext _context;

    public Flow2PhucService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RoomItemDto>> GetRoomsAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Room> query = _context.Rooms.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(room => room.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch = search.Trim();
            query = query.Where(room =>
                room.Name.Contains(normalizedSearch) ||
                (room.Location != null && room.Location.Contains(normalizedSearch)));
        }

        int totalItems = await query.CountAsync();
        List<RoomItemDto> items = await query
            .OrderBy(room => room.Name)
            .ThenBy(room => room.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(room => new RoomItemDto
            {
                Id = room.Id,
                Name = room.Name,
                Location = room.Location,
                Capacity = room.Capacity,
                IsActive = room.IsActive,
                CreatedAt = room.CreatedAt,
                UpdatedAt = room.UpdatedAt
            })
            .ToListAsync();

        return new PagedResult<RoomItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<RoomItemDto> CreateRoomAsync(CreateRoomRequestDto request)
    {
        string normalizedName = request.Name.Trim();
        bool duplicateName = await _context.Rooms.AnyAsync(room => room.Name == normalizedName);
        if (duplicateName)
        {
            throw new BusinessRuleException("ROOM_NAME_DUPLICATE", "Tên phòng đã tồn tại trên hệ thống.");
        }

        Room room = new Room
        {
            Name = normalizedName,
            Location = NormalizeOptional(request.Location),
            Capacity = request.Capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        return MapRoom(room);
    }

    public async Task<RoomItemDto> UpdateRoomAsync(int id, UpdateRoomRequestDto request)
    {
        Room? room = await _context.Rooms.FindAsync(id);
        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng.");
        }

        string normalizedName = request.Name.Trim();
        bool duplicateName = await _context.Rooms.AnyAsync(candidate =>
            candidate.Id != id && candidate.Name == normalizedName);
        if (duplicateName)
        {
            throw new BusinessRuleException("ROOM_NAME_DUPLICATE", "Tên phòng đã tồn tại.");
        }

        if (request.Capacity < room.Capacity)
        {
            DateTime today = DateTime.UtcNow.Date;
            bool capacityConflict = await _context.ClassSessions.AnyAsync(session =>
                session.RoomId == id &&
                session.SessionDate >= today &&
                session.Status == "SCHEDULED" &&
                session.Capacity > request.Capacity);
            if (capacityConflict)
            {
                throw new BusinessRuleException(
                    "ROOM_CAPACITY_BELOW_SCHEDULE",
                    "Không thể giảm sức chứa nhỏ hơn cấu hình của các buổi học tương lai.");
            }
        }

        room.Name = normalizedName;
        room.Location = NormalizeOptional(request.Location);
        room.Capacity = request.Capacity;
        room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapRoom(room);
    }

    public async Task<RoomItemDto> UpdateRoomStatusAsync(int id, bool isActive)
    {
        Room? room = await _context.Rooms.FindAsync(id);
        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng.");
        }

        if (!isActive && room.IsActive)
        {
            DateTime today = DateTime.UtcNow.Date;
            bool hasFutureSession = await _context.ClassSessions.AnyAsync(session =>
                session.RoomId == id &&
                session.SessionDate >= today &&
                session.Status == "SCHEDULED");
            if (hasFutureSession)
            {
                throw new BusinessRuleException(
                    "ROOM_INACTIVE",
                    "Không thể ngừng hoạt động phòng đang có các buổi học tương lai.");
            }
        }

        room.IsActive = isActive;
        room.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapRoom(room);
    }

    public async Task<SessionResponseDto> AssignCoachAsync(int sessionId, string coachId, string actorId)
    {
        _ = actorId;
        string normalizedCoachId = coachId.Trim();
        ClassSession? session = await _context.ClassSessions
            .Include(candidate => candidate.Room)
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId);
        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        if (session.Status == "CANCELED")
        {
            throw new BusinessRuleException(
                "SESSION_CANCELLED",
                "Không thể phân công HLV cho buổi học đã bị hủy.");
        }

        Account? coachAccount = await _context.Accounts
            .Include(account => account.Role)
            .Include(account => account.Coach)
            .FirstOrDefaultAsync(account => account.Id == normalizedCoachId);
        if (coachAccount is null || coachAccount.Coach is null || coachAccount.Role.Name != "Coach")
        {
            throw new BusinessRuleException("COACH_NOT_FOUND", "Không tìm thấy huấn luyện viên.");
        }

        if (!string.Equals(coachAccount.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("COACH_INACTIVE", "Huấn luyện viên đang ngừng hoạt động.");
        }

        bool isConflicted = await _context.ClassSessions.AnyAsync(candidate =>
            candidate.Id != sessionId &&
            candidate.CoachId == normalizedCoachId &&
            candidate.SessionDate == session.SessionDate &&
            candidate.Status == "SCHEDULED" &&
            candidate.StartTime < session.EndTime &&
            candidate.EndTime > session.StartTime);
        if (isConflicted)
        {
            throw new BusinessRuleException(
                "COACH_SCHEDULE_CONFLICT",
                "Huấn luyện viên đã có lịch dạy khác trong khung giờ này.");
        }

        session.CoachId = normalizedCoachId;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapSession(session);
    }

    private static RoomItemDto MapRoom(Room room) => new RoomItemDto
    {
        Id = room.Id,
        Name = room.Name,
        Location = room.Location,
        Capacity = room.Capacity,
        IsActive = room.IsActive,
        CreatedAt = room.CreatedAt,
        UpdatedAt = room.UpdatedAt
    };

    private static SessionResponseDto MapSession(ClassSession session)
    {
        string date = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string startTime = session.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        string endTime = session.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        return new SessionResponseDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            CoachId = session.CoachId,
            RoomId = session.RoomId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            StartsAt = $"{date}T{startTime}:00+07:00",
            EndsAt = $"{date}T{endTime}:00+07:00",
            Capacity = session.Capacity,
            BookedCount = session.BookedCount,
            Status = session.Status,
            Notes = session.Notes
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}