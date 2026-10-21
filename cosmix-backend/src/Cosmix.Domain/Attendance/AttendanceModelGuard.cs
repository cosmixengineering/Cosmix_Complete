using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance;

internal static class AttendanceModelGuard
{
    public static Guid? OptionalId(Guid? value, string name) => value.HasValue ? ModelGuard.Id(value.Value, name) : null;
    public static void Coordinates(decimal? latitude, decimal? longitude, decimal? accuracyMeters)
    {
        if (latitude.HasValue != longitude.HasValue || latitude is < -90 or > 90 || longitude is < -180 or > 180
            || accuracyMeters is < 0 or > 99999999.99m || (accuracyMeters.HasValue && !latitude.HasValue))
            throw new ArgumentException("Valid paired coordinates and non-negative accuracy are required.");
    }
    public static void Window(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from) throw new ArgumentException("The end must be after the start.");
    }
    public static int NonNegative(int value, string name) => value < 0 ? throw new ArgumentOutOfRangeException(name) : value;
    public static int Revision(int value) => value < 1 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
}
