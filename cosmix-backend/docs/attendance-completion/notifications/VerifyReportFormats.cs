#:project ../../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyReportFormats.packages.lock.json
using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Domain.Common.Reports;

// Offline synthetic documents only. An explicit output folder writes parser/render QA artifacts.
var checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
var from=new DateOnly(2026,10,1);
var employees=Enumerable.Range(1,25).Select(x=>Guid.Parse($"00000000-0000-0000-0000-{x:000000000000}")).ToArray();
var reportId=Guid.Parse("11111111-2222-3333-4444-555555555555");
var when=new DateTimeOffset(2026,10,9,12,34,56,TimeSpan.Zero);
FactualReportRow Row(Guid id,int date)=>new(id,from.AddDays(date),date%3==0?"no_accepted_session_evidence":"closed_session_evidence",date%3==0?0:28800+date, date%3==0?0:480,0,date%3==0?0:2,date%4,date%2==0,
 date%2==0?Guid.Parse($"99999999-0000-0000-0000-{date+1:000000000000}"):null,date%2==0?1:null,new string((char)('A'+date%6),64));
var rows=employees.SelectMany(id=>Enumerable.Range(0,31).Select(date=>Row(id,date))).ToArray();
var metadata=new AttendanceReportMetadata(reportId,when,from,from.AddDays(30),employees,"mixed");
var originalCulture=CultureInfo.CurrentCulture;
try
{
 CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("th-TH");
 Check(FactualAttendanceDocument.MetadataRows(metadata).Single(x=>x.Label=="Policy period").Value=="2026-10-01 to 2026-10-31","metadata policy period uses the original Gregorian business dates under non-Gregorian culture");
 var culturePdf=Encoding.ASCII.GetString(FactualAttendanceDocument.Generate(ReportFormat.Pdf,rows,metadata));
 Check(culturePdf.Contains("Filter / policy period: 2026-10-01 to 2026-10-31")&&culturePdf.Contains("Generated UTC: 2026-10-09 12:34:56"),"PDF date/time provenance stays invariant under Thai process culture");
}
finally{CultureInfo.CurrentCulture=originalCulture;}
foreach(var format in new[]{ReportFormat.Csv,ReportFormat.Xlsx,ReportFormat.Pdf})
{
 var bytes=FactualAttendanceDocument.Generate(format,rows,metadata);
 Check(bytes.Length>0 && bytes.Length<=2_000_000,$"{format} maximum bounds produce permitted bytes");
 if(format==ReportFormat.Csv)
 {
  var text=Encoding.UTF8.GetString(bytes);
  Check(text.Contains("Generated at UTC")&&text.Contains("2026-10-09T12:34:56")&&text.Contains("Employee filter 25")&&text.Contains("Policy period")&&text.Contains("Lock status at generation"),"CSV embeds metadata and all employee filters");
  Check(text.Contains("EmployeeId,BusinessDate")&&text.Contains(new string('F',64)),"CSV actual columns and full source fingerprint");
 }
 if(format==ReportFormat.Xlsx)
 {
  using var stream=new MemoryStream(bytes);using var zip=new ZipArchive(stream);
  var docs=zip.Entries.Where(x=>x.FullName.EndsWith(".xml")||x.FullName.EndsWith(".rels")).Select(x=>{using var file=x.Open();return XDocument.Load(file);}).ToArray();
  XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  Check(zip.GetEntry("[Content_Types].xml")!=null&&docs.Length==7,"real OOXML package and well-formed XML entries");
  Check(!docs.SelectMany(x=>x.Descendants()).Any(x=>x.Name.LocalName=="f"||x.Attributes().Any(a=>a.Name.LocalName=="TargetMode"&&a.Value=="External")),"XLSX contains no formulas or external relationships");
  var cells=docs.SelectMany(x=>x.Descendants(s+"c")).ToArray();
  Check(cells.All(x=>(string?)x.Attribute("t") is "inlineStr" or "n")&&cells.Any(x=>x.Value==employees.Last().ToString()),"XLSX values explicit inert strings/numbers and full final employee ID");
  Check(cells.Count(x=>(string?)x.Attribute("t")=="n")==rows.Length*5+rows.Count(x=>x.SummaryRevision.HasValue),"trusted factual quantities and available revisions use numeric cells");
  Check(docs.Sum(x=>x.Descendants(s+"row").Count())==rows.Length+1+36,"metadata and every attendance row included");
 }
 if(format==ReportFormat.Pdf)
 {
  var text=Encoding.ASCII.GetString(bytes);
  Check(text.StartsWith("%PDF-1.4")&&text.EndsWith("%%EOF\n")&&text.Contains("xref\n"),"real PDF header/xref/trailer");
  Check(text.Contains("/Count 76 ")&&text.Contains("Page 76 of 76"),"maximum PDF paginates all 25 employees and 775 rows");
  Check(text.Contains(employees.Last().ToString())&&text.Contains(new string('F',64))&&text.Contains("Policy calculations are unavailable"),"PDF includes full identifiers/fingerprints and factual policy limitation");
 }
 if(args.Length==1)
 {
  var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
  File.WriteAllBytes(Path.Combine(folder,"attendance-maximum"+FactualAttendanceDocument.Extension(format)),bytes);
 }
}
foreach(var altered in new[]{metadata with{EmployeeIds=employees.Take(24).ToArray()},metadata with{LockStatus="all_locked"},metadata with{ReportId=Guid.Empty}})
{
 try{FactualAttendanceDocument.Generate(ReportFormat.Xlsx,rows,altered);throw new Exception("Expected metadata rejection");}
 catch(ArgumentException){Check(true,"altered provenance rejected");}
}
try{FactualAttendanceDocument.Generate(ReportFormat.Pdf,rows[..^1],metadata);throw new Exception("Expected incomplete range rejection");}
catch(ArgumentException){Check(true,"partial employee/date range rejected");}
var injected=rows.ToArray();injected[0]=injected[0] with{FactualStatus="=HYPERLINK(\"bad\")"};
var injection=FactualAttendanceDocument.Generate(ReportFormat.Xlsx,injected,metadata);
using(var zip=new ZipArchive(new MemoryStream(injection)))
{
 using var entry=zip.GetEntry("xl/worksheets/sheet2.xml")!.Open();var xml=XDocument.Load(entry);
 Check(xml.Descendants().Any(x=>x.Name.LocalName=="t"&&x.Value=="=HYPERLINK(\"bad\")")&&!xml.Descendants().Any(x=>x.Name.LocalName=="f"),"formula-looking text remains an inline literal in real XLSX");
}
if(args.Length==1)File.WriteAllText(Path.Combine(Path.GetFullPath(args[0]),"expected.json"),JsonSerializer.Serialize(new{Metadata=metadata,Rows=rows}));
Console.WriteLine($"Attendance report formats: {checks} offline generation/provenance checks passed. Parser/render QA is separate; no SQL/private store used.");
