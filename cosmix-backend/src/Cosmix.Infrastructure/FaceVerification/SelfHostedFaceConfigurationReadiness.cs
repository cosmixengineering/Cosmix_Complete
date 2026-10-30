using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Infrastructure.Files;
using Microsoft.Extensions.Configuration;

namespace Cosmix.Infrastructure.FaceVerification;

public sealed record FaceConfigurationIssue(string Setting, string Code);
public sealed record FaceConfigurationReport(IReadOnlyList<FaceConfigurationIssue> Issues,
    IReadOnlyList<string> DisabledSections, string LocalArtifacts)
{
    public bool Passed => Issues.Count == 0;
}

/// <summary>One-time local setup inspection. Never registers a provider, submits images or writes evidence.</summary>
public static class SelfHostedFaceConfigurationReadiness
{
    public static FaceConfigurationReport EvaluateFile(string? configurationFile, bool checkLocalArtifacts = false)
    {
        if (configurationFile is null) return Evaluate(new ConfigurationBuilder().Build(), checkLocalArtifacts);
        byte[]? bytes = null;
        try
        {
            var path = PrivateFilePath.Require(configurationFile, false);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is < 1 or > 65536) throw new InvalidDataException();
            bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1 || file.Length != bytes.Length) throw new InvalidDataException();
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Flatten(json.RootElement, "", values);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            try { return Evaluate(configuration, checkLocalArtifacts); }
            finally { (configuration as IDisposable)?.Dispose(); }
        }
        catch (Exception)
        {
            return new([new("ConfigurationFile", "private_configuration_file_invalid")], [], "not_checked");
        }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void Flatten(JsonElement element, string key, Dictionary<string, string?> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException();
                Flatten(property.Value, key.Length == 0 ? property.Name : key + ":" + property.Name, values);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in element.EnumerateArray()) Flatten(item, key + ":" + i++, values);
        }
        else if (!values.TryAdd(key, element.ValueKind == JsonValueKind.String ? element.GetString() :
            element.ValueKind == JsonValueKind.Null ? null : element.GetRawText())) throw new InvalidDataException();
    }

    public static FaceConfigurationReport Evaluate(IConfiguration configuration, bool checkLocalArtifacts = false)
    {
        var issues = new List<FaceConfigurationIssue>();
        var disabled = new List<string>();
        void Issue(string setting, string code) => issues.Add(new(setting, code));
        void Required(string section, params string[] fields)
        {
            foreach (var field in fields)
                if (string.IsNullOrWhiteSpace(configuration[$"{section}:{field}"])) Issue($"{section}:{field}", "required");
        }
        T Bind<T>(string section) where T : new()
        {
            try { return configuration.GetSection(section).Get<T>() ?? new(); }
            catch (Exception) { Issue(section, "invalid_configuration_binding"); return new(); }
        }
        void Selection(string section, bool enabled) { if (!enabled) disabled.Add(section); }

        var provider = Bind<SelfHostedFaceOptions>(SelfHostedFaceOptions.SectionName);
        Selection(SelfHostedFaceOptions.SectionName, provider.Enabled);
        Required(SelfHostedFaceOptions.SectionName, "MinimumSimilarity");
        if (provider.MinimumSimilarity is not double similarity || !double.IsFinite(similarity) || similarity is <= 0 or > 1)
            Issue(SelfHostedFaceOptions.SectionName, "invalid_configuration");

        var client = Bind<CompreFaceOptions>(CompreFaceOptions.SectionName);
        Required(CompreFaceOptions.SectionName, "BaseUri", "DetectionApiKey", "VerificationApiKey",
            "DeploymentVersion", "MinimumDetectionProbability");
        try { CompreFaceClient.ValidateOptions(client); }
        catch (Exception) { Issue(CompreFaceOptions.SectionName, "invalid_configuration"); }

        var silent = Bind<SilentFaceOptions>(SilentFaceOptions.SectionName);
        Selection(SilentFaceOptions.SectionName, silent.Enabled);
        var modelIssuesStart = issues.Count;
        if (silent.TimeoutSeconds is < 1 or > 60) Issue(SilentFaceOptions.SectionName + ":TimeoutSeconds", "invalid_configuration");
        if (silent.Models is null || silent.Models.Count is < 1 or > 4)
            Issue(SilentFaceOptions.SectionName + ":Models", "one_to_four_reviewed_models_required");
        else
        {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < silent.Models.Count; i++)
            {
                var section = $"{SilentFaceOptions.SectionName}:Models:{i}";
                Required(section, "Path", "Sha256", "SourceRevision", "ExportReference", "LicenseReviewReference",
                    "SemanticsReviewReference", "InputName", "OutputName", "InputWidth", "InputHeight", "InputContract",
                    "CropKind", "OutputKind", "ClassCount", "LiveClassIndex", "MinimumLiveProbability");
                var model = silent.Models[i];
                if (model is null) { Issue(section, "invalid_contract_or_local_path"); continue; }
                if (model.CropKind == SilentFaceCropKind.UpstreamScaledBox) Required(section, "CropScale");
                if (!hashes.Add(model.Sha256 ?? "")) Issue(section + ":Sha256", "duplicate_model");
                try { SilentFaceModelValidation.Validate(model); }
                catch (Exception) { Issue(section, "invalid_contract_or_local_path"); }
            }
        }
        var localArtifacts = "not_checked";
        if (checkLocalArtifacts)
        {
            localArtifacts = "failed";
            // Enabling this local instance only loads pinned bytes/metadata/codecs. It is never put into DI.
            if (issues.Count == modelIssuesStart)
            {
                using var load = new SilentFaceLivenessService(silent with { Enabled = true });
                if (load.IsConfigured) localArtifacts = "loaded";
            }
            if (localArtifacts == "failed") Issue(SilentFaceOptions.SectionName, "local_artifact_load_failed");
        }

        var policy = Bind<FaceVerificationPolicy>(FaceVerificationPolicy.SectionName);
        Required(FaceVerificationPolicy.SectionName, "AuthorityConfirmed", "ApprovalReference", "ChallengeLifetimeSeconds",
            "MaximumAttemptsPerPunch", "RetentionPolicyReference");
        if (!policy.IsConfirmed) Issue(FaceVerificationPolicy.SectionName, "invalid_configuration");
        var capture = Bind<FaceCaptureOptions>(FaceCaptureOptions.SectionName);
        Selection(FaceCaptureOptions.SectionName, capture.Enabled);
        Required(FaceCaptureOptions.SectionName, "LifetimeSeconds", "ApprovalReference");
        var captureInputs = new FaceCaptureOptions
            { Enabled = true, LifetimeSeconds = capture.LifetimeSeconds, ApprovalReference = capture.ApprovalReference };
        if (!captureInputs.IsConfigured) Issue(FaceCaptureOptions.SectionName, "invalid_configuration");

        var retention = Bind<FaceStorageRetentionOptions>(FaceStorageRetentionOptions.SectionName);
        Selection(FaceStorageRetentionOptions.SectionName, retention.Enabled);
        Required(FaceStorageRetentionOptions.SectionName, "AuthorityConfirmed", "ApprovalReference",
            "CaptureUseLifetimeSeconds", "EnrollmentUseLifetimeSeconds");
        var biometric = Bind<FaceTemplateStorageOptions>(FaceTemplateStorageOptions.SectionName);
        Selection(FaceTemplateStorageOptions.SectionName, biometric.Enabled);
        var journalOptions = Bind<FaceStorageRetentionJournalOptions>(FaceStorageRetentionJournalOptions.SectionName);
        Selection(FaceStorageRetentionJournalOptions.SectionName, journalOptions.Enabled);
        Required(FaceStorageRetentionJournalOptions.SectionName, "IndexDirectory");
        foreach (var section in new[] { FaceTemplateStorageOptions.SectionName + ":Storage",
            FaceStorageRetentionJournalOptions.SectionName + ":Storage", PrivateFileStorageOptions.SectionName })
            Required(section, "Directory", "KeyReference", "KeyFile");
        var ordinary = Bind<PrivateFileStorageOptions>(PrivateFileStorageOptions.SectionName);
        // These constructors only inspect existing paths/ACLs/keys. No directories or objects are created.
        try
        {
            _ = new EncryptedPrivateFileStore(ordinary);
        }
        catch (Exception) { Issue(PrivateFileStorageOptions.SectionName, "private_storage_configuration_invalid"); }
        var vault = new EncryptedFaceTemplateStore(new() { Enabled = true, Storage = biometric.Storage }, ordinary);
        if (!vault.IsConfigured) Issue(FaceTemplateStorageOptions.SectionName, "private_storage_configuration_invalid");
        var journal = new EncryptedFaceStorageRetentionJournal(journalOptions with { Enabled = true }, biometric, ordinary);
        if (!journal.IsConfigured) Issue(FaceStorageRetentionJournalOptions.SectionName, "private_journal_configuration_invalid");
        var gate = new RetentionControlledFaceTemplateStore(vault, journal, retention with { Enabled = true }, TimeProvider.System);
        if (!gate.IsConfigured) Issue(FaceStorageRetentionOptions.SectionName, "approved_use_or_storage_configuration_invalid");

        return new(issues.AsReadOnly(), disabled.AsReadOnly(), localArtifacts);
    }
}
