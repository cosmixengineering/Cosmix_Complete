#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Common.Audit;
using Microsoft.EntityFrameworkCore;

// Explicit synthetic historical test setup only. Never a production import or a
// substitute for the future-dated, authenticated policy publication API.
const string actor = "TEST-ONLY-FIXTURE-DEMO-POLICY-20261028";
const string code = "DEMO-POLICY-20261028";
var employeeId = Guid.Parse("405f402c-7cd8-47ac-9f85-71c993b9d49d");
var siteId = Guid.Parse("bca2faf7-6401-41d4-9c46-1602cf2483e4");
var oldRosterId = Guid.Parse("8dd34c33-a329-45b1-aa7e-d8177f0be906");
var date = new DateOnly(2026,10,28); var end = date.AddDays(1);
var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cosmix", "LocalTesting", "DemoAttendancePolicy20261028");
if(args.Length!=1 || args[0] is not ("inspect" or "prepare" or "verify")) throw new Exception("Use inspect, prepare or verify");
await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(@"Server=np:\\.\pipe\LOCALDB#36E7B148\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15").Options);
await db.Database.OpenConnectionAsync();
await using(var cmd=db.Database.GetDbConnection().CreateCommand()){
 cmd.CommandText="SELECT @@SERVERNAME,DB_NAME(),SUSER_SNAME(owner_sid),family_guid,database_guid,d.state_desc FROM sys.databases d JOIN sys.database_recovery_status r ON d.database_id=r.database_id WHERE d.database_id=DB_ID()";
 await using var r=await cmd.ExecuteReaderAsync();Require(await r.ReadAsync(),"Identity missing");
 Require(r.GetString(0)==@"Software-PC1\LOCALDB#36E7B148" && r.GetString(1)=="Cosmix_Testing" && r.GetString(2)==@"COSMIXRND\Huma.Khan" && r.GetGuid(3)==Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303") && r.GetGuid(4)==Guid.Parse("631714B1-1CB2-43CA-9A04-4B2C04E3D1A7") && r.GetString(5)=="ONLINE","Wrong target");
}
var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
Require(migrations.Length==31 && migrations[^1]=="20261028114154_ProtectPayrollPaymentSettlementAndBankExports","Migration drift");
Require(await db.Employees.CountAsync()==33 && await db.Employees.AnyAsync(x=>x.Id==employeeId && x.EmployeeNumber==33 && x.FullLegalName=="TEST ONLY DEMO ATTENDANCE 20261028"),"Demo identity changed");
Require(await db.EmployeeAssignments.CountAsync(x=>x.SiteId==siteId)==1 && await db.EmployeeAssignments.AnyAsync(x=>x.EmployeeId==employeeId && x.SiteId==siteId && x.EffectiveFrom<=date && x.EffectiveTo==end),"Demo isolation changed");
Require(!await db.AttendancePeriodLocks.AnyAsync(x=>x.IsLocked && x.PeriodStart<end && x.PeriodEndExclusive>date),"Locked date");
var originalSummary=await db.DailyAttendanceSummaries.AsNoTracking().SingleAsync(x=>x.Id==Guid.Parse("35629bd1-7641-486e-9932-81df6662cedd"));
Require(originalSummary.WorkedMinutes==480 && originalSummary.PolicyVersionId==null && originalSummary.PayableMinutes==null,"Prior factual summary changed");
var rules = new AttendancePolicyRules(3,new(900,3600,9000),
 new(AttendancePaidTimeBasis.ScheduledWindowOverlap,AttendanceBreakTreatment.CountAllBreaks,AttendanceEarlyExitTreatment.ActualWorkedTime,14400,
  AttendanceNonScheduledDayTreatment.NoCredit,AttendanceNonScheduledDayTreatment.NoCredit,AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
 new(3,AttendanceLateResetPeriod.CalendarMonth,false,false,AttendanceDeductionSalaryBasis.BaseSalary,AttendanceSalaryDivisor.ActualCalendarMonthDays,null,1m,
  AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
  Enabled:false,DeductionKind:AttendanceLateDeductionKind.DayFraction,OverlapTreatment:AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
 new(AttendanceDeductionSalaryBasis.BaseSalary,AttendanceSalaryDivisor.ActualCalendarMonthDays,null,0m,0m,AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours,
  AttendanceOrdinaryPaidDayTreatment.NoDeduction,AttendanceOrdinaryPaidDayTreatment.NoDeduction,AttendanceOrdinaryPaidDayTreatment.NoDeduction,AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
var validated=AttendancePolicyRulesCodec.Validate(rules);Require(validated.IsValid,"Invalid explicit test rules");
if(args[0]=="inspect"){
 Require(!await db.Policies.AnyAsync(x=>x.Code==code),"Fixture already exists");
 Console.WriteLine("PASS read-only target/demo/31-migration checks; validated schema3; six new configuration rows plus one labelled fixture audit; no SQL writes.");return;
}
Directory.CreateDirectory(root);PrivateBootstrapSnapshotStore.RequirePrivatePath(root,true);
if(args[0]=="verify"){
 using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"fixture.json")));
 var versionId=manifest.RootElement.GetProperty("PolicyVersionId").GetGuid();
 var assignments=await db.PolicyAssignments.AsNoTracking().Where(x=>x.PolicyVersionId==versionId).ToArrayAsync();
 Require(assignments.Length==1 && assignments[0].EmployeeId==employeeId && assignments[0].DepartmentId==null && assignments[0].SiteId==null && assignments[0].StaffCategoryId==null,"Policy leaked beyond demo");
 var summaries=await db.DailyAttendanceSummaries.AsNoTracking().Where(x=>x.EmployeeId==employeeId && x.AttendanceDate==date).OrderBy(x=>x.Revision).ToArrayAsync();
 Require(summaries.Length>=1 && summaries.All(x=>x.PolicyVersionId==versionId && x.WorkedMinutes==480 && x.PayableMinutes==480 && x.LateMinutes==0 && x.OvertimeMinutes==0),"Calculated SQL summary mismatch");
 Require(await db.WorkSessions.CountAsync(x=>x.EmployeeId==employeeId && x.AttendanceDate==date)==1,"Duplicate session");
 Require(!await db.RawPunchEvents.AnyAsync(x=>x.EmployeeId==employeeId) && !await db.FaceVerificationAttempts.AnyAsync(x=>x.EmployeeId==employeeId),"Unexpected raw/biometric record");
 Console.WriteLine(JsonSerializer.Serialize(new{Employee=33,PolicyVersionId=versionId,CalculatedSummaries=summaries.Length,Worked=480,Payable=480,Late=0,Overtime=0,OriginalOctober27Preserved=true,EmployeeOnlyPolicy=true}));return;
}
var manifestPath=Path.Combine(root,"fixture.json");Require(!File.Exists(manifestPath)&&!File.Exists(manifestPath+".pending"),"Fixture already attempted");
await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
Require(!await db.Policies.AnyAsync(x=>x.Code==code) && !await db.HolidayCalendars.AnyAsync(x=>x.Code==code),"Existing fixture");
Require(!await db.WorkSessions.AnyAsync(x=>x.EmployeeId==employeeId && x.AttendanceDate==date) && !await db.DailyAttendanceSummaries.AnyAsync(x=>x.EmployeeId==employeeId && x.AttendanceDate==date),"Date already has attendance; no reseeding");
Require(!await db.PolicyAssignments.AnyAsync(x=>x.EmployeeId==employeeId),"Demo already has policy");
var roster=await db.ShiftPopulationAssignments.SingleAsync(x=>x.Id==oldRosterId);
Require(roster.SiteId==siteId && roster.EffectiveFrom==date.AddDays(-1) && roster.EffectiveTo==end && roster.HolidayCalendarId==null && !await db.ShiftPopulationAssignmentEnds.AnyAsync(x=>x.ShiftPopulationAssignmentId==oldRosterId),"Roster changed");
var now=DateTimeOffset.UtcNow;
var policy=new Policy(code,"TEST ONLY demo attendance policy; no active deductions",PolicyCategory.Attendance);policy.AdvanceRevision();
var version=new PolicyVersion(policy.Id,1,date,end,"attendance.v3",validated.CanonicalJson!,null,null,actor,now,PolicyAuthority.DirectHr);
var assignment=new PolicyAssignment(version.Id,null,employeeId,null,null,null,date,end,"TEST ONLY synthetic test policy, solely demo #33; not company policy or legal approval",null,null,actor,now,PolicyAuthority.DirectHr);
var calendar=new HolidayCalendar(code,"TEST ONLY no-holiday calendar for demo 28 October",date,end,"TEST ONLY synthetic calendar; no government notification or legal verification asserted");
var rosterEnd=ShiftPopulationAssignmentEnd.CreateDirect(roster,date,"TEST ONLY append dated roster replacement for demo calendar; preserve original Oct27",actor,now);
var replacement=ShiftPopulationAssignment.CreateDirect(roster.ShiftId,ShiftPopulationKind.Site,date,end,siteId:siteId,holidayCalendarId:calendar.Id);
var audit=new AuditEvent("development.fixture.demo-policy",actor,"TEST_FIXTURE","Employee",employeeId.ToString(),now,code,employeeId);
db.AddRange(policy,version,assignment,calendar,rosterEnd,replacement,audit);
await db.SaveChangesAsync();
var output=new{EmployeeId=employeeId,PolicyId=policy.Id,PolicyVersionId=version.Id,PolicyAssignmentId=assignment.Id,HolidayCalendarId=calendar.Id,RosterEndId=rosterEnd.Id,NewRosterId=replacement.Id,FixtureAuditId=audit.Id,Date=date,EndExclusive=end,Rules=JsonSerializer.Deserialize<JsonElement>(validated.CanonicalJson!),SyntheticFixtureOnly=true,NormalPublicationBackdatingAllowed=false};
await File.WriteAllTextAsync(manifestPath+".pending",JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
await tx.CommitAsync();File.Move(manifestPath+".pending",manifestPath);
Console.WriteLine(JsonSerializer.Serialize(new{Employee=33,NewConfigurationRows=6,FixtureAuditRows=1,PolicyId=policy.Id,PolicyVersionId=version.Id,Calendar=calendar.Id,Assignment=assignment.Id,NoExistingRowsRewritten=true}));
static void Require(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
