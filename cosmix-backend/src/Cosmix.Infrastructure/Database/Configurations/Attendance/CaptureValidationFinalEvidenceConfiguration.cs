using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class CaptureValidationFinalEvidenceConfiguration : IEntityTypeConfiguration<CaptureValidationFinalEvidence>
{
    public void Configure(EntityTypeBuilder<CaptureValidationFinalEvidence> b)
    {
        AttendanceMapping.Record(b, "CaptureValidationFinalEvidence",
            ("Action", "[Action] IN (1, 2)"), ("Outcome", "[CandidateOutcome] BETWEEN 1 AND 5 AND [FinalOutcome] BETWEEN 1 AND 5"),
            ("Clock", "[ReceivedAtUtc] <= [EvaluatedAtUtc] AND [EvaluatedAtUtc] <= [AssessedAtUtc]"),
            ("Offline", "[IsOffline] = 0 OR ([CandidateOutcome] = 1 AND [FinalOutcome] = 1)"),
            ("Policy", "([PolicyVersionId] IS NULL AND [PolicyRevision] IS NULL AND [SourceFingerprint] IS NULL) OR ([PolicyVersionId] IS NOT NULL AND [PolicyRevision] IS NOT NULL AND [SourceFingerprint] IS NOT NULL AND [PolicyRevision] > 0 AND LEN([SourceFingerprint]) = 64)"),
            ("Acceptance", "[FinalOutcome] NOT IN (2, 3) OR ([CandidateOutcome] IN (2, 3) AND [PolicyVersionId] IS NOT NULL)"),
            ("Json", "ISJSON([CodesJson]) = 1 AND ISJSON([RelatedRawPunchIdsJson]) = 1"));
        AttendanceMapping.Strings(b, ("RegisteredDeviceId", 200), ("CandidateReasonCode", 100), ("FinalReasonCode", 100), ("DeviceObservationCode", 100));
        b.Property(x => x.RequestSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.SourceFingerprint).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.CodesJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.RelatedRawPunchIdsJson).HasColumnType("nvarchar(max)").IsRequired();
        b.HasIndex(x => x.RawPunchEventId).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.ReceivedAtUtc });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RawPunchEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.RawPunchEventId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CaptureValidationPolicyVersion>().WithMany().HasForeignKey(x => new { x.PolicyVersionId, x.PolicyRevision, x.SourceFingerprint })
            .HasPrincipalKey(x => new { x.Id, x.Revision, x.SourceFingerprint }).OnDelete(DeleteBehavior.Restrict);
    }
}
