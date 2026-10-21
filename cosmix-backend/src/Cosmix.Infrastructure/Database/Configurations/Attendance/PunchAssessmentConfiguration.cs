using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.FaceVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class PunchAssessmentConfiguration : IEntityTypeConfiguration<PunchAssessment>
{
    public void Configure(EntityTypeBuilder<PunchAssessment> b)
    {
        AttendanceMapping.Record(b, "PunchAssessments", ("Revision", "[Revision] > 0"), ("Outcome", "[Outcome] BETWEEN 1 AND 5"),
            ("Distance", "[GeofenceDistanceMeters] IS NULL OR [GeofenceDistanceMeters] >= 0"));
        AttendanceMapping.Strings(b, ("ReasonCode", 100), ("CalculationVersion", 100), ("DeviceRiskCode", 100));
        b.Property(x => x.GeofenceDistanceMeters).HasPrecision(10, 2);
        b.HasIndex(x => new { x.RawPunchEventId, x.Revision }).IsUnique();
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RawPunchEvent).WithMany().HasForeignKey(x => new { x.EmployeeId, x.RawPunchEventId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FaceVerificationAttempt>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.FaceVerificationAttemptId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
