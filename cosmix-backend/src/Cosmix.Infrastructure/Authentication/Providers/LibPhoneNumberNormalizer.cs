using Cosmix.Application.Authentication.Providers;
using PhoneNumbers;

namespace Cosmix.Infrastructure.Authentication.Providers;

public sealed class LibPhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private static readonly PhoneNumberUtil Parser = PhoneNumberUtil.GetInstance();

    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 64)
            throw InvalidPhone();

        var number = input.Trim();
        if (!number.StartsWith('+') || number.Length < 2 ||
            number[1..].Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '.')))
            throw InvalidPhone();

        try
        {
            // No default region: national numbers and region-dependent dialling prefixes are not inferred.
            var parsed = Parser.Parse(number, null);
            if (parsed.HasExtension || !Parser.IsValidNumber(parsed))
                throw InvalidPhone();
            return Parser.Format(parsed, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            throw InvalidPhone();
        }
    }

    private static ArgumentException InvalidPhone() =>
        new("A valid international phone number with a leading + and no extension is required.", "input");
}
