using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeRecords;

namespace Cosmix.Application.HR.Employees;

public enum EmployeeRecordKind { Asset = 1, Sim = 2, Certification = 3 }
public enum EmployeeRecordAction { Issue = 1, Return = 2, Transfer = 3, Renew = 4, Suspend = 5, Revoke = 6, Replace = 7 }
public static class EmployeeRecordPermissions
{
    public const string AssetsRead = "hr.employees.assets.read";
    public const string AssetsRequest = "hr.employees.assets.request";
    public const string AssetsReview = "hr.employees.assets.review";
    public const string SimsRead = "hr.employees.sims.read";
    public const string SimsRequest = "hr.employees.sims.request";
    public const string SimsReview = "hr.employees.sims.review";
    public const string CertificationsRead = "hr.employees.certifications.read";
    public const string CertificationsRequest = "hr.employees.certifications.request";
    public const string CertificationsReview = "hr.employees.certifications.review";
    public static string Read(EmployeeRecordKind kind) => kind switch
    { EmployeeRecordKind.Asset => AssetsRead, EmployeeRecordKind.Sim => SimsRead, EmployeeRecordKind.Certification => CertificationsRead, _ => throw EmployeeOperation.Invalid("invalid_employee_record_kind") };
    public static string Request(EmployeeRecordKind kind) => kind switch
    { EmployeeRecordKind.Asset => AssetsRequest, EmployeeRecordKind.Sim => SimsRequest, EmployeeRecordKind.Certification => CertificationsRequest, _ => throw EmployeeOperation.Invalid("invalid_employee_record_kind") };
    public static string Review(EmployeeRecordKind kind) => kind switch
    { EmployeeRecordKind.Asset => AssetsReview, EmployeeRecordKind.Sim => SimsReview, EmployeeRecordKind.Certification => CertificationsReview, _ => throw EmployeeOperation.Invalid("invalid_employee_record_kind") };
}

