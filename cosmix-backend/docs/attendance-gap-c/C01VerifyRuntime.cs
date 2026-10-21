#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
var employee = Guid.NewGuid(); var department = Guid.NewGuid(); var category = Guid.NewGuid();
var date = new DateOnly(2026, 10, 20); var now = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
var family = AdvancedAttendanceFamily.EarlyCredit;
var policy = new Policy(AdvancedAttendanceRulesCodec.Code(family), "Explicit early instruction", PolicyCategory.EmployeeException);
var rules = new AdvancedAttendanceRules(1, family, ["OFFICE"],
    new("Approved-reference", AdvancedAttendanceRulesCodec.ReviewPermission(family), true, [AdvancedEvidenceKind.WrittenInstruction]),
    EarlyCredit: new(3600, true, true));
var json = AdvancedAttendanceRulesCodec.Validate(rules).CanonicalJson!;
var version = new PolicyVersion(policy.Id, 1, date.AddDays(-1), null, AdvancedAttendanceRulesCodec.Schema,
    json, "private-source-doc", null, "author", now, PolicyAuthority.DirectHr);
var assignment = new PolicyAssignment(version.Id, null, employee, null, null, null, date.AddDays(-1), null,
    "Explicit assignment", "private-instruction", null, "assigner", now, PolicyAuthority.DirectHr);
var member = new AttendancePolicyMembership(Guid.NewGuid(), employee, department, category, null, date.AddDays(-20), null);
var candidate = new AttendancePolicyCandidate(assignment, version, null, true);
AdvancedPolicyDatedSource Source(params AttendancePolicyCandidate[] candidates) => new(policy, new([member], candidates),
    [new(member.Id, category, "OFFICE")]);
AdvancedPolicyResolution Resolve(AdvancedPolicyDatedSource source, DateOnly? on = null, AdvancedAttendanceFamily? requested = null) =>
    AdvancedPolicyReader.Resolve(employee, requested ?? family, on ?? date, source);
var effective = Resolve(Source(candidate));
Check(effective.State == "configured" && AdvancedAttendanceRulesCodec.Validate(effective.Rules).CanonicalJson == json && effective.PolicyVersionId == version.Id, "exact typed family resolves");
Check(effective.AssignmentIds.SequenceEqual([assignment.Id]) && effective.Fingerprint.Length == 64, "assignment provenance and digest retained");
Check(Resolve(Source()).State == "missing", "missing rules have no default");
Check(Resolve(Source(candidate) with { Source = new([], [candidate]) }).State == "historical_membership_unavailable", "missing historical membership fails closed");
Check(Resolve(Source(candidate) with { Source = new([member, member with { Id = Guid.NewGuid() }], [candidate]) }).State == "historical_membership_unavailable", "ambiguous historical membership fails closed");
Check(Resolve(Source(candidate) with { StaffCategories = [] }).State == "historical_staff_category_unavailable", "missing category code fails closed");
Check(Resolve(Source(candidate) with { StaffCategories = [new(member.Id, category, "SITE")] }).State == "staff_category_not_applicable", "actual dated category must be permitted by rules");
Check(Resolve(Source(candidate), requested: (AdvancedAttendanceFamily)999).State == "invalid_family", "unknown family has explicit unavailable state");
Check(Resolve(Source(candidate), date.AddDays(-1)).State == "configured", "start date is inclusive");
var ended = new PolicyAssignmentEnd(assignment, date, "Future end", null, "ender", now, PolicyAuthority.DirectHr);
Check(Resolve(Source(candidate with { End = ended })).State == "missing", "valid end date is exclusive");
var untrustedEnd = Copy(ended, nameof(PolicyAssignmentEnd.Authority), PolicyAuthority.ApprovedWorkflow);
Check(Resolve(Source(candidate with { End = untrustedEnd, AuthorityValid = false })).State == "invalid_configuration", "untrusted ending cannot hide active assignment");
Check(Resolve(Source(candidate with { AuthorityValid = false })).State == "invalid_configuration", "explicit source authority failure is respected");
Check(Resolve(Source(candidate with { Version = Copy(version, nameof(PolicyVersion.Authority), PolicyAuthority.ApprovedWorkflow) })).State == "invalid_configuration", "authority flag cannot legitimize workflow publication");
Check(Resolve(Source(candidate with { Version = Copy(version, nameof(PolicyVersion.ApprovalRequestId), Guid.NewGuid()) })).State == "invalid_configuration", "generic approval source cannot establish direct authority");
Check(Resolve(Source(candidate with { Version = Copy(version, nameof(PolicyVersion.RulesSchemaVersion), "attendance.v3") })).State == "invalid_configuration", "ordinary schema cannot resolve as advanced");
Check(Resolve(Source(candidate with { Version = Copy(version, nameof(PolicyVersion.RulesJson), "{}") })).State == "invalid_configuration", "incomplete legacy JSON produces no guessed rules");
Check(Resolve(Source(candidate) with { Policy = Copy(policy, nameof(Policy.Category), PolicyCategory.Attendance) }).State == "invalid_configuration", "category one cannot resolve as advanced");
Check(Resolve(Source(candidate) with { Policy = Copy(policy, nameof(Policy.Code), "UNCONTROLLED") }).State == "invalid_configuration", "only controlled family code resolves");
var population = new PolicyAssignment(version.Id, null, null, department, null, null, date.AddDays(-1), null,
    "Population assignment", null, null, "assigner", now, PolicyAuthority.DirectHr);
