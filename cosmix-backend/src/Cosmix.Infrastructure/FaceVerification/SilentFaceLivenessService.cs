using System.Security.Cryptography;
using System.Diagnostics;
using Cosmix.Application.Authentication;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Cosmix.Infrastructure.FaceVerification;

/// <summary>Local CPU ONNX inference only. Missing/rejected artifacts never have a fallback success path.</summary>
public sealed class SilentFaceLivenessService : IDisposable
{
    private readonly List<LoadedModel> models = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly int timeoutSeconds;
    private bool disposed;
    public bool IsConfigured { get; }
    public string ContractDigest { get; } = "";
    public string ReasonCode { get; } = "face_liveness_not_configured";

    public SilentFaceLivenessService(SilentFaceOptions options)
    {
        timeoutSeconds = options.TimeoutSeconds;
        if (!options.Enabled || timeoutSeconds is < 1 or > 60 || options.Models is null || options.Models.Count is < 1 or > 4) return;
        try
        {
            var contracts = options.Models.ToArray();
            if (contracts.Select(m => m.Sha256.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() != contracts.Length)
                throw SilentFaceModelValidation.Unavailable();
            foreach (var model in contracts)
            {
                var bytes = SilentFaceModelValidation.ReadPinnedBytes(model);
                try
                {
                    using var sessionOptions = new SessionOptions
                    {
                        IntraOpNumThreads = 1, InterOpNumThreads = 1,
                        ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
                        LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_FATAL,
                        EnableProfiling = false
                    };
                    if (model.BatchDimensionName is not null)
                        sessionOptions.AddFreeDimensionOverrideByName(model.BatchDimensionName, 1);
                    var session = new InferenceSession(bytes, sessionOptions);
                    try { ValidateMetadata(session, model); models.Add(new(model, session)); }
                    catch { session.Dispose(); throw; }
                }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            // Check the native image codec is present as well as ONNX, without any biometric input.
            using var nativeProbe = new Mat(1, 1, MatType.CV_8UC3);
            ContractDigest = SilentFaceModelValidation.ContractDigest(contracts);
            IsConfigured = true;
            ReasonCode = "configured";
        }
        catch (Exception)
        {
            foreach (var model in models) model.Session.Dispose();
            models.Clear();
        }
    }

    public async Task<SilentFaceLivenessResult> VerifyAsync(byte[] image, FaceBounds bounds, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsConfigured) throw SilentFaceModelValidation.Unavailable();
        var started = Stopwatch.GetTimestamp();
        var info = SelfHostedFaceImage.Inspect(image);
        if (!bounds.IsWithin(info.Width, info.Height)) throw SelfHostedFaceImage.Invalid();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var entered = false;
        try
        {
            await gate.WaitAsync(deadline.Token);
            entered = true;
            ObjectDisposedException.ThrowIf(disposed, this);
            // Await native work to completion; never abandon a still-running inference holding image memory.
            return await Task.Run(() => Infer(image, bounds, info, deadline.Token, started), CancellationToken.None);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AuthenticationFailure("face_liveness_timeout", 503); }
        catch (OnnxRuntimeException) when (deadline.IsCancellationRequested)
        { ct.ThrowIfCancellationRequested(); throw new AuthenticationFailure("face_liveness_timeout", 503); }
        catch (OnnxRuntimeException) { throw new AuthenticationFailure("face_liveness_unavailable", 503); }
        catch (OpenCvSharpException) { throw SelfHostedFaceImage.Invalid(); }
        finally { if (entered) gate.Release(); }
    }

    private SilentFaceLivenessResult Infer(byte[] image, FaceBounds bounds, SelfHostedFaceImageInfo info, CancellationToken ct, long started)
    {
        ct.ThrowIfCancellationRequested();
        using var decoded = Cv2.ImDecode(image, ImreadModes.Color | ImreadModes.IgnoreOrientation);
        if (decoded.Empty() || decoded.Width != info.Width || decoded.Height != info.Height || decoded.Type() != MatType.CV_8UC3)
            throw SelfHostedFaceImage.Invalid();
        try
        {
            var scores = new List<SilentFaceModelScore>();
            foreach (var model in models)
            {
                ct.ThrowIfCancellationRequested();
                using var crop = new Mat(decoded, CropRectangle(bounds, info.Width, info.Height, model.Contract));
                using var resized = new Mat();
                Cv2.Resize(crop, resized, new Size(model.Contract.InputWidth, model.Contract.InputHeight), 0, 0, InterpolationFlags.Linear);
                try
                {
                    var tensor = CreateInputTensor(resized);
                    try
                    {
                        using var runOptions = new RunOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_FATAL };
                        using var cancellation = ct.Register(() => runOptions.Terminate = true);
                        ct.ThrowIfCancellationRequested();
                        using var outputs = model.Session.Run(
                            new[] { NamedOnnxValue.CreateFromTensor(model.Contract.InputName, tensor) },
                            new[] { model.Contract.OutputName }, runOptions);
                        ct.ThrowIfCancellationRequested();
                        var output = outputs.Single().AsTensor<float>();
                        if (!output.Dimensions.SequenceEqual(new[] { 1, model.Contract.ClassCount })) throw InvalidOutput();
                        var values = output.ToArray();
                        try { scores.Add(Evaluate(model.Contract, values)); }
                        finally { Array.Clear(values); }
                    }
                    finally { tensor.Buffer.Span.Clear(); }
                }
                finally { resized.SetTo(Scalar.All(0)); }
            }
            ct.ThrowIfCancellationRequested();
            // Each reviewed model must pass; a strong model cannot hide another model's spoof verdict.
            var passed = scores.All(s => s.Passed);
            return new(passed, scores.Min(s => s.LiveProbability), scores.AsReadOnly(), ContractDigest,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, passed ? null : "face_liveness_failed");
        }
        finally { decoded.SetTo(Scalar.All(0)); }
    }

