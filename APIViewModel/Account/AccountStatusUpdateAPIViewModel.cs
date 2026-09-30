using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Account;

public class AccountStatusUpdateAPIViewModel
{
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = null!;
}
