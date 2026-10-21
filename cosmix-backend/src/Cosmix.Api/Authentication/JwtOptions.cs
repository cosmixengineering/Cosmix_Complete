namespace Cosmix.Api.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Cosmix.Backend";
    public string Audience { get; set; } = "Cosmix.Clients";
    public string SigningKey { get; set; } = string.Empty;
}
