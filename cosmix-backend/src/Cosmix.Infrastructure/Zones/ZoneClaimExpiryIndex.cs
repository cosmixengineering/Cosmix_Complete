using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.Files;

namespace Cosmix.Infrastructure.Zones;

public sealed partial class EncryptedZoneClaimStore
{
    private const int MaximumIndexBytes = 2048;
    private readonly Dictionary<string, ExpiryScan> expiryScans = new(StringComparer.Ordinal);
    private sealed class ExpiryScan(IEnumerator<string> paths, DateTimeOffset cutoff, DateTimeOffset expires) : IDisposable
    {
        public IEnumerator<string> Paths { get; } = paths;
        public DateTimeOffset Cutoff { get; } = cutoff;
        public DateTimeOffset Expires { get; } = expires;
        public void Dispose() => Paths.Dispose();
    }
    private async Task WriteIndex(ZoneClaimIndexEntry entry, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
        if (bytes.Length > MaximumIndexBytes) throw Unavailable();
        var path = IndexPath(entry.ExpiresAtUtc, entry.EventId, createBucket: true);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        // This allocation index is durable before the existing encryption adapter creates .enc.
        // An incomplete index aborts ciphertext creation; no claim text is present here.
        await using (var stream = new FileStream(path, options))
        {
            PrivateFilePath.Require(path, false);
            await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); stream.Flush(flushToDisk: true);
        }
        var persisted = await File.ReadAllBytesAsync(path, ct);
        if (!CryptographicOperations.FixedTimeEquals(bytes, persisted)) throw Unavailable();
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        PrivateFilePath.Require(path, false);
    }
    private async Task<ZoneClaimIndexEntry> ReadIndex(string path, CancellationToken ct)
    {
        PrivateFilePath.Require(indexDirectory!, true); PrivateFilePath.Require(path, false);
        await using var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read,
            Share = FileShare.Read, Options = FileOptions.Asynchronous | FileOptions.SequentialScan });
        if (stream.Length is < 1 or > MaximumIndexBytes) throw Unavailable();
        var bytes = new byte[(int)stream.Length]; await stream.ReadExactlyAsync(bytes, ct);
        if (await stream.ReadAsync(new byte[1], ct) != 0 || stream.Length != bytes.Length) throw Unavailable();
        var entry = JsonSerializer.Deserialize<ZoneClaimIndexEntry>(bytes) ?? throw Unavailable();
        if (entry.EventId == Guid.Empty || entry.EmployeeId == Guid.Empty || entry.WindowId == Guid.Empty || entry.ExpiresAtUtc == default || entry.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            Path.GetFileName(path) != IndexName(entry.ExpiresAtUtc, entry.EventId) ||
            !string.Equals(path, IndexPath(entry.ExpiresAtUtc, entry.EventId), PrivateFilePath.Comparison)) throw Unavailable();
        _ = Parse(entry.Reference); return entry;
    }
    public async Task<ZoneClaimIndexPage> ScanExpiredAsync(DateTimeOffset now, string? afterCursor, int maximumEntries, CancellationToken ct)
    {
        Require();
        if (now == default || now.Offset != TimeSpan.Zero || maximumEntries is < 1 or > 500 || afterCursor is not null && !Canonical(afterCursor))
            throw new AuthenticationFailure("invalid_zone_claim_retention_scan", 400);
        await gate.WaitAsync(ct);
        try
        {
            using var lease = await AcquireStorageLease(ct);
            PrivateFilePath.Require(indexDirectory!, true);
            var actualNow = clock.GetUtcNow();
            foreach (var expired in expiryScans.Where(x => x.Value.Expires <= actualNow).Select(x => x.Key).ToArray())
            {
                expiryScans[expired].Dispose(); expiryScans.Remove(expired);
            }
            string cursor; ExpiryScan scan;
            if (afterCursor is null)
            {
                if (expiryScans.Count >= 4) throw Unavailable();
                cursor = Guid.NewGuid().ToString("N"); var cutoff = now < actualNow ? now : actualNow;
                scan = new(ExpiredIndexPaths(cutoff).GetEnumerator(), cutoff, actualNow.AddMinutes(10));
                expiryScans.Add(cursor, scan);
            }
            else if (!expiryScans.TryGetValue(afterCursor, out scan!))
                throw new AuthenticationFailure("zone_claim_retention_cursor_expired", 409);
            else cursor = afterCursor;
            var entries = new List<ZoneClaimIndexEntry>(); var invalid = 0;
            // Continue the same streaming enumerator: each candidate is visited once per sweep,
            // rather than sorting/rescanning the complete archive for every page. No claim text is cached.
            for (var examined = 0; examined < maximumEntries; examined++)
            {
                ct.ThrowIfCancellationRequested();
                if (!scan.Paths.MoveNext())
                {
                    scan.Dispose(); expiryScans.Remove(cursor);
                    return new(entries, null, invalid);
                }
                var path = scan.Paths.Current;
                if (!TryIndexName(Path.GetFileName(path), out var expiry)) { invalid++; continue; }
                if (expiry > scan.Cutoff || !File.Exists(path)) continue;
                try { entries.Add(await ReadIndex(path, ct)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { invalid++; }
            }
            return new(entries, cursor, invalid);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure error) when (error.Code == "zone_claim_retention_cursor_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { gate.Release(); }
    }
    private IEnumerable<string> ExpiredIndexPaths(DateTimeOffset cutoff)
    {
        var lastBucket = cutoff.UtcDateTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);
        foreach (var bucket in Directory.EnumerateDirectories(indexDirectory!, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(bucket);
            if (!DateTime.TryParseExact(name, "yyyyMMddHH", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw Unavailable();
            if (StringComparer.Ordinal.Compare(name, lastBucket) > 0) continue;
            PrivateFilePath.Require(bucket, true);
            foreach (var path in Directory.EnumerateFiles(bucket, "*", SearchOption.TopDirectoryOnly)) yield return path;
        }
        // Legacy flat allocations remain discoverable; new writes always use bounded hour buckets.
        foreach (var path in Directory.EnumerateFiles(indexDirectory!, "*", SearchOption.TopDirectoryOnly)) yield return path;
    }
    private string IndexPath(DateTimeOffset expires, Guid eventId, bool createBucket = false)
    {
        PrivateFilePath.Require(indexDirectory!, true);
        var legacy = Path.Combine(indexDirectory!, IndexName(expires, eventId));
        if (!createBucket && File.Exists(legacy)) return legacy;
        var bucket = Path.Combine(indexDirectory!, expires.UtcDateTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture));
        if (createBucket)
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(bucket);
            else Directory.CreateDirectory(bucket, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        if (Directory.Exists(bucket)) PrivateFilePath.Require(bucket, true);
        var path = Path.GetFullPath(Path.Combine(bucket, IndexName(expires, eventId)));
        if (!string.Equals(Path.GetDirectoryName(path), bucket, PrivateFilePath.Comparison) ||
            !string.Equals(Path.GetDirectoryName(bucket), indexDirectory, PrivateFilePath.Comparison)) throw Unavailable();
        return path;
    }
    private static string IndexName(DateTimeOffset expires, Guid eventId) => expires.UtcTicks.ToString("D19", CultureInfo.InvariantCulture) + "-" + eventId.ToString("N") + ".zci";
    private static bool TryIndexName(string name, out DateTimeOffset expires)
    {
        expires = default;
        if (name.Length != 56 || name[19] != '-' || !name.EndsWith(".zci", StringComparison.Ordinal) || !Canonical(name.Substring(20, 32)) ||
            !long.TryParse(name.AsSpan(0, 19), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0 || ticks > DateTimeOffset.MaxValue.UtcTicks) return false;
        expires = new(ticks, TimeSpan.Zero);
        return name == IndexName(expires, Guid.ParseExact(name.Substring(20, 32), "N"));
    }
    private void DeleteIndex(string path)
    {
        PrivateFilePath.Require(indexDirectory!, true);
        if (!File.Exists(path)) return;
        PrivateFilePath.Require(path, false);
        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); File.Delete(path);
    }
    public void Dispose()
    {
        foreach (var scan in expiryScans.Values) scan.Dispose();
        expiryScans.Clear(); gate.Dispose();
    }
}
