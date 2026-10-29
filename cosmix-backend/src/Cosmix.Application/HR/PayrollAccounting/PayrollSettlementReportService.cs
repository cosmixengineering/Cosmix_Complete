using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.Reports;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed class PayrollSettlementReportService(SensitiveComplianceOperation operation, PayrollSettlementReader reader)
{
    public const string ReadPermission = "payroll.reports.read";
    public const string ExportPermission = "payroll.reports.export";

    public Task<PayrollSettlementBatchPage> ListAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync(ReadPermission, "payroll.settlement.list", "PayrollHandoffBatch", "collection", context,
            (_, token) => reader.BatchPageInTransactionAsync(page, pageSize, token), ct);

    public Task<PayrollSettlementReport> ReadAsync(Guid batchId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync(ReadPermission, "payroll.settlement.read", "PayrollHandoffBatch", batchId.ToString(), context,
            (_, token) => reader.ReportInTransactionAsync(batchId, token), ct);

    public Task<PayslipDocument> ExportAsync(Guid batchId, string format, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync(ExportPermission, "payroll.settlement.export", "PayrollHandoffBatch", batchId.ToString(), context,
            async (_, token) =>
            {
                if (format != "csv") throw AttendanceOperation.Invalid("payroll_settlement_format_invalid");
                var report = await reader.ReportInTransactionAsync(batchId, token);
                var bytes = ExportCsv(report);
                if (bytes.Length > 16 * 1024 * 1024) throw AttendanceOperation.Conflict("payroll_settlement_export_limit");
                return new PayslipDocument($"payroll-settlement-{report.PeriodStart:yyyy-MM}-{batchId:N}.csv", "text/csv; charset=utf-8",
                    Convert.ToBase64String(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
            }, ct);

    public static byte[] ExportCsv(PayrollSettlementReport report)
    {
        var text = new StringBuilder();
        void Row(params string[] cells) => text.AppendLine(string.Join(',', cells.Select(HrReportCsvExporter.Cell)));
        string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        Row("Report", "Payroll settlement"); Row("Batch reference", report.BatchReference);
        Row("Batch ID", report.BatchId.ToString()); Row("Period start", report.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("Period end exclusive", report.PeriodEndExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row("Currency", report.Currency); Row("Frozen source fingerprint", report.SourceFingerprint);
        Row("Generated UTC", report.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        Row("Employee ID", "Employee number", "Employee name", "Gross earnings", "Deductions", "Net entitlement", "Confirmed", "Posted", "Pending", "Failed", "Reversed", "Outstanding", "Remaining to initiate", "Payment state");
        foreach (var employee in report.Employees)
        {
            var p = employee.Payments;
            Row(employee.EmployeeId.ToString(), employee.EmployeeNumber.ToString(CultureInfo.InvariantCulture), employee.EmployeeName,
                Money(employee.GrossEarnings), Money(employee.Deductions), Money(employee.NetEntitlement), Money(p.Confirmed),
                Money(p.Posted), Money(p.Pending), Money(p.Failed), Money(p.Reversed), Money(p.Outstanding), Money(p.RemainingToInitiate), p.State);
        }
        Row("Totals", "", "", Money(report.GrossEarnings), Money(report.Deductions), Money(report.NetEntitlement), Money(report.ConfirmedPayments),
            Money(report.Employees.Sum(x => x.Payments.Posted)), Money(report.Employees.Sum(x => x.Payments.Pending)),
            Money(report.Employees.Sum(x => x.Payments.Failed)), Money(report.Employees.Sum(x => x.Payments.Reversed)), Money(report.OutstandingPayments),
            Money(report.Employees.Sum(x => x.Payments.RemainingToInitiate)), "");
        Row("Component employee ID", "Component", "Amount");
        foreach (var component in report.Components) Row(component.EmployeeId.ToString(), component.Component, Money(component.Amount));
        Row("Component totals", "Component", "Amount");
        foreach (var component in report.Components.GroupBy(x => x.Component).OrderBy(x => x.Key, StringComparer.Ordinal))
            Row("", component.Key, Money(component.Sum(x => x.Amount)));
        Row("Payment ledger net", Money(report.PaymentLedgerNet));
        Row("Payment ledger difference (ledger minus posted and confirmed)", Money(report.PaymentLedgerDifference));
        foreach (var issue in report.Issues) Row("Reconciliation issue", issue);
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }
}
