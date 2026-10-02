using System.ComponentModel.DataAnnotations;
using APIViewModel.Member;

namespace SportsCenterManagement.Tests;

public class CreateMemberAPIViewModelTests
{
    [Theory]
    [InlineData("0888614033", true)]
    [InlineData("08886140331", false)]
    [InlineData("888614033", false)]
    [InlineData("08886abc33", false)]
    public void Phone_RequiresTenDigitsStartingWithZero(string phone, bool expectedValid)
    {
        CreateMemberAPIViewModel model = new CreateMemberAPIViewModel
        {
            Email = "member@example.com",
            Password = "Test@12345",
            FullName = "Test Member",
            Phone = phone,
            MemberCode = "MEM001"
        };
        List<ValidationResult> validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            validationResults,
            validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }
}
