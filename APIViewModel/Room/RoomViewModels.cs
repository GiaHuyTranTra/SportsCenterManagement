using System;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Room;

public class RoomViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }

    public int Capacity { get; set; }

    public string? RoomType { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CreateRoomAPIViewModel
{
    [Required(ErrorMessage = "Tên phòng không được để trống.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "Tên phòng từ 1 đến 80 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120, ErrorMessage = "Vị trí tối đa 120 ký tự.")]
    public string? Location { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa phòng phải là số nguyên dương.")]
    public int Capacity { get; set; }

    [StringLength(50, ErrorMessage = "Loại phòng tối đa 50 ký tự.")]
    public string? RoomType { get; set; }
}

public class UpdateRoomAPIViewModel
{
    [Required(ErrorMessage = "Tên phòng không được để trống.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "Tên phòng từ 1 đến 80 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120, ErrorMessage = "Vị trí tối đa 120 ký tự.")]
    public string? Location { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa phòng phải là số nguyên dương.")]
    public int Capacity { get; set; }

    [StringLength(50, ErrorMessage = "Loại phòng tối đa 50 ký tự.")]
    public string? RoomType { get; set; }
}

public class UpdateRoomStatusAPIViewModel
{
    [Required(ErrorMessage = "Trạng thái không được để trống.")]
    public bool? IsActive { get; set; }
}
