using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Receptionist;

public class UpdateManagedReceptionistAPIViewModel
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? WorkShift { get; set; }
}
