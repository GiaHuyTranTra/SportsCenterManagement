using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipTerms;

public sealed class AcceptMembershipTermsAPIViewModel
{
    [Required]
    [MaxLength(80)]
    public string Version { get; set; } = null!;

    [Required]
    [StringLength(64, MinimumLength = 64)]
    public string ContentHash { get; set; } = null!;
}
