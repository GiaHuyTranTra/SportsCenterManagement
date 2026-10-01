using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MemberSubscription;

public class CounterRegisterMemberAPIViewModel
{
    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required]
    [RegularExpression("^0[0-9]{9}$")]
    public string Phone { get; set; } = null!;

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? PackageId { get; set; }

    [Required]
    [Range(typeof(decimal), "1", "1000000000")]
    public decimal? ExpectedPrice { get; set; }

    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;
}
