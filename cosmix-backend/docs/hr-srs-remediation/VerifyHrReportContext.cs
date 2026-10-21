using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Cosmix.Application.HR.Reports;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Reports;

// Actual report/export code with synthetic repository pages; no SQL or host.
var failures = new List<string>(); var checks = 0;
void Check(bool value, string name) { checks++; if (!value) failures.Add(name); }
var employee = Guid.NewGuid();
var filter = new HrReportFilter(From: new(2026, 10, 1), To: new(2026, 10, 19),
    EmployeeId: employee, Status: "synthetic", Page: 2, PageSize: 50);
var repository = DispatchProxy.Create<IHrReportRepository, ContextRepository>();
var service = new HrReportService(repository, null!, TimeProvider.System);
async Task Invalid(Func<Task> action, string code)
{
    try { await action(); Check(false, code + " must fail before data access"); }
    catch (AuthenticationFailure ex) { Check(ex.StatusCode == 400 && ex.Code == code, code + " returns safe validation failure"); }
    catch (Exception ex) { Check(false, code + " unexpected " + ex.GetType().Name); }
}
await Invalid(() => service.GetReportDataAsync("unsupported", filter, new("", "synthetic"), default), "invalid_hr_report_type");
await Invalid(() => service.ExportReportAsync(HrReportTypes.DailyAttendance,
    new((ReportFormat)999, "Synthetic", filter), new("", "synthetic"), default), "invalid_hr_report_format");
await Invalid(() => service.ExportReportAsync(HrReportTypes.DailyAttendance,
    null!, new("", "synthetic"), default), "invalid_hr_report_format");
var scope = new HrReportSelection([employee], false);
var result = await (Task<object>)typeof(HrReportService).GetMethod("ReadDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(service, [HrReportTypes.DailyAttendance, filter, scope, CancellationToken.None])!;
var json = JsonSerializer.SerializeToElement(result);
var metadata = json.GetProperty("Metadata");
Check(metadata.TryGetProperty("Filter", out var saved) && saved.GetRawText() == JsonSerializer.Serialize(filter),
    "Preview retains exact effective filter including dates, employee and pagination");
Check(metadata.TryGetProperty("ReturnedCount", out var count) && count.GetInt32() == 0,
    "Empty requested page is explicitly zero rows, not total population");
Check(metadata.GetProperty("TotalCount").GetInt32() == 65, "Total matches retained independently of returned page");
foreach (var format in new[] { ReportFormat.Csv, ReportFormat.Xlsx })
{
    var download = await (Task<HrReportDownload>)typeof(HrReportService).GetMethod("ExportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(service, [HrReportTypes.DailyAttendance, new HrReportExportRequest(format, "Synthetic report context", filter), filter, scope, CancellationToken.None])!;
    if (format == ReportFormat.Csv)
    {
        var text = Encoding.UTF8.GetString(download.Content);
        Check(text.Contains(HrReportCsvExporter.Cell(JsonSerializer.Serialize(filter))), "CSV contains exact filter JSON");
        Check(text.Contains("\"Returned Records\",\"0\""), "CSV distinguishes returned count");
        Check(text.Contains("\"Total Records\",\"65\""), "CSV retains total count");
    }
    else
    {
        using var zip = new ZipArchive(new MemoryStream(download.Content));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = XDocument.Load(stream).Descendants(ns + "row").Skip(1).Select(row => row.Descendants(ns + "t").Select(t => t.Value).ToArray())
            .ToDictionary(row => row[0], row => row[1]);
        Check(rows.TryGetValue("Filters", out var value) && value == JsonSerializer.Serialize(filter), "XLSX retains exact filter JSON");
        Check(rows.TryGetValue("Returned Records", out value) && value == "0", "XLSX distinguishes returned count");
        Check(rows["Total Records"] == "65", "XLSX retains total count");
    }
}
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"HR report context: {checks - failures.Count}/{checks} passed; no SQL/HTTP acceptance.");
return failures.Count == 0 ? 0 : 1;

public class ContextRepository : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "GetDailyAttendanceAsync" => Task.FromResult<IReadOnlyList<DailyAttendanceReportRow>>(new HrReportRows<DailyAttendanceReportRow>([], 65)),
        _ => throw new InvalidOperationException(method.Name)
    };
}
