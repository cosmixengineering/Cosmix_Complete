#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
// Standalone offline harness. Run with the repository's existing Application project reference.
// No SQL, host, provider, real private storage, or test project.
using Cosmix.Application.Attendance.Zones;
using Cosmix.Domain.Attendance.Zones;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Invalid(Action action, string name) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception(name); }
var circle = new ZoneBoundary(ZoneBoundaryKind.Circle, 24.86m, 67.01m, 100m, null);
ZoneGeometry.Validate(circle);
Check(ZoneGeometry.Evaluate(circle, 24.86m, 67.01m, 5m, 20m).Position == ZonePosition.Inside, "circle centre inside");
Check(ZoneGeometry.Evaluate(circle, 24.862m, 67.01m, 5m, 20m).Position == ZonePosition.Outside, "circle outside");
Check(ZoneGeometry.Evaluate(circle, 24.86m, 67.01m, 101m, 20m).Position == ZonePosition.Uncertain, "accuracy crosses boundary");
var polygon = new ZoneBoundary(ZoneBoundaryKind.Polygon, null, null, null,
    "{\"type\":\"Polygon\",\"coordinates\":[[[67,24],[67.01,24],[67.01,24.01],[67,24.01],[67,24]]]}");
ZoneGeometry.Validate(polygon);
Check(ZoneGeometry.Evaluate(polygon,24.005m,67.005m,2,10).Position == ZonePosition.Inside, "polygon inside");
Check(ZoneGeometry.Evaluate(polygon,24.02m,67.005m,2,10).Position == ZonePosition.Outside, "polygon outside");
Check(ZoneGeometry.Evaluate(polygon,24m,67.005m,2,10).Position == ZonePosition.Uncertain, "polygon boundary uncertain");
Invalid(() => ZoneGeometry.Validate(circle with { RadiusMeters = 0 }), "positive radius");
Invalid(() => ZoneGeometry.Validate(circle with { Latitude = 91 }), "valid coordinates");
Invalid(() => ZoneGeometry.Validate(polygon with { PolygonGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[67,24],[67.01,24.01],[67.01,24],[67,24.01],[67,24]]]}" }), "self intersection");
Invalid(() => ZoneGeometry.Validate(polygon with { PolygonGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[179,24],[-179,24],[-179,25],[179,24]]]}" }), "date line geometry rejected");
var now = new DateTimeOffset(2026,10,9,10,0,0,TimeSpan.Zero);
var config = new ZoneMonitoringConfigurationVersion(Guid.NewGuid(),1,now,now.AddDays(2),20,30,60,30,7200,"Location monitoring runs during your accepted duty.","actor","explicit approved settings",now,"private-authority",120);
Check(config.MinimumIntervalSeconds == 60 && config.MaximumMonitoringSeconds == 7200, "explicit configuration");
Invalid(() => new ZoneMonitoringConfigurationVersion(config.SiteId,2,now,now.AddDays(2),20,30,0,30,7200,"Notice","actor","reason",now,"private"), "no hidden interval default");
Check(ZoneRules.Warning(ZoneEntryRule.Prohibited,ZonePosition.Inside,true), "red entry warning");
Check(ZoneRules.Warning(ZoneEntryRule.Permitted,ZonePosition.Outside,false), "operational exit warning");
Check(!ZoneRules.Warning(ZoneEntryRule.Permitted,ZonePosition.Inside,false), "inside operational safe");
Check(ZoneRules.Warning(ZoneEntryRule.Permitted,ZonePosition.Uncertain,true), "uncertainty does not claim safe");
Console.WriteLine($"PASS {checks} zone geometry/configuration checks (offline only)");