var equivalent = Resolve(Source(candidate, new(population, version, null, true)));
Check(equivalent.State == "configured" && equivalent.AssignmentIds.Count == 2, "equivalent rules keep all contributing assignments");
Check(equivalent.Fingerprint == Resolve(Source(new(population, version, null, true), candidate)).Fingerprint, "source ordering does not change digest");
var alternativeJson = AdvancedAttendanceRulesCodec.Validate(rules with { EarlyCredit = new(1800, true, true) }).CanonicalJson!;
Check(Resolve(Source(candidate, new(population, Copy(version, nameof(PolicyVersion.RulesJson), alternativeJson), null, true))).State == "conflict", "competing population interpretations remain conflict");
Check(Resolve(Source(candidate with { Version = Copy(version, nameof(PolicyVersion.RulesJson), " " + json) })).Fingerprint != effective.Fingerprint,
    "exact legacy JSON bytes retained even when canonical rules match");
foreach (var (field, value) in new (string, object?)[] {
    (nameof(PolicyVersion.PolicyDocumentPrivateReference), "different-private-proof"),
    (nameof(PolicyVersion.PublishedByUserId), "different-publisher"), (nameof(PolicyVersion.PublishedAt), now.AddMinutes(1)),
    (nameof(PolicyVersion.ApprovalRequestId), Guid.NewGuid()), (nameof(PolicyVersion.Authority), PolicyAuthority.ApprovedWorkflow),
    (nameof(PolicyVersion.PolicyId), Guid.NewGuid()), (nameof(PolicyVersion.VersionNumber), 2)
}) Check(Resolve(Source(candidate with { Version = Copy(version, field, value) })).Fingerprint != effective.Fingerprint, "version provenance digest: " + field);
foreach (var (field, value) in new (string, object?)[] {
    (nameof(PolicyAssignment.InstructionPrivateReference), "different-instruction"), (nameof(PolicyAssignment.Reason), "Different reason"),
    (nameof(PolicyAssignment.RecordedByUserId), "different-assigner"), (nameof(PolicyAssignment.RecordedAt), now.AddMinutes(1)),
    (nameof(PolicyAssignment.ApprovalRequestId), Guid.NewGuid()), (nameof(PolicyAssignment.Authority), PolicyAuthority.ApprovedWorkflow)
}) Check(Resolve(Source(candidate with { Assignment = Copy(assignment, field, value) })).Fingerprint != effective.Fingerprint, "assignment provenance digest: " + field);
var laterEnd = new PolicyAssignmentEnd(assignment, date.AddDays(2), "Scheduled end", null, "ender", now, PolicyAuthority.DirectHr);
var endFingerprint = Resolve(Source(candidate with { End = laterEnd })).Fingerprint;
foreach (var (field, value) in new (string, object?)[] {
    (nameof(PolicyAssignmentEnd.Reason), "Changed ending reason"), (nameof(PolicyAssignmentEnd.RecordedByUserId), "different-ender"),
    (nameof(PolicyAssignmentEnd.RecordedAt), now.AddMinutes(1)), (nameof(PolicyAssignmentEnd.ApprovalRequestId), Guid.NewGuid()),
    (nameof(PolicyAssignmentEnd.Authority), PolicyAuthority.ApprovedWorkflow)
}) Check(Resolve(Source(candidate with { End = Copy(laterEnd, field, value) })).Fingerprint != endFingerprint, "ending provenance digest: " + field);
Check(!System.Text.Json.JsonSerializer.Serialize(effective).Contains("private-source-doc") &&
    !System.Text.Json.JsonSerializer.Serialize(effective).Contains("private-instruction"), "safe resolution contains hashes, no private storage references");
Console.WriteLine($"PASS: {checks} C01 reader runtime assertions; no SQL, host or provider.");

static T Copy<T>(T original, string property, object? value) where T : class
{
    var clone = (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(original, null)!;
    typeof(T).GetProperty(property)!.SetValue(clone, value); return clone;
}
