using System.Text;
using Cosmix.Infrastructure.Authentication.Providers;
using Xunit;

namespace Cosmix.Tests;

public sealed class TotpProviderTests
{
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    public void Rfc6238_sha1_vectors_use_six_digits_and_each_counter_is_accepted_once(long timestamp, string code)
    {
        var provider = new TotpProvider(); var secret = Encoding.ASCII.GetBytes("12345678901234567890");
        var now = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var accepted = provider.Verify(secret, code, now, null);
        Assert.Equal(timestamp / 30, accepted);
        Assert.Null(provider.Verify(secret, code, now, accepted));
        Assert.Null(provider.Verify(secret, code, now.AddMinutes(2), null));
        Assert.Null(provider.Verify(secret, "" + code + "0", now, null));
    }
    [Fact]
    public void Enrollment_uses_unique_secrets_and_uri_labels_are_encoded()
    {
        var provider = new TotpProvider(); var a = provider.Generate("employee+test@example.invalid");
        var b = provider.Generate("employee+test@example.invalid");
        Assert.Equal(20, a.Secret.Length); Assert.NotEqual(a.Base32Secret, b.Base32Secret);
        Assert.StartsWith("otpauth://totp/", a.Uri); Assert.Contains("issuer=Cosmix", a.Uri);
        Assert.DoesNotContain("employee+test@", a.Uri);
    }
}
