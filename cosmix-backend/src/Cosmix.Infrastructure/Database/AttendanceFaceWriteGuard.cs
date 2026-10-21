using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

/// <summary>Protect mutable face lifecycle rows from generic tracked updates bypassing their domain methods.</summary>
internal static class AttendanceFaceWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<FaceEnrollment>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            string[] allowed = ["IsActive", "RevokedAtUtc", "RevokedBy", "RevocationReason", "RowVersion"];
            if (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)) ||
                !entry.Property(x => x.IsActive).OriginalValue || entry.Entity.IsActive ||
                entry.Property(x => x.RevokedAtUtc).OriginalValue.HasValue || entry.Entity.RevokedAtUtc is null ||
                entry.Entity.RevokedAtUtc < entry.Entity.EnrolledAtUtc || string.IsNullOrWhiteSpace(entry.Entity.RevokedBy) ||
                string.IsNullOrWhiteSpace(entry.Entity.RevocationReason))
                throw new InvalidOperationException("Face enrollment identity/template/history is immutable; only one audited revocation is permitted.");
        }
        foreach (var entry in db.ChangeTracker.Entries<FaceVerificationAttempt>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            string[] resultFields = ["Outcome", "LivenessPassed", "MatchPassed", "ResultReasonCode", "CompletedAtUtc"];
            string[] consumptionFields = ["ConsumedAtUtc", "ConsumedByPunchId"];
            if (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name != "RowVersion" &&
                !resultFields.Contains(x.Metadata.Name) && !consumptionFields.Contains(x.Metadata.Name)))
                throw new InvalidOperationException("Face challenge identity and provider context cannot be rewritten or deleted.");
            var value = entry.Entity;
            if (entry.Properties.Any(x => x.IsModified && resultFields.Contains(x.Metadata.Name)))
            {
                if (entry.Property(x => x.Outcome).OriginalValue != FaceVerificationOutcome.Pending ||
                    entry.Property(x => x.CompletedAtUtc).OriginalValue.HasValue || value.Outcome == FaceVerificationOutcome.Pending ||
                    !Enum.IsDefined(value.Outcome) || value.CompletedAtUtc is null || value.CompletedAtUtc < value.IssuedAtUtc ||
                    string.IsNullOrWhiteSpace(value.ResultReasonCode) || !value.LivenessPassed.HasValue || !value.MatchPassed.HasValue ||
                    (value.Outcome == FaceVerificationOutcome.Passed && (value.LivenessPassed != true || value.MatchPassed != true)) ||
                    (value.Outcome != FaceVerificationOutcome.Expired && value.CompletedAtUtc >= value.ExpiresAtUtc) ||
                    (value.Outcome == FaceVerificationOutcome.Expired && (value.CompletedAtUtc < value.ExpiresAtUtc ||
                        value.LivenessPassed != false || value.MatchPassed != false)))
                    throw new InvalidOperationException("Face results can only finalize one pending challenge within its approved validity rules.");
            }
            if (entry.Properties.Any(x => x.IsModified && consumptionFields.Contains(x.Metadata.Name)))
            {
                var punch = db.ChangeTracker.Entries<RawPunchEvent>().SingleOrDefault(x =>
                    x.State == EntityState.Added && x.Entity.Id == value.ConsumedByPunchId)?.Entity;
                if (entry.Property(x => x.ConsumedAtUtc).OriginalValue.HasValue || entry.Property(x => x.ConsumedByPunchId).OriginalValue.HasValue ||
                    value.Outcome != FaceVerificationOutcome.Passed || value.ConsumedAtUtc is null ||
                    value.ConsumedAtUtc < value.CompletedAtUtc || value.ConsumedAtUtc >= value.ExpiresAtUtc ||
                    punch is null || punch.Action != PunchAction.CheckIn || punch.EmployeeId != value.EmployeeId ||
                    punch.RegisteredDeviceId != value.RegisteredDeviceId || punch.IdempotencyKey != value.PunchIdempotencyKey ||
                    punch.ReceivedAtUtc < value.IssuedAtUtc || punch.ReceivedAtUtc >= value.ExpiresAtUtc ||
                    value.ConsumedAtUtc < punch.ReceivedAtUtc)
                    throw new InvalidOperationException("Face proof consumption must be single-use and saved atomically with its bound new Check In.");
            }
        }
    }
}
