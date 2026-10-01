using System;

namespace APIViewModel.Receptionist;

public class ReceptionistListItemAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? WorkShift { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
