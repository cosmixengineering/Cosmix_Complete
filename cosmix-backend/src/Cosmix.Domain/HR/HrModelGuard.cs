using System.Text.Json;

namespace Cosmix.Domain.HR;

internal static class HrModelGuard
{
    public static string Currency(string value)
    {
        var result = Common.ModelGuard.Code(value, 3);
        if (result.Length != 3 || result.Any(c => c < 'A' || c > 'Z')) throw new ArgumentException("Use a three-letter currency code.", nameof(value));
        return result;
    }
    public static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
    public static int NonNegative(int value, string name) => value >= 0 ? value : throw new ArgumentOutOfRangeException(name);
    public static decimal Money(decimal value, string name) => value >= 0 ? SignedMoney(value, name) : throw new ArgumentOutOfRangeException(name);
    public static decimal SignedMoney(decimal value, string name)
    {
        if (Math.Abs(value) > 9999999999999999.99m || decimal.Round(value, 2) != value) throw new ArgumentOutOfRangeException(name, "Money must fit decimal(18,2).");
        return value;
    }
    public static decimal Distance(decimal value, string name)
    {
        if (value < 0 || value > 999999999.999m || decimal.Round(value, 3) != value) throw new ArgumentOutOfRangeException(name, "Distance/volume must fit decimal(12,3).");
        return value;
    }
    public static decimal SignedDistance(decimal value, string name)
    {
        if (Math.Abs(value) > 999999999.999m || decimal.Round(value, 3) != value) throw new ArgumentOutOfRangeException(name, "Distance must fit decimal(12,3).");
        return value;
    }
    public static decimal Percentage(decimal value, string name)
    {
        if (value < 0 || value > 100 || decimal.Round(value, 4) != value) throw new ArgumentOutOfRangeException(name);
        return value;
    }
    public static string JsonObject(string value, int maximumLength, string name)
    {
        value = Common.ModelGuard.Required(value, maximumLength, name);
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("An object configuration is required.", name);
        return value;
    }
}
