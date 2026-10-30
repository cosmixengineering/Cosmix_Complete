using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

public enum SilentFaceOutputKind { Unspecified, Logits, Probabilities }
public enum SilentFaceCropKind { Unspecified, UpstreamScaledBox, WholeImage }

/// <summary>Server-reviewed contract for one exact ONNX export, never inferred from its filename.</summary>
public sealed record SilentFaceModel
{
    public string Path { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public string ExportReference { get; init; } = "";
    public string LicenseReviewReference { get; init; } = "";
    public string SemanticsReviewReference { get; init; } = "";
    public string InputName { get; init; } = "";
    public string OutputName { get; init; } = "";
    /// <summary>Reviewed release export dimension, explicitly fixed to one by ONNX Runtime.
    /// Null retains the existing static-shape-only contract.</summary>
    public string? BatchDimensionName { get; init; }
    public int InputWidth { get; init; }
    public int InputHeight { get; init; }
    public string InputContract { get; init; } = "";
    public SilentFaceCropKind CropKind { get; init; }
    public double? CropScale { get; init; }
    public SilentFaceOutputKind OutputKind { get; init; }
    public int ClassCount { get; init; }
    public int? LiveClassIndex { get; init; }
    public double? MinimumLiveProbability { get; init; }
}

public sealed record SilentFaceOptions
{
    public const string SectionName = "SelfHostedFace:SilentFace";
    public bool Enabled { get; init; }
    public IReadOnlyList<SilentFaceModel> Models { get; init; } = Array.Empty<SilentFaceModel>();
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed record SilentFaceModelScore(string ModelSha256, double LiveProbability, bool Passed);
public sealed record SilentFaceLivenessResult(bool Passed, double LiveProbability, IReadOnlyList<SilentFaceModelScore> Models,
    string ModelVersion = "", double ProcessingTimeMilliseconds = 0, string? FailureReason = null);

internal static class SilentFaceModelValidation
{
    internal const string InputContract = "float32-NCHW-BGR-0_255-OpenCV_INTER_LINEAR-CompreFaceExtent";
    internal const int MaximumModelBytes = 33554432;

    internal static void Validate(SilentFaceModel model)
    {
        if (model.BatchDimensionName is not (null or "batch_size") ||
            !Hex(model.Sha256, 64) || !Hex(model.SourceRevision, 40) ||
            !CompreFaceClient.ValidText(model.ExportReference, 500) || !CompreFaceClient.ValidText(model.LicenseReviewReference, 500) ||
            !CompreFaceClient.ValidText(model.SemanticsReviewReference, 500) ||
            !CompreFaceClient.ValidText(model.InputName, 100) || !CompreFaceClient.ValidText(model.OutputName, 100) ||
            model.InputContract != InputContract || model.InputWidth is < 16 or > 512 || model.InputHeight is < 16 or > 512 ||
            model.ClassCount is < 2 or > 16 || model.LiveClassIndex is not int live || live < 0 || live >= model.ClassCount ||
            model.MinimumLiveProbability is not double threshold || !double.IsFinite(threshold) || threshold is <= 0 or > 1 ||
            model.OutputKind is not (SilentFaceOutputKind.Logits or SilentFaceOutputKind.Probabilities) ||
            model.CropKind is not (SilentFaceCropKind.UpstreamScaledBox or SilentFaceCropKind.WholeImage) ||
            model.CropKind == SilentFaceCropKind.UpstreamScaledBox &&
                (model.CropScale is not double scale || !double.IsFinite(scale) || scale is < 1 or > 10) ||
            model.CropKind == SilentFaceCropKind.WholeImage && model.CropScale is not null)
            throw Unavailable();
        if (string.IsNullOrWhiteSpace(model.Path) || !System.IO.Path.IsPathFullyQualified(model.Path) ||
            model.Path.StartsWith("\\\\", StringComparison.Ordinal) || model.Path.StartsWith("//", StringComparison.Ordinal) ||
            !System.IO.Path.GetExtension(model.Path).Equals(".onnx", StringComparison.OrdinalIgnoreCase)) throw Unavailable();
        var fullPath = System.IO.Path.GetFullPath(model.Path);
        if (OperatingSystem.IsWindows() && fullPath[2..].Contains(':')) throw Unavailable();
        for (FileSystemInfo? entry = new FileInfo(fullPath); entry is not null;
            entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
            if (!entry.Exists || (entry.Attributes & FileAttributes.ReparsePoint) != 0) throw Unavailable();
    }

    internal static byte[] ReadPinnedBytes(SilentFaceModel model)
    {
        Validate(model);
        using var stream = new FileStream(model.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumModelBytes) throw Unavailable();
        var data = new byte[(int)stream.Length];
        try
        {
            stream.ReadExactly(data);
            if (stream.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(data), Convert.FromHexString(model.Sha256)))
                throw Unavailable();
            return data;
        }
        catch { CryptographicOperations.ZeroMemory(data); throw; }
    }

    internal static string ContractDigest(IEnumerable<SilentFaceModel> models) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(models.Select(m => m with { Path = "" }))));
    private static bool Hex(string? value, int length) => value?.Length == length && value.All(Uri.IsHexDigit);
    internal static AuthenticationFailure Unavailable() => new("face_liveness_not_configured", 503);
}
