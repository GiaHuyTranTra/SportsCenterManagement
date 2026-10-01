namespace APIViewModel.Receptionist;

public class ReceptionistListItemAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public bool IsLocked { get; set; }

    public string FullName { get; set; } = null!;

    public string? WorkShift { get; set; }

    public DateTime CreatedAt { get; set; }
}
