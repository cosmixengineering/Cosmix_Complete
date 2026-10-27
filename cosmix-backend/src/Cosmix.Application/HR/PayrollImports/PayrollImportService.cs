using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollImports;
using Cosmix.Domain.HR.PayrollSalary;

namespace Cosmix.Application.HR.PayrollImports;

public sealed class PayrollImportService(AttendanceOperation operation, IPayrollImportRepository repository,
    IPrivateFileStore files, EmployeeCompensationService compensation, IEmployeeCompensationRepository compensationRecords,
    PayrollSalaryService salary, IPayrollSalaryRepository salaryRecords, IAccountAccessReader access,
    IAccessManagementRepository authorization, TimeProvider clock)
{
    public static IReadOnlyList<PayrollImportTarget> Targets { get; } = [
        new("rowType", "Row type: Compensation or Component", true), new("employeeNumber", "Employee number", true),
        new("effectiveFrom", "Effective from (YYYY-MM-DD)", true), new("currency", "Currency", true),
        new("baseAmount", "Base salary or wage rate", false), new("grossAmount", "Gross salary or wage rate", false),
        new("wageBasis", "Wage basis: Monthly, Daily or Hourly", false), new("paymentMethod", "Payment method", false),
        new("componentCode", "Component code", false), new("componentKind", "Component kind", false),
        new("componentAmount", "Component amount", false), new("effectiveToExclusive", "Component end (exclusive)", false),
        new("prorateWithBase", "Prorate component: true or false", false)];

    public Task<PayrollImportInspection> InspectAsync(InspectPayrollImportRequest input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.FileName) || input.FileName.Length > 150 ||
            input.FileName != Path.GetFileName(input.FileName) || input.FileName.Any(char.IsControl) ||
            input.Base64 is null || input.Base64.Length > 5592410) throw AttendanceOperation.Invalid("salary_import_file_invalid");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input.Base64); }
        catch (FormatException) { throw AttendanceOperation.Invalid("salary_import_file_invalid"); }
        var hash = Hash(bytes);
        return operation.WriteAsync(PayrollImportPermissions.Submit, "attendance.payroll-imports.upload", null, key,
            new { input.FileName, hash }, input.Reason, context, async (actor, token) =>
            {
                await RequireSourcesAsync(actor, true, token);
                var sheets = PayrollSheetParser.Parse(input.FileName, bytes);
                if (await repository.SourceExistsAsync(hash, token)) throw AttendanceOperation.Conflict("salary_import_source_already_uploaded");
                var id = Guid.NewGuid();
                var evidence = await files.WriteAsync(bytes, Purpose(id), token);
                var source = new PayrollSalaryImport(id, input.FileName, hash, JsonSerializer.Serialize(evidence), actor.Identity.AccountId.ToString(), actor.Now);
                repository.Add(source); await repository.SaveChangesAsync(token);
                var result = Inspection(source, sheets);
                return new AttendanceChange<PayrollImportInspection>(id, null, result);
            }, ct, beforeReplay: (actor, token) => RequireSourcesAsync(actor, true, token),
            beforeCommit: (actor, token) => RequireSourcesAsync(actor, true, token),
            afterSave: (actor, token) => RequireSourcesAsync(actor, true, token));
    }

    public Task<PayrollImportInspection> InspectExistingAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(PayrollImportPermissions.Read, "attendance.payroll-imports.inspect", null, context, async (actor, token) =>
        {
            await RequireSourcesAsync(actor, false, token);
            var source = await FindAsync(id, false, token);
            return Inspection(source, await ReadSheetsAsync(source, token));
        }, ct, afterSave: (actor, token) => RequireSourcesAsync(actor, false, token));

    public Task<PayrollImportHistory> HistoryAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("salary_import_pagination");
        return operation.ReadAsync(PayrollImportPermissions.Read, "attendance.payroll-imports.history", null, context, async (actor, token) =>
        {
            await RequireSourcesAsync(actor, false, token); return await repository.HistoryAsync(page, pageSize, token);
        }, ct, afterSave: (actor, token) => RequireSourcesAsync(actor, false, token));
    }

    public Task<PayrollImportPreview> PreviewAsync(PreviewPayrollImportRequest input, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(PayrollImportPermissions.Read, "attendance.payroll-imports.preview", null, context, async (actor, token) =>
        {
            await RequireSourcesAsync(actor, false, token);
            return (await ProjectAsync(input, actor, token)).Preview;
        }, ct, afterSave: (actor, token) => RequireSourcesAsync(actor, false, token));

    public Task<PayrollImportApplied> ApplyAsync(ApplyPayrollImportRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null || input.ExpectedPreviewFingerprint is not { Length: 64 }) throw AttendanceOperation.Invalid("salary_import_fingerprint_required");
        return operation.WriteAsync(PayrollImportPermissions.Submit, "attendance.payroll-imports.submit", null, key,
            input, input.Reason, context, async (actor, token) =>
            {
                await RequireSourcesAsync(actor, true, token);
                var projection = await ProjectAsync(new(input.SourceId, input.SheetName, input.Mapping), actor, token);
                if (!projection.Preview.CanApply) throw AttendanceOperation.Conflict("salary_import_validation_failed");
                if (projection.Preview.PreviewFingerprint != input.ExpectedPreviewFingerprint) throw AttendanceOperation.Conflict("salary_import_preview_changed");
                var source = await FindAsync(input.SourceId, true, token);
                var links = new List<PayrollImportRequestLink>();
                foreach (var row in projection.Resolved)
                {
                    if (row.Compensation is not null)
                    {
                        var result = await compensation.StageInTransactionAsync(row.EmployeeId,
                            row.Compensation with { Reason = input.Reason }, new(actor.Identity, actor.RoleCode, actor.Now), token);
                        links.Add(new(row.RowNumber, row.EmployeeId, result.CompensationRequestId!.Value, EmployeeCompensationService.RequestType));
                    }
                    else
                    {
                        var result = await salary.StageComponentAsync(row.Component! with { Reason = input.Reason }, actor, source.Id, token);
                        links.Add(new(row.RowNumber, row.EmployeeId, result.ApprovalRequestId, "PayrollSalaryComponent"));
                    }
                }
                var response = new PayrollImportApplied(source.Id, "Submitted", links, source.SourceSha256);
                var proof = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new ImportProof(1, source.Id,
                    source.SourceSha256, input.SheetName, input.Mapping, projection.Preview.PreviewFingerprint, response)), Purpose(source.Id) + ":submission", token);
                source.Submit(input.ExpectedPreviewFingerprint, JsonSerializer.Serialize(proof), actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollImportApplied>(source.Id, null, response);
            }, ct, beforeReplay: (actor, token) => RequireSourcesAsync(actor, true, token),
            beforeCommit: (actor, token) => RequireSourcesAsync(actor, true, token),
            afterSave: (actor, token) => RequireSourcesAsync(actor, true, token));
    }

    public Task<PayrollImportApplied> SubmissionAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(PayrollImportPermissions.Read, "attendance.payroll-imports.submission", null, context, async (actor, token) =>
        {
            await RequireSourcesAsync(actor, false, token);
            var source = await FindAsync(id, false, token);
            if (source.AppliedPrivateReference is null) throw AttendanceOperation.Conflict("salary_import_not_submitted");
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(source.AppliedPrivateReference) ?? throw AttendanceOperation.Conflict("salary_import_evidence_invalid");
            var proof = JsonSerializer.Deserialize<ImportProof>(await files.ReadAsync(reference, Purpose(id) + ":submission", token));
            if (proof is null || proof.SchemaVersion != 1 || proof.SourceId != id || proof.SourceSha256 != source.SourceSha256 ||
                proof.Fingerprint != source.AppliedFingerprint) throw AttendanceOperation.Conflict("salary_import_evidence_invalid");
            return proof.Result;
        }, ct, afterSave: (actor, token) => RequireSourcesAsync(actor, false, token));

    private async Task<Projection> ProjectAsync(PreviewPayrollImportRequest input, AttendanceActor actor, CancellationToken ct)
    {
        if (input is null || input.Mapping is null || input.Mapping.Count > Targets.Count ||
            input.Mapping.Any(x => !Targets.Any(t => t.Key == x.Key) || string.IsNullOrWhiteSpace(x.Value)) ||
            Targets.Where(x => x.Required).Any(x => !input.Mapping.ContainsKey(x.Key)) ||
            input.Mapping.Values.Distinct(StringComparer.Ordinal).Count() != input.Mapping.Count)
            throw AttendanceOperation.Invalid("salary_import_mapping_invalid");
        var source = await FindAsync(input.SourceId, false, ct);
        if (source.SubmittedAt.HasValue) throw AttendanceOperation.Conflict("salary_import_already_submitted");
        var sheet = (await ReadSheetsAsync(source, ct)).SingleOrDefault(x => x.Name == input.SheetName)
            ?? throw AttendanceOperation.Invalid("salary_import_sheet_unknown");
        if (input.Mapping.Values.Any(x => !sheet.Columns.Contains(x))) throw AttendanceOperation.Invalid("salary_import_mapping_unknown_column");
        var rows = new List<PayrollImportRow>(); var resolved = new List<Resolved>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sheet.Rows.Count; index++)
        {
            var values = input.Mapping.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key,
                x => sheet.Rows[index][sheet.Columns.ToList().IndexOf(x.Value)], StringComparer.Ordinal);
            var issues = new List<PayrollImportIssue>(); Guid? employeeId = null; string? label = null;
            try
            {
                var number = PositiveLong(Value(values, "employeeNumber"), "employeeNumber");
                var employee = await repository.EmployeeAsync(number, ct) ?? throw Bad("salary_import_employee_unknown");
                employeeId = employee.Id; label = number.ToString(CultureInfo.InvariantCulture);
                var effective = Date(values, "effectiveFrom", source.FileName);
                var currency = Value(values, "currency");
                var rowType = Value(values, "rowType");
                if (rowType.Equals("Compensation", StringComparison.OrdinalIgnoreCase))
                {
                    if (!seen.Add($"salary:{employee.Id}")) throw Bad("salary_import_duplicate_employee_salary");
                    var window = await compensationRecords.WindowAsync(employee.Id, effective, ct);
                    var request = new RequestCompensationChange(employee.Version, window.Latest?.Id,
                        Money(values, "baseAmount"), Money(values, "grossAmount"), currency,
                        EnumValue<WageBasis>(values, "wageBasis"), EnumValue<PaymentMethod>(values, "paymentMethod"), effective, "Salary import preview");
                    await compensation.ValidateInTransactionAsync(employee.Id, request, new(actor.Identity, actor.RoleCode, actor.Now), ct);
                    resolved.Add(new(index + 2, employee.Id, employee.Version, request, null));
                }
                else if (rowType.Equals("Component", StringComparison.OrdinalIgnoreCase))
                {
                    var code = Value(values, "componentCode");
                    if (!seen.Add($"component:{employee.Id}:{code}")) throw Bad("salary_import_duplicate_employee_component");
                    var previous = (await salaryRecords.ComponentsAsync(employee.Id, ct)).Where(x =>
                        string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) && x.ApprovalRequest.State == ApprovalState.Approved)
                        .OrderByDescending(x => x.Revision).FirstOrDefault();
                    var request = new PayrollSalaryComponentInput(employee.Id, code, EnumValue<PayrollSalaryComponentKind>(values, "componentKind"),
                        Money(values, "componentAmount"), currency, effective,
                        values.TryGetValue("effectiveToExclusive", out var until) && until.Length > 0 ? Date(values, "effectiveToExclusive", source.FileName) : null,
                        Bool(values, "prorateWithBase"), previous?.Id, "Salary import preview");
                    await salary.ValidateComponentInTransactionAsync(request, actor, ct);
                    resolved.Add(new(index + 2, employee.Id, employee.Version, null, request));
                }
                else throw Bad("salary_import_row_type");
            }
            catch (AuthenticationFailure error) when (error.StatusCode is 400 or 404 or 409)
            { issues.Add(new(error.Message)); }
            rows.Add(new(index + 2, employeeId, label, values, issues));
        }
        var fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(new { source.Id, source.SourceSha256,
            input.SheetName, mapping = input.Mapping.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(), rows, resolved }));
        return new(new(source.Id, fingerprint, rows, rows.Count > 0 && rows.All(x => x.Issues.Count == 0),
            new(rows.Count, rows.Count(x => x.Issues.Count == 0), rows.Sum(x => x.Issues.Count))), resolved);
    }

    private async Task RequireSourcesAsync(AttendanceActor actor, bool write, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!await authorization.IsCurrentAdministratorAsync(actor.Identity, now, ct)) throw AttendanceOperation.Denied();
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, now, ct);
        var permissions = write
            ? new[] { EmployeeCompensationPermissions.Read, EmployeeCompensationPermissions.Request, PayrollSalaryPermissions.Read, PayrollSalaryPermissions.Request }
            : [EmployeeCompensationPermissions.Read, PayrollSalaryPermissions.Read];
        var checkedAt = clock.GetUtcNow();
        if (permissions.Any(permission => !AccessPermissionEvaluator.HasAllScope(actor.Identity,
            grants.Where(x => x.IsEffectiveAt(checkedAt)).ToArray(), permission))) throw AttendanceOperation.Denied();
        await operation.RequireAllPermissionAsync(actor, PayrollSalaryPermissions.Read, ct);
        if (write) await operation.RequireAllPermissionAsync(actor, PayrollSalaryPermissions.Request, ct);
    }
    private async Task<PayrollSalaryImport> FindAsync(Guid id, bool tracked, CancellationToken ct) =>
        await repository.FindAsync(id, tracked, ct) ?? throw AttendanceOperation.Missing("salary_import_not_found");
    private async Task<IReadOnlyList<PayrollSheet>> ReadSheetsAsync(PayrollSalaryImport source, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(source.SourcePrivateReference)
                ?? throw AttendanceOperation.Conflict("salary_import_source_unavailable");
            var bytes = await files.ReadAsync(reference, Purpose(source.Id), ct);
            if (Hash(bytes) != source.SourceSha256) throw AttendanceOperation.Conflict("salary_import_source_changed");
            return PayrollSheetParser.Parse(source.FileName, bytes);
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("salary_import_source_invalid"); }
    }
    private static PayrollImportInspection Inspection(PayrollSalaryImport source, IReadOnlyList<PayrollSheet> sheets) =>
        new(source.Id, source.SourceSha256, sheets.Select(x => new PayrollImportSheetInfo(x.Name, x.Columns, x.Rows.Count)).ToArray(), Targets);
    private static string Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw Bad("salary_import_required_" + key);
    private static decimal Money(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!decimal.TryParse(Value(values, key), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
            number < 0 || number > 9999999999999999.99m || decimal.Round(number, 2) != number) throw Bad("salary_import_money_" + key);
        return number;
    }
    private static long PositiveLong(string value, string key) => long.TryParse(value, NumberStyles.None,
        CultureInfo.InvariantCulture, out var number) && number > 0 ? number : throw Bad("salary_import_number_" + key);
    private static T EnumValue<T>(IReadOnlyDictionary<string, string> values, string key) where T : struct, Enum =>
        Enum.TryParse<T>(Value(values, key), true, out var result) && Enum.IsDefined(result) ? result : throw Bad("salary_import_enum_" + key);
    private static bool Bool(IReadOnlyDictionary<string, string> values, string key) =>
        bool.TryParse(Value(values, key), out var result) ? result : throw Bad("salary_import_boolean_" + key);
    private static DateOnly Date(IReadOnlyDictionary<string, string> values, string key, string fileName)
    {
        var value = Value(values, key);
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) return result;
        // Excel serial dates are accepted only for OpenXML sources and whole business dates.
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var serial) && serial is > 60 and < 2958466)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        throw Bad("salary_import_date_" + key);
    }
    private static AuthenticationFailure Bad(string code) => AttendanceOperation.Invalid(code);
    private static string Purpose(Guid id) => $"hr:payroll-import:{id:N}:source";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed record Resolved(int RowNumber, Guid EmployeeId, string EmployeeVersion,
        RequestCompensationChange? Compensation, PayrollSalaryComponentInput? Component);
    private sealed record Projection(PayrollImportPreview Preview, IReadOnlyList<Resolved> Resolved);
    private sealed record ImportProof(int SchemaVersion, Guid SourceId, string SourceSha256, string SheetName,
        IReadOnlyDictionary<string, string> Mapping, string Fingerprint, PayrollImportApplied Result);
}
