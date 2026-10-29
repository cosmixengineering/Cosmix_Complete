using System.Globalization;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Reports;

namespace Cosmix.Application.HR.Payslips;

public sealed class PayslipService(AttendanceOperation operation, IPayrollFrozenBatchReader frozen,
    IPayslipRepository repository, TimeProvider clock, PayrollSettlementReader? settlements = null)
{
    public Task<PayslipDocument> ManagementDocumentAsync(Guid batchId, Guid employeeId, string format,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(PayrollHandoffPermissions.Read, "attendance.payroll-handoff.payslip", employeeId, context,
            (_, token) => DocumentInTransactionAsync(batchId, employeeId, format, token), ct);

    // Called only inside EmployeeSelfService's current session / financial permission audit boundary.
    internal async Task<PayslipPage> OwnPageInTransactionAsync(Guid employeeId, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 50) throw AttendanceOperation.Invalid("invalid_payslip_pagination");
        var ids = await repository.OwnBatchIdsAsync(employeeId, page, pageSize, ct);
        var rows = new List<PayslipSummary>();
        foreach (var id in ids.Take(pageSize)) rows.Add((await ReadAsync(id, employeeId, ct)).Summary);
        return new(rows, page, pageSize, ids.Count > pageSize);
    }

    internal async Task<PayslipDocument> DocumentInTransactionAsync(Guid batchId, Guid employeeId, string format, CancellationToken ct)
    {
        if (format is not ("pdf" or "xlsx")) throw AttendanceOperation.Invalid("payslip_format_invalid");
        var source = await ReadAsync(batchId, employeeId, ct);
        var employee = await repository.EmployeeAsync(employeeId, ct) ?? throw AttendanceOperation.Missing("payslip_employee_not_found");
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Payroll reference", source.Summary.BatchReference, "" },
            new[] { "Employee number", employee.Number.ToString(CultureInfo.InvariantCulture), "" },
            new[] { "Employee name", employee.Name, "" },
            new[] { "Period start", source.Summary.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "" },
            new[] { "Period end (exclusive)", source.Summary.PeriodEndExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "" },
            new[] { "Currency", source.Summary.Currency, "" },
            new[] { "Payroll status", source.Summary.PayrollState, "" },
            new[] { "Payment status", source.Summary.PaymentState, "" }
        };
        foreach (var line in source.Frozen.Lines.Where(x => x.EmployeeId == employeeId).OrderBy(x => x.Component).ThenBy(x => x.Id))
            rows.Add(new[] { line.Component, Money(line.Amount), line.SourceId.ToString() });
        rows.Add(new[] { "Gross earnings", Money(source.Summary.GrossAmount), "" });
        rows.Add(new[] { "Total deductions", Money(source.Summary.DeductionAmount), "" });
        rows.Add(new[] { "Net pay", Money(source.Summary.NetAmount), "" });
        rows.Add(new[] { "Frozen source fingerprint", source.Frozen.Fingerprint, "" });
        var metadata = new HrReportMetadata("employee-payslip", "Employee payslip", clock.GetUtcNow(), "Asia/Karachi", rows.Count,
            "approved_locked_salary_v2", new(EmployeeId: employeeId), rows.Count);
        string[] headers = ["Description", "Amount or value", "Source record"];
        var bytes = format == "pdf" ? HrReportPdfExporter.Generate(metadata, headers, rows,
            "Cosmix | Employee payslip", "Approved salary breakdown. Payment status is reported separately.") : HrReportXlsxExporter.Generate(metadata, headers, rows);
        if (bytes.Length > 1024 * 1024) throw AttendanceOperation.Conflict("payslip_document_limit");
        var name = $"payslip-{source.Summary.PeriodStart:yyyy-MM}-{employee.Number}.{format}";
        return new(name, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Convert.ToBase64String(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private async Task<Source> ReadAsync(Guid batchId, Guid employeeId, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || !await repository.ContainsEmployeeAsync(batchId, employeeId, ct))
            throw AttendanceOperation.Missing("payslip_not_found");
        var batch = await frozen.ReadAsync(batchId, ct);
        var employee = batch.Employees.SingleOrDefault(x => x.EmployeeId == employeeId)
            ?? throw AttendanceOperation.Conflict("payslip_employee_source_missing");
        var ownLines = batch.Lines.Where(x => x.EmployeeId == employeeId).ToArray();
        if (employee.GrossEarnings != ownLines.Where(x => x.Amount > 0).Sum(x => x.Amount) ||
            employee.TotalDeductions != -ownLines.Where(x => x.Amount < 0).Sum(x => x.Amount) ||
            employee.NetPay != ownLines.Sum(x => x.Amount) || employee.NetPay < 0)
            throw AttendanceOperation.Conflict("payslip_totals_invalid");
        var identity = await repository.BatchAsync(batchId, ct) ?? throw AttendanceOperation.Missing("payslip_not_found");
        var settlement = settlements is null ? null : await settlements.OwnPeriodInTransactionAsync(batchId, employeeId, 1, 1, ct);
        var paid = settlement?.ConfirmedAmount ?? await repository.ConfirmedPaymentsAsync(batchId, employeeId, ct);
        if (settlements is null && paid > 0) throw AttendanceOperation.Conflict("payslip_payment_evidence_validator_required");
        if (paid < 0 || paid > employee.NetPay) throw AttendanceOperation.Conflict("payslip_payment_total_invalid");
        if (settlement is not null && (settlement.NetEntitlement != employee.NetPay || settlement.Currency != batch.Currency ||
            settlement.PeriodStart != batch.PeriodStart || settlement.PeriodEndExclusive != batch.PeriodEndExclusive))
            throw AttendanceOperation.Conflict("payslip_payment_source_mismatch");
        var payment = settlement?.PaymentState ?? (employee.NetPay == 0 ? "NoPaymentDue" : "Unpaid");
        return new(new(batchId, identity.Reference, batch.PeriodStart, batch.PeriodEndExclusive, batch.Currency,
            employee.GrossEarnings, employee.TotalDeductions, employee.NetPay, "ManagementApproved", payment, identity.Version), batch);
    }
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private sealed record Source(PayslipSummary Summary, PayrollFrozenBatch Frozen);
}
