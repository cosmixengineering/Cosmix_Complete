#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Common.Approvals;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=unexecuted;Database=unexecuted;Integrated Security=true;TrustServerCertificate=true").Options;
using var db = new CosmixDbContext(options);
var proxy = DispatchProxy.Create<IRosterRepository, ReadOnlyProxy>();
var repository = new WorkTreatmentRepository(db,proxy,null!,null!);
try { repository.RequireTransaction(); throw new Exception("Transaction guard did not reject an unbound caller"); }
catch (InvalidOperationException e) when (e.Message.Contains("authorized serializable")) { }
var method = typeof(WorkTreatmentRepository).GetMethod("SessionQuery",BindingFlags.NonPublic|BindingFlags.Instance)!;
var query = (IQueryable)method.Invoke(repository,[Guid.NewGuid(),new DateOnly(2026,10,13),new DateOnly(2026,10,14)])!;
var sql = query.ToQueryString();
if (!sql.Contains("NOT EXISTS") || !sql.Contains("SessionKey") || !sql.Contains("Revision") || !sql.Contains("EmployeeId") || !sql.Contains("AttendanceDate") || !sql.Contains("TOP"))
    throw new Exception("Source session query is missing employee/date/latest-revision/bounded guards");
var correctionReader = DispatchProxy.Create<ICorrectionReviewRepository, MissingCorrectionProxy>();
var alternativeReader = DispatchProxy.Create<IAttendanceAlternativeCorrectionApprovalReader, ReadOnlyProxy>();
var proofs = new WorkTreatmentCorrectionProofs(correctionReader,alternativeReader);
var employee = Guid.NewGuid(); var request = new ApprovalRequest("attendance.correction",Guid.NewGuid(),Guid.NewGuid().ToString(),"private-ref",1,DateTimeOffset.UtcNow);
var correction = new AttendanceCorrection(employee,new(2026,10,13),AttendanceCorrectionKind.ManualSession,request.Id,request.RequesterUserId,
    request.RequestedAt,"actual correction proposal","private-ref",requestedTimeUtc:new(2026,10,13,8,0,0,TimeSpan.Zero),requestedEndUtc:new(2026,10,13,10,0,0,TimeSpan.Zero));
var decision = request.Decide(ApprovalState.Approved,Guid.NewGuid().ToString(),"TEST-REVIEWER","review",request.RequestedAt,"private-ref");
var session = new WorkSession(Guid.NewGuid(),1,employee,new(2026,10,13),correction.RequestedTimeUtc!.Value,correction.RequestedEndUtc,
    Guid.NewGuid(),null,request.RequestedAt,null,correctionId:correction.Id);
if (await proofs.VerifyAsync(session,correction,request,decision,null!,default)) throw new Exception("Correction without real applied source proof was accepted");
Console.WriteLine("PASS: 3 repository transaction/query-translation/missing-applied-correction checks. SQL was translated only, never connected or executed.");
class ReadOnlyProxy : DispatchProxy {
    protected override object? Invoke(MethodInfo? method,object?[]? args) => throw new Exception("Unexpected query execution");
}
class MissingCorrectionProxy : DispatchProxy {
    protected override object? Invoke(MethodInfo? method,object?[]? args) => method!.Name == "FindAsync"
        ? Task.FromResult<CorrectionRecord?>(null) : throw new Exception("Unexpected correction query");
}
