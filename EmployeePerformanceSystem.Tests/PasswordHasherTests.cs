using Xunit;

namespace EmployeePerformanceSystem.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashCanBeVerified()
    {
        var hash = PasswordHasher.Hash("12345678");
        Assert.True(PasswordHasher.Verify("12345678", hash));
    }

    [Fact]
    public void WrongPasswordFailsVerification()
    {
        var hash = PasswordHasher.Hash("12345678");
        Assert.False(PasswordHasher.Verify("87654321", hash));
    }

    [Fact]
    public void SamePasswordProducesDifferentSaltedHashes()
    {
        var first = PasswordHasher.Hash("12345678");
        var second = PasswordHasher.Hash("12345678");
        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("12345678", first));
        Assert.True(PasswordHasher.Verify("12345678", second));
    }
}