    private static DenseTensor<float> CreateInputTensor(Mat resized)
    {
        var height = resized.Height;
        var width = resized.Width;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = resized.At<Vec3b>(y, x);
            tensor[0, 0, y, x] = pixel.Item0;
            tensor[0, 1, y, x] = pixel.Item1;
            tensor[0, 2, y, x] = pixel.Item2;
        }
        return tensor;
    }

    private static SilentFaceModelScore Evaluate(SilentFaceModel model, float[] values)
    {
        if (values.Length != model.ClassCount || values.Any(v => !float.IsFinite(v))) throw InvalidOutput();
        var probabilities = new double[values.Length];
        if (model.OutputKind == SilentFaceOutputKind.Logits)
        {
            var maximum = values.Max();
            var sum = 0d;
            for (var i = 0; i < values.Length; i++) { probabilities[i] = Math.Exp((double)values[i] - maximum); sum += probabilities[i]; }
            if (!double.IsFinite(sum) || sum <= 0) throw InvalidOutput();
            for (var i = 0; i < probabilities.Length; i++) probabilities[i] /= sum;
        }
        else
        {
            if (values.Any(v => v is < 0 or > 1) || Math.Abs(values.Sum(v => (double)v) - 1) > 0.0001) throw InvalidOutput();
            for (var i = 0; i < values.Length; i++) probabilities[i] = values[i];
        }
        var live = model.LiveClassIndex!.Value;
        var probability = probabilities[live];
        var uniqueLiveWinner = probabilities.Where((_, i) => i != live).All(p => probability > p);
        return new(model.Sha256.ToUpperInvariant(), probability, uniqueLiveWinner && probability >= model.MinimumLiveProbability!.Value);
    }

    private static Rect CropRectangle(FaceBounds bounds, int width, int height, SilentFaceModel model)
    {
        if (model.CropKind == SilentFaceCropKind.WholeImage) return new Rect(0, 0, width, height);
        // Adapted from Minivision CropImage._get_new_box (Apache-2.0, Copyright 2020 Minivision).
        // CompreFace's DTO width/height are max-min; reviewed adapter contract uses those extents.
        var scale = Math.Min(model.CropScale!.Value, Math.Min((height - 1d) / bounds.Height, (width - 1d) / bounds.Width));
        var cropWidth = bounds.Width * scale;
        var cropHeight = bounds.Height * scale;
        var centerX = bounds.XMin + bounds.Width / 2d;
        var centerY = bounds.YMin + bounds.Height / 2d;
        var left = centerX - cropWidth / 2;
        var top = centerY - cropHeight / 2;
        var right = centerX + cropWidth / 2;
        var bottom = centerY + cropHeight / 2;
        if (left < 0) { right -= left; left = 0; }
        if (top < 0) { bottom -= top; top = 0; }
        if (right > width - 1) { left -= right - width + 1; right = width - 1; }
        if (bottom > height - 1) { top -= bottom - height + 1; bottom = height - 1; }
        var x = (int)left;
        var y = (int)top;
        var w = (int)right - x + 1;
        var h = (int)bottom - y + 1;
        if (x < 0 || y < 0 || w < 1 || h < 1 || x + w > width || y + h > height) throw SelfHostedFaceImage.Invalid();
        return new Rect(x, y, w, h);
    }

    private static void ValidateMetadata(InferenceSession session, SilentFaceModel contract)
    {
        if (session.InputMetadata.Count != 1 || session.OutputMetadata.Count != 1 ||
            !session.InputMetadata.TryGetValue(contract.InputName, out var input) ||
            !session.OutputMetadata.TryGetValue(contract.OutputName, out var output) ||
            !input.IsTensor || input.ElementType != typeof(float) ||
            !input.Dimensions.SequenceEqual(new[] { 1, 3, contract.InputHeight, contract.InputWidth }) ||
            !output.IsTensor || output.ElementType != typeof(float) ||
            !output.Dimensions.SequenceEqual(new[] { 1, contract.ClassCount })) throw SilentFaceModelValidation.Unavailable();
    }

    private static AuthenticationFailure InvalidOutput() => new("face_liveness_invalid_result", 503);

    public void Dispose()
    {
        gate.Wait();
        try
        {
            if (disposed) return;
            disposed = true;
            foreach (var model in models) model.Session.Dispose();
        }
        finally { gate.Release(); }
    }

    private sealed record LoadedModel(SilentFaceModel Contract, InferenceSession Session);
}
