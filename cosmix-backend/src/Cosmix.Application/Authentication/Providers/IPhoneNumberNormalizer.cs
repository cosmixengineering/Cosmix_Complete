namespace Cosmix.Application.Authentication.Providers;

public interface IPhoneNumberNormalizer
{
    /// <summary>Returns canonical E.164. Requires a leading + and rejects invalid numbers/extensions.</summary>
    /// <exception cref="ArgumentException">The input is not an explicit international phone number.</exception>
    string Normalize(string input);
}