// No authority is inferred from empty deployment configuration.
public sealed class EmployeeRecordPolicyOptions
{
    public const string SectionName = "EmployeeRecordPolicy";
    public string? ApprovedVersion { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? ApprovedReviewMode { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssetRecordInput(string? AssetReference, string Condition, Guid EvidenceDocumentId,
    Guid? ReceiverAccountId = null, Guid? TargetEmployeeId = null, string? ExpectedTargetVersion = null,
    Guid? TargetEvidenceDocumentId = null, string? TargetCondition = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SimRecordInput(Guid EvidenceDocumentId, string? SimReference = null, string? PhoneNumber = null,
    string? CreditTierCode = null, Guid? ReceiverAccountId = null, Guid? TargetEmployeeId = null,
    string? ExpectedTargetVersion = null, Guid? TargetEvidenceDocumentId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificationRecordInput(Guid EvidenceDocumentId, string? Type = null, string? Number = null,
    string? Issuer = null, DateOnly? IssuedOn = null, DateOnly? ExpiresOn = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestEmployeeRecord(string ExpectedEmployeeVersion, EmployeeRecordAction Action,
    Guid? ExpectedCurrentRecordId, DateTimeOffset EffectiveAt, string Reason,
    AssetRecordInput? Asset = null, SimRecordInput? Sim = null, CertificationRecordInput? Certification = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideEmployeeRecord(string ExpectedEmployeeVersion, string ExpectedRequestVersion,
    ApprovalState Decision, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelEmployeeRecord(string ExpectedRequestVersion);
public sealed record EmployeeRecordQuery(int Page = 1, int PageSize = 25, ApprovalState? State = null);
public sealed record EmployeeAssetRecord(Guid Id, Guid EmployeeId, string AssetReference, DateTimeOffset IssuedAt,
    DateTimeOffset? ReturnedAt, string IssueCondition, string? ReturnCondition, string? ReturnedToAccountId,
    string ActorId, string Version);
public sealed record EmployeeAssetHistoryRecord(Guid Id, Guid CustodyId, int Revision, EmployeeAssetHistoryAction Action,
    DateTimeOffset EffectiveAt, string Condition, Guid EvidenceDocumentId, Guid? ReceiverAccountId,
    Guid? TransferredFromCustodyId, Guid ApprovalRequestId, string ActorId, DateTimeOffset RecordedAt, string Reason);
public sealed record EmployeeSimRecord(Guid Id, Guid EmployeeId, int Revision, Guid? SupersedesId,
    Guid? TransferredFromId, string SimReference, string PhoneNumber, string CreditTierCode, CompanySimStatus Status,
    DateOnly IssuedOn, DateOnly EffectiveOn, DateOnly? ReturnedOn, Guid? IssueDocumentId, Guid? ReturnDocumentId,
    Guid? ReturnedToAccountId, string ActorId, DateTimeOffset RecordedAt, Guid? ApprovalRequestId, string? Reason);
public sealed record EmployeeCertificationRecord(Guid Id, Guid EmployeeId, int Revision, Guid? SupersedesId,
    string Type, string Number, string Issuer, DateOnly IssuedOn, DateOnly? ExpiresOn, DateOnly EffectiveOn,
    EmployeeCertificationStatus Status, bool IsExpired, Guid? EmployeeDocumentId, Guid? ActionDocumentId,
    string ActorId, DateTimeOffset RecordedAt, Guid? ApprovalRequestId, string? Reason);
public sealed record EmployeeRecordRequestSummary(Guid Id, Guid EmployeeId, ApprovalState State, string RequesterId,
    DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt, string Version);
public sealed record EmployeeRecordRequestDetail(EmployeeRecordRequestSummary Request, EmployeeRecordKind Kind,
    RequestEmployeeRecord Command, string ApprovedPolicyVersion, IReadOnlyList<EmployeeChangeDecision> Decisions);
public sealed record EmployeeRecordSnapshot(int SchemaVersion, Guid EmployeeId, EmployeeRecordKind Kind,
    RequestEmployeeRecord Command, string ApprovedPolicyVersion, string? ApprovedSimCreditPolicyVersion = null);

public interface IEmployeeRecordRepository
{
    Task<EmployeeAssetCustody?> AssetAsync(Guid id, CancellationToken ct);
    Task<EmployeeAssetCustody?> LatestAssetAsync(string reference, CancellationToken ct);
    Task<IReadOnlyList<EmployeeAssetHistory>> AssetHistoryAsync(Guid employeeId, Guid custodyId, CancellationToken ct);
    Task<PagedResult<EmployeeAssetCustody>> AssetsAsync(Guid employeeId, bool history, DateTimeOffset asOf, EmployeeRecordQuery query, CancellationToken ct);
    Task<EmployeeSimAssignment?> SimAsync(Guid id, CancellationToken ct);
    Task<bool> SimSuccessorAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EmployeeSimAssignment>> LatestSimsAsync(string reference, string phone, DateOnly asOf, CancellationToken ct);
    Task<PagedResult<EmployeeSimAssignment>> SimsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct);
    Task<EmployeeCertification?> CertificationAsync(Guid id, CancellationToken ct);
    Task<bool> CertificationSuccessorAsync(Guid id, CancellationToken ct);
    Task<bool> ActiveCertificationAsync(Guid employeeId, string type, string number, string issuer, CancellationToken ct);
    Task<PagedResult<EmployeeCertification>> CertificationsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct);
    Task<bool> HasPendingAsync(Guid employeeId, string requestType, CancellationToken ct);
    Task<ApprovalRequest?> RequestAsync(Guid employeeId, string requestType, Guid id, CancellationToken ct);
    Task<PagedResult<EmployeeRecordRequestSummary>> RequestsAsync(Guid? employeeId, string type, EmployeeRecordQuery query, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    void Add(EmployeeAssetCustody row);
    void Add(EmployeeAssetHistory row);
    void Add(EmployeeSimAssignment row);
    void Add(EmployeeCertification row);
    void Add(ApprovalRequest row);
    void Add(ApprovalDecision row);
}
