using Services.ClassScheduling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Room;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.ScheduleQueryService;

namespace Services.RoomService;

public class RoomService : IRoomService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IAuditLogService _auditLogService;

    public RoomService(
        SportsCenterManagementContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<RoomViewModel>> GetRoomsPagedAsync(
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
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            query = query.Where(item => item.Name.Contains(keyword) ||
                (item.Location != null && item.Location.Contains(keyword)));
        }

        int totalItems = await query.CountAsync();

        List<RoomViewModel> items = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => new RoomViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Location = item.Location,
                Capacity = item.Capacity,
                RoomType = item.RoomType,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync();

        return new PagedResult<RoomViewModel>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<RoomViewModel?> GetRoomByIdAsync(int id)
    {
        Room? room = await _context.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (room is null)
        {
            return null;
        }

        return MapRoom(room);
    }

    public async Task<RoomViewModel> CreateRoomAsync(
        string actorAccountId,
        CreateRoomAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        string normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Tên phòng không hợp lệ.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng từ 1 đến 80 ký tự." } } });
        }

        if (request.Capacity <= 0)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa phòng phải là số nguyên dương.",
                new Dictionary<string, string[]> { { "capacity", new[] { "Sức chứa phải lớn hơn 0." } } });
        }

        bool duplicate = await _context.Rooms
            .AnyAsync(item => (item.NormalizedName == Flow2Name.Normalize(normalizedName) || item.Name == normalizedName));
        if (duplicate)
        {
            throw new BusinessRuleException(
                "ROOM_NAME_DUPLICATE",
                "Tên phòng đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng đã tồn tại trong hệ thống." } } });
        }

        DateTime now = DateTime.UtcNow;
        Room room = new Room
        {
            NormalizedName = Flow2Name.Normalize(normalizedName),
            Name = normalizedName,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Capacity = request.Capacity,
            RoomType = string.IsNullOrWhiteSpace(request.RoomType) ? null : request.RoomType.Trim(),
            IsActive = true,
            CreatedAt = now
        };

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.Rooms.AddAsync(room);
            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "ROOM_CREATED",
                "Room",
                room.Id.ToString(),
                "Tạo mới phòng tập " + normalizedName + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            return MapRoom(room);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException(
                "ROOM_NAME_DUPLICATE",
                "Tên phòng đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng đã tồn tại trong hệ thống." } } });
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<RoomViewModel> UpdateRoomAsync(
        string actorAccountId,
        int id,
        UpdateRoomAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        string normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Tên phòng không hợp lệ.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng từ 1 đến 80 ký tự." } } });
        }

        if (request.Capacity <= 0)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa phòng phải là số nguyên dương.",
                new Dictionary<string, string[]> { { "capacity", new[] { "Sức chứa phải lớn hơn 0." } } });
        }

        Room? room = await _context.Rooms.FirstOrDefaultAsync(item => item.Id == id);
        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng tập.");
        }

        bool duplicate = await _context.Rooms
            .AnyAsync(item => item.Id != id && (item.NormalizedName == Flow2Name.Normalize(normalizedName) || item.Name == normalizedName));
        if (duplicate)
        {
            throw new BusinessRuleException(
                "ROOM_NAME_DUPLICATE",
                "Tên phòng đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng đã tồn tại trong hệ thống." } } });
        }

        if (request.Capacity < room.Capacity)
        {
            DateTime localNow = DateTime.UtcNow.AddHours(7);
            DateTime today = localNow.Date;
            TimeSpan currentTime = localNow.TimeOfDay;

            bool hasOverCapacityScheduledSession = await _context.ClassSessions
                .AnyAsync(session => session.RoomId == id &&
                    session.Status == "SCHEDULED" &&
                    (session.SessionDate > today ||
                     (session.SessionDate == today && session.StartTime > currentTime)) &&
                    session.Capacity > request.Capacity);

            if (hasOverCapacityScheduledSession)
            {
                throw new BusinessRuleException(
                    "ROOM_CAPACITY_BELOW_SCHEDULE",
                    "Không thể hạ sức chứa phòng xuống dưới sức chứa của các buổi học đang lên lịch.",
                    new Dictionary<string, string[]> { { "capacity", new[] { "Có buổi học tương lai có sức chứa lớn hơn sức chứa phòng mới." } } });
            }
        }

        DateTime now = DateTime.UtcNow;
        room.NormalizedName = Flow2Name.Normalize(normalizedName);
        room.Name = normalizedName;
        room.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        room.Capacity = request.Capacity;
        room.RoomType = string.IsNullOrWhiteSpace(request.RoomType) ? null : request.RoomType.Trim();
        room.UpdatedAt = now;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "ROOM_UPDATED",
                "Room",
                room.Id.ToString(),
                "Cập nhật phòng tập " + normalizedName + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            return MapRoom(room);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException(
                "ROOM_NAME_DUPLICATE",
                "Tên phòng đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên phòng đã tồn tại trong hệ thống." } } });
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<RoomViewModel> UpdateRoomStatusAsync(
        string actorAccountId,
        int id,
        bool isActive)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        Room? room = await _context.Rooms.FirstOrDefaultAsync(item => item.Id == id);
        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng tập.");
        }

        room.IsActive = isActive;
        room.UpdatedAt = DateTime.UtcNow;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "ROOM_STATUS_CHANGED",
                "Room",
                room.Id.ToString(),
                "Cập nhật trạng thái phòng tập thành " + (isActive ? "Hoạt động" : "Ngừng hoạt động") + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            return MapRoom(room);
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    private static RoomViewModel MapRoom(Room room)
    {
        return new RoomViewModel
        {
            Id = room.Id,
            Name = room.Name,
            Location = room.Location,
            Capacity = room.Capacity,
            RoomType = room.RoomType,
            IsActive = room.IsActive,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sqlException && (sqlException.Number == 2627 || sqlException.Number == 2601))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }
}
