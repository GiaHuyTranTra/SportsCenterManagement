using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipPackage;

public class UpdateMembershipPackageStatusAPIViewModel
{
    [Required]
    public bool? IsActive { get; set; }
}
