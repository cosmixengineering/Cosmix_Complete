namespace Cosmix.Domain.Authentication.Mobile;

internal static class LoginPhoneNumber
{
    // Application supplies parser-normalized international numbers; this is only a storage-shape guard.
    internal static string RequireCanonical(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (value.Length is < 3 or > 16 || value[0] != '+' || value[1] is < '1' or > '9' ||
            value.AsSpan(2).ContainsAnyExceptInRange('0', '9'))
            throw new ArgumentException("A canonical international login phone number is required.", name);
        return value;
    }
}
