using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.WorkTreatments;

public static class WorkTreatmentPermissionDefs
{
    public const string Read = "attendance.work-treatments.read";
    public const string Submit = "attendance.work-treatments.submit";
    public const string Decide = "attendance.work-treatments.decide";
    public const string Self = "attendance.work-treatments.self";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Read, "Read retained work instructions and factual treatments within dated scopes", true, true, false),
        new(Submit, "Propose named work instructions and factual treatments within dated scopes", true, true, false),
        new(Decide, "Independently review work treatments under the retained typed policy authority", true, true, false),
        new(Self, "Read and dispute only the signed-in employee's factual work treatments", false, false, true)
    ]);
}
