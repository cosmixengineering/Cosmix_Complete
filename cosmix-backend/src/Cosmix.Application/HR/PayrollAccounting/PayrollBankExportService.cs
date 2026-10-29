using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Produces an audited file only. No bank submission, transfer or confirmation is inferred.</summary>
public sealed class PayrollBankExportService(IPayrollAccountingRepository repository, IPayrollFrozenBatchReader frozenReader,
    IEmployeeBankRepository banks, IEmployeeUpdateRepository updates, IPrivateFileStore files,
    PayrollFinanceOperation writes, SensitiveComplianceOperation reads, TimeProvider clock, IEmployeeHiringRepository? employees = null,
    PayrollAccountingService? paymentEvidence = null)
{
    public const string Permission = "payroll.accounting.bank.export";
    public Task<IReadOnlyList<PayrollBankTemplateDefinition>> TemplatesAsync(AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync(Permission, "payroll.bank.templates.read", "PayrollBankFormat", "templates", context,
            (_, _) => Task.FromResult(PayrollBankTemplates.Definitions()), ct);
    public Task<IReadOnlyList<PayrollBankFormatResponse>> ListFormatsAsync(AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync<IReadOnlyList<PayrollBankFormatResponse>>(Permission, "payroll.bank.formats.read", "PayrollBankFormat", "collection", context,
            async (_, token) => (await repository.ListBankFormatsAsync(token)).Select(Map).ToArray(), ct);

    public Task<PayrollBankFormatResponse> CreateFormatAsync(CreatePayrollBankFormatRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(PayrollFinanceSetupService.Configure, "payroll.bank.format.create", "formats", key, request, request.Reason, context, async (actor, token) =>
        {
            ValidateFormat(request.Rules); PayrollAccountingService.ValidateEvidence(request.Reason, request.TemplateEvidence);
            var profile = PayrollFinanceErrors.Run(() => new PayrollBankFormatProfile(request.Name, request.FormatVersion, request.Currency,
                JsonSerializer.Serialize(request.Rules), actor.AccountId.ToString(), clock.GetUtcNow()));
            var proof = new FormatProof(1, profile.Id, profile.Name, profile.FormatVersion, profile.Currency, profile.RulesJson,
                profile.CreatedByUserId, profile.CreatedAt, request.Reason.Trim(), request.TemplateEvidence.Trim());
            profile.BindTemplate(await WriteProofAsync(proof, FormatPurpose(profile.Id, "template"), token));
            await repository.AddBankFormatAsync(profile, token); await repository.SaveChangesAsync(token); return Map(profile);
        }, ct);

    public Task<PayrollBankFormatResponse> ApproveFormatAsync(Guid id, ApprovePayrollBankFormatRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(PayrollFinanceSetupService.Approve, "payroll.bank.format.approve", id.ToString(), key, request, request.Reason, context, async (actor, token) =>
        {
            var profile = await repository.GetBankFormatAsync(id, token) ?? throw Conflict("Bank format not found.");
            if (request.ExpectedVersion != profile.Version || profile.State != PayrollFinanceApprovalState.Draft ||
                string.Equals(profile.CreatedByUserId, actor.AccountId.ToString(), StringComparison.OrdinalIgnoreCase)) throw Conflict("Independent current draft format review is required.");
            PayrollAccountingService.ValidateEvidence(request.Reason, request.Evidence);
            await ValidateFormatProofAsync(profile, false, token);
            var at = clock.GetUtcNow();
            var proof = new FormatProof(1, profile.Id, profile.Name, profile.FormatVersion, profile.Currency, profile.RulesJson,
                actor.AccountId.ToString(), at, request.Reason.Trim(), request.Evidence.Trim());
            profile.Approve(actor.AccountId.ToString(), at, await WriteProofAsync(proof, FormatPurpose(id, "approval"), token));
            await repository.SaveChangesAsync(token); return Map(profile);
        }, ct);

    public Task<PayrollBankExportResponse> CreateExportAsync(CreatePayrollBankExportRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Permission, "payroll.bank.export.create", request.ObligationId.ToString(), key, request, request.Reason, context,
            (actor, token) => CreateExportCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<IReadOnlyList<PayrollBankExportResponse>> ListExportsAsync(Guid obligationId, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync<IReadOnlyList<PayrollBankExportResponse>>(Permission, "payroll.bank.exports.read", "PayrollObligation", obligationId.ToString(), context,
            async (_, token) =>
            {
                var exports = await repository.ListBankExportsAsync(obligationId, token);
                var results = new List<PayrollBankExportResponse>();
                foreach (var export in exports) results.Add(Map(export) with { ExpiresAt = await EffectiveExpiryAsync(export, token) });
                return results;
            }, ct);

    public Task<PayrollBankExportResponse> RenewAsync(Guid id, RenewPayrollBankExportRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Permission, "payroll.bank.export.renew", id.ToString(), key, request, request.Reason, context,
            (actor, token) => RenewCoreAsync(id, request, actor.AccountId.ToString(), token), ct);

    internal async Task<PayrollBankExportResponse> RenewCoreAsync(Guid id, RenewPayrollBankExportRequest request, string actor, CancellationToken ct)
    {
        PayrollAccountingService.ValidateEvidence(request.Reason, "Renewal of access to the same retained file.");
        var export = await repository.GetBankExportAsync(id, ct) ?? throw Conflict("Bank export not found.");
        if (!string.Equals(export.Sha256, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw Conflict("Expected export digest does not match.");
        // Validate every source and exact output bytes; elapsed access expiry alone may be renewed.
        var checkedOutput = await DownloadCoreAsync(id, ct, allowExpired: true);
        CryptographicOperations.ZeroMemory(checkedOutput.Content);
        var at = clock.GetUtcNow();
        var proof = new RenewalProof(1, id, export.Sha256!, export.SourceFingerprint, actor, at, at.AddHours(24), request.Reason.Trim());
        var reference = await WriteProofAsync(proof, ExportPurpose(id, "renewal"), ct);
        var renewal = new PayrollBankExportRenewal(id, actor, at, reference);
        await repository.AddBankExportRenewalAsync(renewal, ct); await repository.SaveChangesAsync(ct);
        return Map(export) with { ExpiresAt = renewal.ExpiresAt };
    }

    internal async Task<PayrollBankExportResponse> CreateExportCoreAsync(CreatePayrollBankExportRequest request, string actor, CancellationToken ct)
    {
        if (request.PaymentIds is null || request.PaymentIds.Count is < 1 or > 500 || request.PaymentIds.Distinct().Count() != request.PaymentIds.Count)
            throw Conflict("Select one to 500 distinct recorded employee payments.");
        await repository.AcquirePaymentLockAsync(request.ObligationId, Guid.Empty, ct);
        var obligation = await RequireSourceAsync(request.ObligationId, request.ExpectedSourceFingerprint, ct);
        var entitlements = JsonSerializer.Deserialize<BatchEmployeeEntitlementDto[]>(obligation.EmployeeEntitlementsJson!)!;
        var existingPayments = await repository.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);
        foreach (var existing in existingPayments.Where(x => x.State is SalaryPaymentState.Confirmed or SalaryPaymentState.Failed or SalaryPaymentState.Reversed))
            await (paymentEvidence ?? throw Conflict("Payment outcome evidence validator is required.")).ValidatePaymentProofsForReadAsync(existing, obligation, ct);
        if (existingPayments.Where(PayrollAccountingService.ReservesEntitlement).GroupBy(x => x.EmployeeId).Any(group =>
            group.Key is null || entitlements.SingleOrDefault(x => x.EmployeeId == group.Key) is not { } entitlement || group.Sum(x => x.Amount) > entitlement.NetSalary))
            throw Conflict("Existing employee payment reservations exceed the frozen entitlement.");
        var profile = await repository.GetBankFormatAsync(request.FormatProfileId, ct) ?? throw Conflict("A supplied reviewed bank format is required.");
        if (profile.State != PayrollFinanceApprovalState.Approved || profile.Currency != obligation.Currency)
            throw Conflict("An independently reviewed format for the obligation currency is required.");
        await ValidateFormatProofAsync(profile, true, ct);
        var rules = Rules(profile); var rows = new List<BankRow>(); var payments = new List<SalaryPayment>();
        foreach (var id in request.PaymentIds.Order())
        {
            var payment = await repository.GetSalaryPaymentByIdAsync(id, ct) ?? throw Conflict("Selected payment not found.");
            await repository.RefreshSalaryPaymentAsync(payment, ct);
            if (payment.ObligationId != obligation.Id || payment.EmployeeId is null || payment.State != SalaryPaymentState.Posted ||
                payment.BankExportId.HasValue || payment.ReversalRequestedAt.HasValue || !payment.PaymentJournalEntryId.HasValue)
                throw Conflict("Every selected payment must be posted, employee-bound, unexported and free of a reversal request.");
            var journal = await repository.GetJournalEntryByIdAsync(payment.PaymentJournalEntryId.Value, ct);
            var accrual = await repository.GetJournalEntryByIdAsync(payment.AccrualJournalEntryId, ct);
            if (entitlements.SingleOrDefault(x => x.EmployeeId == payment.EmployeeId) is not { } entitlement || payment.Amount > entitlement.NetSalary ||
                accrual is null || accrual.Kind != JournalEntryKind.SalaryAccrual || accrual.ObligationId != obligation.Id || accrual.Currency != obligation.Currency ||
                accrual.Amount != obligation.TotalNetAmount || accrual.SourceKey != $"ACCRUAL-SALARY-{obligation.Id:N}")
                throw Conflict("Payment does not belong to the frozen employee salary accrual.");
            if (journal is null || journal.Kind != JournalEntryKind.SalaryPayment || journal.ObligationId != obligation.Id ||
                journal.Amount != payment.Amount || journal.Currency != obligation.Currency || journal.PostingDate is null ||
                journal.PostedByUserId != payment.CreatedByUserId || journal.PostedAt != payment.CreatedAt ||
                journal.DebitAccountCode != accrual.CreditAccountCode || journal.PostingDate > request.ValueDate || journal.SourceKey != $"PAYMENT-{obligation.Id:N}-{payment.Id:N}")
                throw Conflict("Selected payment journal is inconsistent with the requested value date.");
            var bank = await RequireBankAsync(payment.EmployeeId.Value, request.ValueDate, ct);
            var employee = rules.Columns.Any(x => x.Field is "BeneficiaryName" or "ContactNumber" or "Email")
                ? await (employees ?? throw Conflict("Employee source is required for this format.")).FindAsync(payment.EmployeeId.Value, ct) : null;
            if (rules.Columns.Any(x => x.Field is "BeneficiaryName" or "ContactNumber" or "Email") && employee is null)
                throw Conflict("Beneficiary employee source is unavailable.");
            rows.Add(new(payment, bank.Bank.Id, bank.VerificationId, bank.Bank.Revision,
                await ReadBankFieldAsync(bank.Bank.BankNameEncryptedReference, bank.Bank, "name", ct),
                await ReadBankFieldAsync(bank.Bank.AccountTitleEncryptedReference, bank.Bank, "title", ct),
                await ReadBankFieldAsync(bank.Bank.AccountNumberEncryptedReference, bank.Bank, "account", ct),
                bank.Bank.IbanEncryptedReference is null ? "" : await ReadBankFieldAsync(bank.Bank.IbanEncryptedReference, bank.Bank, "iban", ct),
                employee?.FullLegalName ?? "", employee?.PersonalPhone ?? "", employee?.OfficialEmail ?? employee?.PersonalEmail ?? "", EmployeeFingerprint(employee, rules)));
            payments.Add(payment);
        }
        var export = new PayrollBankExport(obligation.Id, profile.Id, request.ValueDate, obligation.SourceFingerprint!, actor, clock.GetUtcNow());
        var output = Render(rules, rows, obligation.Currency, request.ValueDate);
        try
        {
            var outputReference = await files.WriteAsync(output, ExportPurpose(export.Id, "output"), ct);
            var items = rows.Select(x => new ExportItem(x.Payment.Id, x.Payment.EmployeeId!.Value, x.Payment.Amount, x.Payment.PaymentReference,
                x.Payment.Version + 1, x.BankId, x.BankRevision, x.VerificationId, x.EmployeeSourceSha256)).ToArray();
            var manifest = new ExportManifest(1, export.Id, obligation.Id, profile.Id, profile.RulesJson, obligation.SourceFingerprint!,
                obligation.Currency, request.ValueDate, actor, export.CreatedAt, export.ExpiresAt, outputReference.Sha256, items);
            var manifestReference = await WriteProofAsync(manifest, ExportPurpose(export.Id, "manifest"), ct);
            // Re-read protected output and all effective sources after storage IO before retaining this export.
            var retainedOutput = await files.ReadAsync(outputReference, ExportPurpose(export.Id, "output"), ct);
            if (!output.AsSpan().SequenceEqual(retainedOutput))
                throw Conflict("Protected bank output failed verification.");
            await RequireSourceAsync(obligation.Id, obligation.SourceFingerprint!, ct);
            foreach (var row in rows)
            {
                var current = await RequireBankAsync(row.Payment.EmployeeId!.Value, request.ValueDate, ct);
                if (current.Bank.Id != row.BankId || current.Bank.Revision != row.BankRevision || current.VerificationId != row.VerificationId) throw Conflict("Bank details changed during export.");
                if (row.EmployeeSourceSha256 is not null && EmployeeFingerprint(await employees!.FindAsync(row.Payment.EmployeeId.Value, ct), rules) != row.EmployeeSourceSha256)
                    throw Conflict("Beneficiary identity or contact source changed during export.");
            }
            export.Complete(JsonSerializer.Serialize(outputReference), manifestReference, outputReference.Sha256, payments.Count, payments.Sum(x => x.Amount));
            foreach (var payment in payments) payment.BindBankExport(export.Id);
            await repository.AddBankExportAsync(export, ct); await repository.SaveChangesAsync(ct);
            return Map(export);
        }
        finally { CryptographicOperations.ZeroMemory(output); }
    }

    public Task<PayrollBankExportDownload> DownloadAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync(Permission, "payroll.bank.export.download", "PayrollBankExport", id.ToString(), context,
            (_, token) => DownloadCoreAsync(id, token), ct);

    internal async Task<PayrollBankExportDownload> DownloadCoreAsync(Guid id, CancellationToken token, bool allowExpired = false)
            {
                var export = await repository.GetBankExportAsync(id, token) ?? throw Conflict("Bank export not found.");
                await repository.AcquirePaymentLockAsync(export.ObligationId, Guid.Empty, token);
                var expiry = await EffectiveExpiryAsync(export, token);
                if (!allowExpired && clock.GetUtcNow() >= expiry) throw Conflict("Bank export download has expired.");
                var manifest = await ReadProofAsync<ExportManifest>(export.PrivateManifestReference, ExportPurpose(id, "manifest"), token);
                var profile = await repository.GetBankFormatAsync(export.FormatProfileId, token) ?? throw Conflict("Bank format unavailable.");
                await ValidateFormatProofAsync(profile, true, token);
                var obligation = await RequireSourceAsync(export.ObligationId, export.SourceFingerprint, token);
                if (manifest.Schema != 1 || manifest.ExportId != id || manifest.ObligationId != obligation.Id || manifest.FormatProfileId != profile.Id ||
                    manifest.RulesJson != profile.RulesJson || manifest.SourceFingerprint != export.SourceFingerprint || manifest.Currency != obligation.Currency ||
                    manifest.ValueDate != export.ValueDate || manifest.Actor != export.CreatedByUserId || manifest.CreatedAt != export.CreatedAt ||
                    manifest.ExpiresAt != export.ExpiresAt || manifest.OutputSha256 != export.Sha256 || manifest.Items.Count != export.PaymentCount ||
                    manifest.Items.Sum(x => x.Amount) != export.TotalAmount || manifest.Items.Select(x => x.PaymentId).Distinct().Count() != manifest.Items.Count)
                    throw Conflict("Bank export manifest mismatch.");
                foreach (var item in manifest.Items)
                {
                    var payment = await repository.GetSalaryPaymentByIdAsync(item.PaymentId, token) ?? throw Conflict("Payment unavailable.");
                    await repository.RefreshSalaryPaymentAsync(payment, token);
                    if (payment.BankExportId != id || payment.ObligationId != obligation.Id || payment.EmployeeId != item.EmployeeId || payment.Amount != item.Amount ||
                        payment.PaymentReference != item.PaymentReference || payment.State != SalaryPaymentState.Posted || payment.ReversalRequestedAt.HasValue || payment.Version != item.PaymentVersion)
                        throw Conflict("Payment changed after export; this file cannot be released.");
                    var bank = await RequireBankAsync(item.EmployeeId, export.ValueDate, token);
                    if (bank.Bank.Id != item.BankId || bank.Bank.Revision != item.BankRevision || bank.VerificationId != item.VerificationId)
                        throw Conflict("Employee banking source changed after export.");
                    if (item.EmployeeSourceSha256 is not null && (employees is null || EmployeeFingerprint(await employees.FindAsync(item.EmployeeId, token), Rules(profile)) != item.EmployeeSourceSha256))
                        throw Conflict("Beneficiary identity or contact source changed after export.");
                }
                var reference = JsonSerializer.Deserialize<PrivateFileReference>(export.PrivateFileReference!) ?? throw Conflict("Export output unavailable.");
                var bytes = await files.ReadAsync(reference, ExportPurpose(id, "output"), token);
                if (Convert.ToHexString(SHA256.HashData(bytes)) != export.Sha256 || (!allowExpired && clock.GetUtcNow() >= expiry))
                    throw Conflict("Bank output is unavailable or expired.");
                var xlsx = Rules(profile).OutputFormat == "Xlsx";
                return new PayrollBankExportDownload(bytes, $"payroll-bank-{id:N}.{(xlsx ? "xlsx" : "csv")}",
                    xlsx ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv; charset=utf-8");
            }

    private async Task<DateTimeOffset> EffectiveExpiryAsync(PayrollBankExport export, CancellationToken ct)
    {
        var renewal = await repository.LatestBankExportRenewalAsync(export.Id, ct);
        if (renewal is null) return export.ExpiresAt;
        var proof = await ReadProofAsync<RenewalProof>(renewal.PrivateProofReference, ExportPurpose(export.Id, "renewal"), ct);
        if (proof.Schema != 1 || proof.ExportId != export.Id || renewal.ExportId != export.Id || proof.OutputSha256 != export.Sha256 ||
            proof.SourceFingerprint != export.SourceFingerprint || proof.Actor != renewal.CreatedByUserId || proof.CreatedAt != renewal.CreatedAt ||
            proof.ExpiresAt != renewal.ExpiresAt || renewal.ExpiresAt != renewal.CreatedAt.AddHours(24) || string.IsNullOrWhiteSpace(proof.Reason))
            throw Conflict("Retained export renewal evidence is inconsistent.");
        return renewal.ExpiresAt > export.ExpiresAt ? renewal.ExpiresAt : export.ExpiresAt;
    }

    private async Task<PayrollObligation> RequireSourceAsync(Guid id, string fingerprint, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(id, ct) ?? throw Conflict("Payroll obligation not found.");
        var source = await frozenReader.ReadAsync(obligation.BatchId, ct);
        if (source.BatchId != obligation.BatchId || source.CalculationVersion != "salary-v2" || source.Fingerprint != fingerprint)
            throw Conflict("Exact frozen payroll source is required.");
        PayrollAccountingService.ValidateObligationSource(obligation, source); return obligation;
    }

    private async Task<(EmployeeBankDetails Bank, Guid? VerificationId)> RequireBankAsync(Guid employeeId, DateOnly valueDate, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Pakistan Standard Time").DateTime);
        var bank = await banks.CurrentBankAsync(employeeId, valueDate, ct) ?? throw Conflict("Verified employee banking details are required.");
        var current = await banks.CurrentBankAsync(employeeId, today, ct);
        if (bank.PayloadSchemaVersion != 1 || current?.Id != bank.Id || bank.EffectiveTo <= valueDate || bank.EffectiveTo <= today ||
            await updates.HasConflictingHistoryAsync(employeeId, EmployeeChangeSection.Bank, ct) || await banks.HasConflictingHistoryAsync(employeeId, bank.Id, ct))
            throw Conflict("Bank revision is stale, conflicting or outside its effective dates.");
        var verification = await banks.LatestAsync(employeeId, bank.Id, ct);
        if (!(verification?.IsVerified ?? bank.IsVerified)) throw Conflict("Employee bank details are not verified.");
        if (verification is not null)
        {
            var proof = await ReadProofAsync<BankVerificationProof>(verification.PrivateEvidenceReference,
                EmployeeHiringService.Purpose(employeeId, $"bank-verification/{bank.Id:N}"), ct);
            if (proof.SchemaVersion != 1 || proof.EmployeeId != employeeId || proof.BankDetailsId != bank.Id || proof.Version != verification.Version ||
                !proof.IsVerified || proof.Actor != verification.RecordedByUserId || proof.RecordedAt != verification.RecordedAt || string.IsNullOrWhiteSpace(proof.Proof))
                throw Conflict("Bank verification evidence is inconsistent.");
        }
        else if (string.IsNullOrWhiteSpace(bank.VerifiedByUserId) || bank.VerifiedAt is null) throw Conflict("Bank verification actor and time are required.");
        return (bank, verification?.Id);
    }

    private async Task<string> ReadBankFieldAsync(string encoded, EmployeeBankDetails bank, string field, CancellationToken ct)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(encoded) ?? throw Conflict("Protected bank data unavailable.");
        if (reference.KeyReference != bank.EncryptionKeyReference) throw Conflict("Bank encryption key binding mismatch.");
        return await ReadProofAsync<string>(encoded, EmployeeHiringService.Purpose(bank.EmployeeId, $"approved/bank/{field}"), ct);
    }

    private async Task ValidateFormatProofAsync(PayrollBankFormatProfile profile, bool approval, CancellationToken ct)
    {
        ValidateFormat(Rules(profile));
        async Task Validate(string? reference, string section, string actor, DateTimeOffset at)
        {
            var proof = await ReadProofAsync<FormatProof>(reference, FormatPurpose(profile.Id, section), ct);
            if (proof.Schema != 1 || proof.Id != profile.Id || proof.Name != profile.Name || proof.FormatVersion != profile.FormatVersion ||
                proof.Currency != profile.Currency || proof.RulesJson != profile.RulesJson || proof.Actor != actor || proof.At != at)
                throw Conflict("Retained bank format evidence does not match its profile.");
            PayrollAccountingService.ValidateEvidence(proof.Reason, proof.Evidence);
        }
        await Validate(profile.TemplateProofReference, "template", profile.CreatedByUserId, profile.CreatedAt);
        if (approval)
        {
            if (profile.State != PayrollFinanceApprovalState.Approved || profile.ApprovedAt is null || string.IsNullOrWhiteSpace(profile.ApprovedByUserId) ||
                string.Equals(profile.ApprovedByUserId, profile.CreatedByUserId, StringComparison.OrdinalIgnoreCase)) throw Conflict("Independent format approval required.");
            await Validate(profile.ApprovalProofReference, "approval", profile.ApprovedByUserId, profile.ApprovedAt.Value);
        }
    }

    internal static void ValidateFormat(PayrollBankFormatRules rules)
    {
        string[] fields = ["EmployeeId", "AccountNumber", "Iban", "AccountTitle", "BankName", "BankCode", "BeneficiaryName", "ContactNumber", "Email", "Amount", "Currency", "Reference", "ValueDate", "Literal", "Blank"];
        if (rules is null || rules.OutputFormat is not ("Csv" or "Xlsx") || rules.Delimiter is not ("," or ";" or "\t") || rules.DateFormat is not ("yyyy-MM-dd" or "yyyyMMdd" or "dd/MM/yyyy") ||
            rules.Columns is null || rules.Columns.Count is < 3 or > 20 || rules.Columns.Any(x => x is null || !fields.Contains(x.Field) || string.IsNullOrWhiteSpace(x.Header) || x.Header.Length > 100 ||
                (x.Field == "Literal" && string.IsNullOrWhiteSpace(x.Literal)) || (x.Field != "Literal" && x.Literal is not null)) ||
            rules.Columns.Where(x => x.Field is not ("Literal" or "Blank")).Select(x => x.Field).Distinct().Count() != rules.Columns.Count(x => x.Field is not ("Literal" or "Blank")) ||
            rules.Columns.Select(x => x.Header).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rules.Columns.Count ||
            !new[] { "Amount", "Reference" }.All(f => rules.Columns.Any(x => x.Field == f)) || !rules.Columns.Any(x => x.Field is "AccountNumber" or "Iban"))
            throw Conflict("Supply an explicit supported format with unique mapped columns, amount, reference and account identifier.");
        if (rules.OutputFormat == "Csv" && !rules.Columns.Any(x => x.Field == "Currency")) throw Conflict("CSV requires an explicit currency column.");
        if (rules.OutputFormat == "Xlsx" && (!rules.IncludeHeader || string.IsNullOrWhiteSpace(rules.WorksheetName) || rules.WorksheetName.Length > 31 ||
            rules.WorksheetName.Any(c => "[]:*?/\\".Contains(c) || char.IsControl(c)) || rules.TemplateSha256 is null ||
            rules.TemplateSha256.Length != 64 || !rules.TemplateSha256.All(Uri.IsHexDigit)))
            throw Conflict("XLSX requires a supplied template SHA-256, exact worksheet name and header row.");
        if (rules.Columns.Any(x => x.Field == "BankCode") && (rules.BankCodes is null || rules.BankCodes.Count is < 1 or > 200 ||
            rules.BankCodes.Any(x => string.IsNullOrWhiteSpace(x.BankName) || x.BankName.Length > 200 || string.IsNullOrWhiteSpace(x.BankCode) || x.BankCode.Length > 20 || !x.BankCode.All(char.IsAsciiDigit)) ||
            rules.BankCodes.Select(x => x.BankName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rules.BankCodes.Count))
            throw Conflict("Explicit reviewed beneficiary bank-name to routing-code mappings are required.");
        if ((string.Equals(rules.TemplateSha256, PayrollBankTemplates.HblInternalSha256, StringComparison.OrdinalIgnoreCase) && rules.BeneficiaryBankNames is null) ||
            (rules.BeneficiaryBankNames is not null && (rules.BeneficiaryBankNames.Count is < 1 or > 200 ||
                rules.BeneficiaryBankNames.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200 || x.Any(char.IsControl)) ||
                rules.BeneficiaryBankNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != rules.BeneficiaryBankNames.Count)))
            throw Conflict("An explicit reviewed beneficiary bank-name allowlist is required for restricted same-bank formats.");
        var suppliedTemplate = PayrollBankTemplates.Definitions().SingleOrDefault(x => string.Equals(x.SourceTemplateSha256, rules.TemplateSha256, StringComparison.OrdinalIgnoreCase));
        if (suppliedTemplate is not null && (rules.OutputFormat != "Xlsx" || rules.WorksheetName != suppliedTemplate.Rules.WorksheetName ||
            !rules.IncludeHeader || !rules.Columns.SequenceEqual(suppliedTemplate.Rules.Columns)))
            throw Conflict("The supplied HBL template hash requires its exact column names, order and field mapping.");
        foreach (var column in rules.Columns) CsvCell(column.Header);
    }

    internal static string CsvCell(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || "=+-@".Contains(value.TrimStart()[0]))
            throw Conflict("Bank export contains a blank, control character or spreadsheet formula value.");
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    private static byte[] Render(PayrollBankFormatRules rules, IReadOnlyList<BankRow> rows, string currency, DateOnly valueDate)
    {
        string Cell(BankRow row, PayrollBankColumn column)
        {
            var value = column.Field switch
            {
                "EmployeeId" => row.Payment.EmployeeId!.Value.ToString("D"), "AccountNumber" => row.AccountNumber, "Iban" => row.Iban,
                "AccountTitle" => row.AccountTitle, "BankName" => row.BankName,
                "BankCode" => rules.BankCodes?.SingleOrDefault(x => string.Equals(x.BankName.Trim(), row.BankName.Trim(), StringComparison.OrdinalIgnoreCase))?.BankCode
                    ?? throw Conflict("No reviewed routing code matches the employee's verified bank name."),
                "BeneficiaryName" => row.BeneficiaryName, "ContactNumber" => row.ContactNumber, "Email" => row.Email,
                "Amount" => row.Payment.Amount.ToString("F2", CultureInfo.InvariantCulture), "Currency" => currency,
                "Reference" => row.Payment.PaymentReference, "ValueDate" => valueDate.ToString(rules.DateFormat, CultureInfo.InvariantCulture),
                "Literal" => column.Literal!, "Blank" => "", _ => throw Conflict("Unsupported bank column.")
            };
            if (string.IsNullOrEmpty(value) && (column.Field == "Blank" || !column.Required)) return "";
            // All XLSX strings are inline text. CSV additionally rejects formula-looking values.
            if (rules.OutputFormat == "Csv") CsvCell(value);
            else if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)) throw Conflict("Required bank cell is blank or contains control characters.");
            return value;
        }
        if (rules.BeneficiaryBankNames is not null && rows.Any(row => !rules.BeneficiaryBankNames.Any(name => string.Equals(name.Trim(), row.BankName.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw Conflict("This format is restricted to explicitly reviewed beneficiary bank names.");
        var values = rows.Select(row => rules.Columns.Select(column => Cell(row, column)).ToArray()).ToArray();
        if (rules.OutputFormat == "Xlsx") return PayrollBankXlsxWriter.Write(rules, values);
        var output = new StringBuilder();
        if (rules.IncludeHeader) output.AppendJoin(rules.Delimiter, rules.Columns.Select(x => CsvCell(x.Header))).Append("\r\n");
        foreach (var row in values) output.AppendJoin(rules.Delimiter, row.Select(value => value.Length == 0 ? "\"\"" : CsvCell(value))).Append("\r\n");
        var bytes = Encoding.UTF8.GetBytes(output.ToString());
        return rules.Utf8Bom ? [0xEF, 0xBB, 0xBF, .. bytes] : bytes;
    }
    private async Task<string> WriteProofAsync<T>(T proof, string purpose, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(proof);
        try { return JsonSerializer.Serialize(await files.WriteAsync(bytes, purpose, ct)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private async Task<T> ReadProofAsync<T>(string? encoded, string purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw Conflict("Protected evidence unavailable.");
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(encoded) ?? throw Conflict("Protected evidence unavailable.");
        var bytes = await files.ReadAsync(reference, purpose, ct);
        try { return JsonSerializer.Deserialize<T>(bytes) ?? throw Conflict("Protected evidence unavailable."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static PayrollBankFormatRules Rules(PayrollBankFormatProfile p) => JsonSerializer.Deserialize<PayrollBankFormatRules>(p.RulesJson) ?? throw Conflict("Format rules unavailable.");
    private static string? EmployeeFingerprint(Employee? employee, PayrollBankFormatRules rules)
    {
        var columns = rules.Columns.Where(x => x.Field is "BeneficiaryName" or "ContactNumber" or "Email").ToArray();
        if (columns.Length == 0) return null;
        if (employee is null) throw Conflict("Beneficiary source unavailable.");
        var values = columns.Select(x => new { x.Field, Value = x.Field switch
        { "BeneficiaryName" => employee.FullLegalName, "ContactNumber" => employee.PersonalPhone, _ => employee.OfficialEmail ?? employee.PersonalEmail } }).ToArray();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { employee.Id, Values = values })));
    }
    private static PayrollBankFormatResponse Map(PayrollBankFormatProfile p) => new(p.Id, p.Name, p.FormatVersion, p.Currency, p.State.ToString(), p.Version, Rules(p), p.CreatedAt, p.ApprovedAt, p.CreatedByUserId, p.ApprovedByUserId);
    private static PayrollBankExportResponse Map(PayrollBankExport e) => new(e.Id, e.ObligationId, e.FormatProfileId, e.ValueDate, e.PaymentCount, e.TotalAmount, e.Sha256!, e.CreatedAt, e.ExpiresAt);
    private static string FormatPurpose(Guid id, string part) => $"payroll-bank-format/{id:N}/{part}";
    private static string ExportPurpose(Guid id, string part) => $"payroll-bank-export/{id:N}/{part}";
    private static PayrollFinanceConflictException Conflict(string message) => new(message);
    private sealed record FormatProof(int Schema, Guid Id, string Name, int FormatVersion, string Currency, string RulesJson, string Actor, DateTimeOffset At, string Reason, string Evidence);
    private sealed record ExportManifest(int Schema, Guid ExportId, Guid ObligationId, Guid FormatProfileId, string RulesJson, string SourceFingerprint,
        string Currency, DateOnly ValueDate, string Actor, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string OutputSha256, IReadOnlyList<ExportItem> Items);
    private sealed record ExportItem(Guid PaymentId, Guid EmployeeId, decimal Amount, string PaymentReference, int PaymentVersion, Guid BankId, int BankRevision, Guid? VerificationId, string? EmployeeSourceSha256);
    private sealed record BankRow(SalaryPayment Payment, Guid BankId, Guid? VerificationId, int BankRevision, string BankName, string AccountTitle, string AccountNumber, string Iban,
        string BeneficiaryName, string ContactNumber, string Email, string? EmployeeSourceSha256);
    private sealed record BankVerificationProof(int SchemaVersion, Guid EmployeeId, Guid BankDetailsId, int Version, bool IsVerified, string Actor, DateTimeOffset RecordedAt, string Reason, string Proof);
    private sealed record RenewalProof(int Schema, Guid ExportId, string OutputSha256, string SourceFingerprint, string Actor, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Reason);
}
