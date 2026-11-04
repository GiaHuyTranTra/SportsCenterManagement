using Services.Utils;

namespace SportsCenterManagement.Tests;

public class MembershipBenefitsTests
{
    [Theory]
    [InlineData("Legacy gym access", "Legacy gym access")]
    [InlineData("[\"Gym\"]", "Gym")]
    public void ReadsLegacyAndJsonBenefits(string input, string expected)
    {
        Assert.Equal(expected, Assert.Single(MembershipBenefits.Read(input)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    public void EmptyBenefitsReturnEmptyList(string? input) => Assert.Empty(MembershipBenefits.Read(input));
}
