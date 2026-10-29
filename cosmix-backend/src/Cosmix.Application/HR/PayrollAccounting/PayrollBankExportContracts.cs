namespace Cosmix.Application.HR.PayrollAccounting;

public sealed record PayrollBankColumn(string Header, string Field, string? Literal = null, bool Required = true);
public sealed record PayrollBankRoutingRule(string BankName, string BankCode);
public sealed record PayrollBankFormatRules(string Delimiter, bool IncludeHeader, bool Utf8Bom, string DateFormat, IReadOnlyList<PayrollBankColumn> Columns,
    string OutputFormat = "Csv", string WorksheetName = "Sheet1", string? TemplateSha256 = null, IReadOnlyList<PayrollBankRoutingRule>? BankCodes = null,
    IReadOnlyList<string>? BeneficiaryBankNames = null);
public sealed record CreatePayrollBankFormatRequest(string Name, int FormatVersion, string Currency, PayrollBankFormatRules Rules, string TemplateEvidence, string Reason);
public sealed record ApprovePayrollBankFormatRequest(int ExpectedVersion, string Evidence, string Reason);
public sealed record PayrollBankFormatResponse(Guid Id, string Name, int FormatVersion, string Currency, string State, int Version,
    PayrollBankFormatRules Rules, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, string CreatedByUserId, string? ApprovedByUserId);
public sealed record PayrollBankTemplateDefinition(string Code, string Name, string SourceTemplateSha256, PayrollBankFormatRules Rules);
public sealed record CreatePayrollBankExportRequest(Guid ObligationId, Guid FormatProfileId, DateOnly ValueDate,
    IReadOnlyList<Guid> PaymentIds, string ExpectedSourceFingerprint, string Reason);
public sealed record PayrollBankExportResponse(Guid Id, Guid ObligationId, Guid FormatProfileId, DateOnly ValueDate, int PaymentCount,
    decimal TotalAmount, string Sha256, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public sealed record PayrollBankExportDownload(byte[] Content, string FileName, string ContentType);
public sealed record RenewPayrollBankExportRequest(string ExpectedSha256, string Reason);
