#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyBoundary.packages.lock.json
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using System;
using System.Collections.Generic;
using Cosmix.Domain.Attendance.Sites;

// Standalone local behavior checks; no test project or external test packages.
// From the repository root, use a local-only restore source:
// dotnet run --file docs/site-geofences/VerifyBoundary.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Domain" --property:NuGetAudit=false
var ordinary = Boundary(0m, 0m, 100m);
var antimeridian = Boundary(0m, 179.9995m, 200m);
var northPole = Boundary(90m, 0m, 1m);
var southPole = Boundary(-90m, 0m, 1m);
var wholeEarth = Boundary(0m, 0m, 20015114.443m);
var quarterEarth = Boundary(0m, 0m, 10007557.221017962m);
var justUnderQuarterEarth = Boundary(0m, 0m, 10007557.220m);

var cases = new List<BoundaryCase>
{
    new("no configured version", null, 0m, 0m, 1m, "not_configured"),
    new("no version takes precedence over invalid coordinates", null, 91m, 181m, 0m, "not_configured"),
    new("same point", ordinary, 0m, 0m, 1m, "inside", 0d),
    new("inside circle", ordinary, 0.0005m, 0m, 1m, "inside", 55.5975401168d),
    new("outside circle", ordinary, 0.001m, 0m, 1m, "outside", 111.1950802335d),
    new("accuracy exactly at configured maximum", ordinary, 0m, 0m, 50m, "inside", 0d),
    new("accuracy exceeds configured maximum", ordinary, 0m, 0m, 50.000001m, "poor_accuracy"),
    new("poor accuracy outside circle", ordinary, 1m, 0m, 51m, "poor_accuracy"),
    new("accuracy does not enlarge the circle", ordinary, 0.001m, 0m, 50m, "outside", 111.1950802335d),
    new("accuracy does not shrink the circle", ordinary, 0.0005m, 0m, 50m, "inside", 55.5975401168d),
    new("zero accuracy", ordinary, 0m, 0m, 0m, "invalid_location"),
    new("negative accuracy", ordinary, 0m, 0m, -1m, "invalid_location"),
    new("latitude below range", ordinary, -90.000001m, 0m, 1m, "invalid_location"),
    new("latitude above range", ordinary, 90.000001m, 0m, 1m, "invalid_location"),
    new("longitude below range", ordinary, 0m, -180.000001m, 1m, "invalid_location"),
    new("longitude above range", ordinary, 0m, 180.000001m, 1m, "invalid_location"),
    new("invalid location precedes poor accuracy", ordinary, 91m, 0m, 51m, "invalid_location"),
    new("maximum decimal latitude", ordinary, decimal.MaxValue, 0m, 1m, "invalid_location"),
    new("minimum decimal longitude", ordinary, 0m, decimal.MinValue, 1m, "invalid_location"),
    new("maximum decimal accuracy", ordinary, 0m, 0m, decimal.MaxValue, "poor_accuracy"),
    new("cross antimeridian", antimeridian, 0m, -179.9995m, 1m, "inside", 111.1950802335d),
    new("same antimeridian", Boundary(0m, 180m, 1m), 0m, -180m, 1m, "inside", 0d),
    new("north pole across longitudes", northPole, 90m, 180m, 1m, "inside", 0d),
    new("south pole across longitudes", southPole, -90m, -180m, 1m, "inside", 0d),
    new("opposite poles", northPole, -90m, 0m, 1m, "outside", 20015114.442035924d),
    new("minimum latitude accepted", wholeEarth, -90m, 0m, 1m, "inside", 10007557.221017962d),
    new("maximum latitude accepted", wholeEarth, 90m, 0m, 1m, "inside", 10007557.221017962d),
    new("minimum longitude accepted", wholeEarth, 0m, -180m, 1m, "inside", 20015114.442035924d),
    new("maximum longitude accepted", wholeEarth, 0m, 180m, 1m, "inside", 20015114.442035924d),
    new("exact circle radius is inclusive", quarterEarth, 0m, 90m, 1m, "inside", 10007557.221017962d),
    new("radius just below distance is outside", justUnderQuarterEarth, 0m, 90m, 1m, "outside", 10007557.221017962d),
    new("near antipodal coordinates stay finite", wholeEarth, 0.000001m, 179.999999m, 1m, "inside", 20015114.442035924d, 0.2d)
};

var failures = 0;
foreach (var item in cases)
{
    var result = SiteGeofenceEvaluator.Evaluate(item.Boundary, item.Latitude, item.Longitude, item.Accuracy);
    var expectedVersion = item.Boundary?.Id;
    var matches = result.Status == item.ExpectedStatus
        && result.GeofenceVersionId == expectedVersion
        && DistanceMatches(result.DistanceMeters, item.ExpectedDistance, item.ToleranceMeters);

    if (matches)
    {
        Console.WriteLine($"PASS: {item.Name}");
        continue;
    }

    failures++;
    Console.Error.WriteLine($"FAIL: {item.Name}: expected status={item.ExpectedStatus}, "
        + $"distance={item.ExpectedDistance}, version={expectedVersion}; "
        + $"actual status={result.Status}, distance={result.DistanceMeters}, version={result.GeofenceVersionId}");
}

Console.WriteLine($"Boundary verification: {cases.Count - failures}/{cases.Count} passed.");
return failures == 0 ? 0 : 1;

static SiteGeofenceVersion Boundary(decimal latitude, decimal longitude, decimal radius) =>
    new(Guid.NewGuid(), 1, new DateOnly(2026, 10, 8), latitude, longitude, radius, 50m);

static bool DistanceMatches(double? actual, double? expected, double tolerance)
{
    if (expected is null) return actual is null;
    return actual.HasValue && double.IsFinite(actual.Value)
        && Math.Abs(actual.Value - expected.Value) <= tolerance;
}

internal sealed record BoundaryCase(string Name, SiteGeofenceVersion? Boundary, decimal Latitude,
    decimal Longitude, decimal Accuracy, string ExpectedStatus, double? ExpectedDistance = null,
    double ToleranceMeters = 0.000001d);
