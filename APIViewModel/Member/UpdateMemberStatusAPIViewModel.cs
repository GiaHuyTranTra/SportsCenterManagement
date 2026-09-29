using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Member;

public class UpdateMemberStatusAPIViewModel
{
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = null!;
}
