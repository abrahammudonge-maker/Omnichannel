using Omni.Domain.ValueObjects;
using Xunit;

namespace Omni.Tests;

public class WhatsAppNumberTests
{
    [Theory]
    [InlineData("0758218192", "254758218192")]
    [InlineData("758218192", "254758218192")]
    [InlineData("254758218192", "254758218192")]
    [InlineData("+254 758 218 192", "254758218192")]
    [InlineData("00254758218192", "254758218192")]
    public void Normalize_MakesEveryFormOfTheSameKenyanNumberIdentical(string input, string expected)
    {
        Assert.Equal(expected, WhatsAppNumber.Normalize(input));
    }

    [Theory]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("12345", null)]
    [InlineData("+1 (415) 555-0100", "14155550100")]
    [InlineData("1234567890123456", null)]
    [InlineData("0758218192", "254758218192")]
    public void NormalizeForSending_RejectsAnythingThatIsntAPhoneNumber(string? input, string? expected)
    {
        Assert.Equal(expected, WhatsAppNumber.NormalizeForSending(input));
    }

    [Fact]
    public void AreEqual_MatchesLocalAndInternationalForms()
    {
        Assert.True(WhatsAppNumber.AreEqual("0758218192", "254758218192"));
    }
}
