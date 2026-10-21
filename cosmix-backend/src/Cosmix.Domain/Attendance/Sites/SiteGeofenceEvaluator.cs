namespace Cosmix.Domain.Attendance.Sites;

/// <summary>Evaluates one reported location against a configured circle; makes no attendance decision.</summary>
public static class SiteGeofenceEvaluator
{
    private const double MeanEarthRadiusMeters = 6371008.8d;
    private const double DegreesToRadians = Math.PI / 180d;

    public static GeofenceEvaluation Evaluate(SiteGeofenceVersion? boundary, decimal latitude,
        decimal longitude, decimal accuracyMeters)
    {
        if (boundary is null) return new("not_configured", null, null);
        return EvaluateCircle(boundary.Id, boundary.Latitude, boundary.Longitude, boundary.RadiusMeters,
            boundary.MaximumAccuracyMeters, latitude, longitude, accuracyMeters);
    }

    public static GeofenceEvaluation EvaluateCircle(Guid versionId, decimal centerLatitude,
        decimal centerLongitude, decimal radiusMeters, decimal maximumAccuracyMeters,
        decimal latitude, decimal longitude, decimal accuracyMeters)
    {
        if (versionId == Guid.Empty || centerLatitude is < -90 or > 90 || centerLongitude is < -180 or > 180 ||
            radiusMeters <= 0 || maximumAccuracyMeters <= 0)
            return new("not_configured", null, null);
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || accuracyMeters <= 0)
            return new("invalid_location", null, versionId);
        if (accuracyMeters > maximumAccuracyMeters)
            return new("poor_accuracy", null, versionId);

        var latitudeDelta = (double)(latitude - centerLatitude) * DegreesToRadians;
        var longitudeDeltaDegrees = longitude - centerLongitude;
        if (longitudeDeltaDegrees > 180m) longitudeDeltaDegrees -= 360m;
        if (longitudeDeltaDegrees < -180m) longitudeDeltaDegrees += 360m;
        var longitudeDelta = (double)longitudeDeltaDegrees * DegreesToRadians;
        var latitudeHalfSine = Math.Sin(latitudeDelta / 2d);
        var longitudeHalfSine = Math.Sin(longitudeDelta / 2d);
        var haversine = latitudeHalfSine * latitudeHalfSine
            + Math.Cos((double)centerLatitude * DegreesToRadians)
            * Math.Cos((double)latitude * DegreesToRadians)
            * longitudeHalfSine * longitudeHalfSine;

        // Rounding near antipodal points must not put square roots outside their valid range.
        haversine = Math.Clamp(haversine, 0d, 1d);
        var distance = 2d * MeanEarthRadiusMeters
            * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
        return new(distance <= (double)radiusMeters ? "inside" : "outside", distance, versionId);
    }
}

public sealed record GeofenceEvaluation(string Status, double? DistanceMeters, Guid? GeofenceVersionId);
