using BCrypt.Net;

namespace Omni.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public void PasswordHashing_RoundTripsSuccessfully()
    {
        var password = "P@ssw0rd123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
    }
}
