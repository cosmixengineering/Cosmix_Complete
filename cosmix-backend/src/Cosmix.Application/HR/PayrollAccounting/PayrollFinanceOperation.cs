using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Retained results use the existing private store and receipt table within one access transaction.</summary>
public sealed class PayrollFinanceOperation(SensitiveComplianceOperation operation, ISchedulingJournalRepository journal,
    IPrivateFileStore files, TimeProvider clock)
{
    public Task<T> WriteAsync<T>(string permission, string action, string target, string key, object payload,
        string reason, AuthRequestContext context, Func<AuthenticatedAccount, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant)) throw AttendanceOperation.Invalid("invalid_idempotency_key");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl)) throw AttendanceOperation.Invalid("invalid_financial_reason");
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { permission, target, payload, reason })));
        return operation.ExecuteAsync(permission, action, "PayrollFinance", target, context, async (actor, token) =>
        {
            var old = await journal.FindReceiptAsync(actor.AccountId.ToString(), action, key, token);
            var purpose = $"payroll-finance/{actor.AccountId:D}/{action}";
            if (old is not null)
            {
                if (old.RequestSha256 != digest) throw AttendanceOperation.Conflict("idempotency_key_reused");
                if (old.ResultCode != "payroll-finance-v1" || old.ProtectedResultReference is null || old.ExpiresAt <= clock.GetUtcNow())
                    throw AttendanceOperation.Conflict("idempotency_result_unavailable");
                var reference = JsonSerializer.Deserialize<PrivateFileReference>(old.ProtectedResultReference) ?? throw AttendanceOperation.Conflict("invalid_financial_receipt");
                return JsonSerializer.Deserialize<T>(await files.ReadAsync(reference, purpose, token)) ?? throw AttendanceOperation.Conflict("invalid_financial_receipt");
            }
            var result = await work(actor, token);
            var resultFile = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(result), purpose, token);
            var now = clock.GetUtcNow();
            var receipt = new IdempotencyReceipt(actor.AccountId.ToString(), action, key, digest, now, now.AddDays(30));
            receipt.Complete("payroll-finance-v1", JsonSerializer.Serialize(resultFile), now);
            journal.AddReceipt(receipt); await journal.SaveChangesAsync(token);
            return result;
        }, ct);
    }
}
