using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication.Providers;
using Microsoft.Extensions.Options;

namespace Cosmix.Infrastructure.Authentication.Providers;

public sealed class OtpProtector : IOtpProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly IPhoneNumberNormalizer _normalizer;
    private readonly TimeProvider _timeProvider;
    private readonly string _keyVersion;
    private readonly int _maxLifetimeSeconds;
    private readonly byte[] _verifierKey;
    private readonly byte[] _encryptionKey;

    public OtpProtector(IOptions<OtpCryptoOptions> options, IPhoneNumberNormalizer normalizer, TimeProvider timeProvider)
    {
        _normalizer = normalizer;
        _timeProvider = timeProvider;
        var settings = options.Value;
        _keyVersion = settings.KeyVersion ?? string.Empty;
        _maxLifetimeSeconds = settings.MaxDeliveryLifetimeSeconds;
        _verifierKey = DecodeKey(settings.VerifierKeyBase64);
        _encryptionKey = DecodeKey(settings.EncryptionKeyBase64);
        IsConfigured = settings.Enabled && AesGcm.IsSupported &&
            _keyVersion.Length is >= 1 and <= 32 &&
            _keyVersion.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
            _maxLifetimeSeconds is >= 1 and <= 3600 &&
            _verifierKey.Length is >= 32 and <= 128 && _encryptionKey.Length == 32 &&
            !CryptographicOperations.FixedTimeEquals(_verifierKey, _encryptionKey);
    }

    public bool IsConfigured { get; }

    public string GenerateCode()
    {
        EnsureConfigured();
        return RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public string ComputeVerifier(Guid challengeId, string code)
    {
        EnsureConfigured();
        ValidateProof(challengeId, code);
        return $"{_keyVersion}:{Convert.ToHexString(ComputeMac($"otp-verifier:v1:{_keyVersion}:{challengeId:N}:{code}"))}";
    }

    public bool Verify(Guid challengeId, string code, string verifier)
    {
        if (!IsConfigured || challengeId == Guid.Empty || !IsCode(code) ||
            verifier is null || verifier.Length != _keyVersion.Length + 65 ||
            !verifier.StartsWith(_keyVersion + ":", StringComparison.Ordinal))
            return false;

        Span<byte> supplied = stackalloc byte[32];
        if (Convert.FromHexString(verifier.AsSpan(_keyVersion.Length + 1), supplied, out _, out var written) != OperationStatus.Done || written != 32)
            return false;
        var expected = ComputeMac($"otp-verifier:v1:{_keyVersion}:{challengeId:N}:{code}");
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    public string ProtectDelivery(string phone, string code, DateTimeOffset expiresAt)
    {
        EnsureConfigured();
        if (!IsCode(code))
            throw new ArgumentException("A six-digit code is required.", nameof(code));
        var recipient = _normalizer.Normalize(phone);
        var issuedAt = _timeProvider.GetUtcNow();
        if (expiresAt <= issuedAt || expiresAt - issuedAt > TimeSpan.FromSeconds(_maxLifetimeSeconds))
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Delivery expiry is outside the configured lifetime.");

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(new Delivery(recipient, code, issuedAt, expiresAt));
        try
        {
            var envelope = new byte[NonceSize + TagSize + plaintext.Length];
            RandomNumberGenerator.Fill(envelope.AsSpan(0, NonceSize));
            using var cipher = new AesGcm(_encryptionKey, TagSize);
            cipher.Encrypt(envelope.AsSpan(0, NonceSize), plaintext, envelope.AsSpan(NonceSize + TagSize),
                envelope.AsSpan(NonceSize, TagSize), AssociatedData());
            return $"1.{_keyVersion}.{Convert.ToBase64String(envelope)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public (string Phone, string Code, DateTimeOffset ExpiresAt) UnprotectDelivery(string payload)
    {
        EnsureConfigured();
        var prefix = $"1.{_keyVersion}.";
        if (string.IsNullOrEmpty(payload) || payload.Length > 2048 || !payload.StartsWith(prefix, StringComparison.Ordinal))
            throw InvalidPayload();

        byte[] plaintext = [];
        try
        {
            var envelope = Convert.FromBase64String(payload[prefix.Length..]);
            if (envelope.Length <= NonceSize + TagSize)
                throw InvalidPayload();
            plaintext = new byte[envelope.Length - NonceSize - TagSize];
            using var cipher = new AesGcm(_encryptionKey, TagSize);
            cipher.Decrypt(envelope.AsSpan(0, NonceSize), envelope.AsSpan(NonceSize + TagSize),
                envelope.AsSpan(NonceSize, TagSize), plaintext, AssociatedData());
            var delivery = JsonSerializer.Deserialize<Delivery>(plaintext);
            var now = _timeProvider.GetUtcNow();
            if (delivery is null || !IsCode(delivery.Code) ||
                delivery.IssuedAt > now || delivery.ExpiresAt <= now || delivery.ExpiresAt <= delivery.IssuedAt ||
                delivery.ExpiresAt - delivery.IssuedAt > TimeSpan.FromSeconds(_maxLifetimeSeconds) ||
                _normalizer.Normalize(delivery.Phone) != delivery.Phone)
                throw InvalidPayload();
            return (delivery.Phone, delivery.Code, delivery.ExpiresAt);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException or ArgumentException)
        {
            // Never attach parser/crypto exceptions that could carry delivery contents.
            throw InvalidPayload();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Digest(string value)
    {
        EnsureConfigured();
        if (string.IsNullOrEmpty(value) || value.Length > 4096)
            throw new ArgumentException("A bounded nonempty value is required.", nameof(value));
        return Convert.ToHexString(ComputeMac($"digest:v1:{_keyVersion}:{value}"));
    }

    public string NewRefreshToken()
    {
        EnsureConfigured();
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private byte[] ComputeMac(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try { return HMACSHA256.HashData(_verifierKey, bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private byte[] AssociatedData() => Encoding.UTF8.GetBytes($"Cosmix:otp-delivery:1:{_keyVersion}");

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Mobile authentication cryptography is unavailable.");
    }

    private static byte[] DecodeKey(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 256)
            return [];
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { return []; }
    }

    internal static bool IsCode(string? code) => code is { Length: 6 } && code.All(char.IsAsciiDigit);

    private static void ValidateProof(Guid challengeId, string code)
    {
        if (challengeId == Guid.Empty)
            throw new ArgumentException("A challenge identifier is required.", nameof(challengeId));
        if (!IsCode(code))
            throw new ArgumentException("A six-digit code is required.", nameof(code));
    }

    private static CryptographicException InvalidPayload() => new("Delivery payload is invalid or expired.");

    private sealed record Delivery(string Phone, string Code, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}
