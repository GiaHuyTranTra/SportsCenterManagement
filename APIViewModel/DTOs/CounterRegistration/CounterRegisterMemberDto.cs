using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.DTOs.CounterRegistration;

public class CounterRegisterMemberDto
{
	[Required(ErrorMessage = "Họ tên không được để trống")]
	public string FullName { get; set; } = string.Empty;

	[Required(ErrorMessage = "Email không được để trống")]
	[EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
	public string Email { get; set; } = string.Empty;

	[Required(ErrorMessage = "Số điện thoại không được để trống")]
	[RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và đủ 10 chữ số")]
	public string PhoneNumber { get; set; } = string.Empty;

	[Required(ErrorMessage = "Ngày sinh không được để trống")]
	public DateTime DateOfBirth { get; set; }

	[Required(ErrorMessage = "Phải chọn gói thành viên")]
	public int PackageId { get; set; }

	[Required(ErrorMessage = "Phải chọn phương thức thanh toán")]
	public string PaymentMethod { get; set; } = "CASH";
}
