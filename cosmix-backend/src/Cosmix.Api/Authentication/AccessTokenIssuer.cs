using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cosmix.Api.Authentication;

public sealed class AccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SigningKey) &&
        Encoding.UTF8.GetByteCount(options.Value.SigningKey) >= 32 &&
        !string.IsNullOrWhiteSpace(options.Value.Issuer) && !string.IsNullOrWhiteSpace(options.Value.Audience);

    public string Issue(UserAccount account, UserSession session, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (!IsConfigured || session.UserAccountId != account.Id || !session.IsActiveAt(now) ||
            expiresAt > session.ExpiresAt || expiresAt <= now || session.SecurityVersion != account.SecurityVersion)
            throw new AuthenticationFailure("authentication_unavailable", 503);
        var settings = options.Value;
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience,
        [
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("sid", session.Id.ToString()),
            new Claim("sv", account.SecurityVersion.ToString(CultureInfo.InvariantCulture)),
            new Claim("employee_id", account.EmployeeId.ToString()),
            new Claim("client", session.Client == SessionClient.Mobile ? "mobile" : "web"),
            new Claim("amr", session.Client == SessionClient.Web ? "pwd" : "sms"),
            new Claim("amr", session.SmsOtpChallengeId.HasValue ? "sms" : session.MfaChallengeId.HasValue ? "mfa" : "sms")
        ], now.UtcDateTime, expiresAt.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
