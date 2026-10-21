namespace Cosmix.Domain.Common;

internal static class ModelGuard
{
    public static string Required(string value, int maxLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"{name} cannot exceed {maxLength} characters.", name);
        return value;
    }

    public static string Code(string value, int maxLength = 30) =>
        Required(value, maxLength, nameof(value)).ToUpperInvariant();

    public static string? Optional(string? value, int maxLength, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, name);

    public static Guid Id(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An identifier is required.", name);
        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();

    public static void EffectiveDates(DateOnly from, DateOnly? to)
    {
        if (to.HasValue && to.Value <= from)
            throw new ArgumentException("EffectiveTo must be after EffectiveFrom (exclusive end).");
    }
}
