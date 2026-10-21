using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Infrastructure.Files;

/// <summary>Clamd INSTREAM over a configured trusted socket. No network access occurs during construction.</summary>
public sealed class ClamAvEmployeeFileScanner : IEmployeeFileScanner
{
    private const int ChunkSize = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex VersionFormat = new(
        @"\AClamAV [0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?/[0-9]+/[ -~]+\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    private readonly ClamAvScannerOptions options;
    private readonly IPAddress? address;

    public ClamAvEmployeeFileScanner(ClamAvScannerOptions options)
    {
        this.options = options ?? new ClamAvScannerOptions();
        options = this.options;
        if (!options.Enabled) return;
        if (!IPAddress.TryParse(options.Host, out var parsed) || options.Port is < 1 or > 65535 ||
            options.TimeoutSeconds is < 1 or > 120 || options.MaxResponseBytes is < 128 or > 16384 ||
            options.MaxSizeBytes is < 1 or > PrivateFileStorageOptions.MaximumAllowedSizeBytes)
            return;
        var candidate = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
        if (IPAddress.IsLoopback(candidate) || (options.AllowPrivateNetwork && IsPrivateNetwork(candidate))) address = candidate;
    }

    public async Task<FileScanResult> ScanAsync(byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!options.Enabled) return Failed("clamav/disabled");
        if (address is null) return Failed("clamav/not-configured");
        if (content is null || content.Length < 1 || content.Length > options.MaxSizeBytes) return Failed("clamav/invalid-size");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var version = "clamav/unavailable";
        var bytes = content.ToArray();
        try
        {
            // Each non-session command owns a connection and must receive exactly one record followed by EOF.
            // https://docs.clamav.net/manual/Usage/ClamdProtocol.html
            using (var client = await ConnectAsync(deadline.Token))
            {
                var stream = client.GetStream();
                await stream.WriteAsync("zVERSION\0"u8.ToArray(), deadline.Token);
                var reported = await ReadReplyAsync(stream, deadline.Token);
                if (reported.Length > 150 || !VersionFormat.IsMatch(reported)) return Failed(version);
                version = reported;
            }
            using (var client = await ConnectAsync(deadline.Token))
            {
                var stream = client.GetStream();
                await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), deadline.Token);
                var header = new byte[4];
                for (var offset = 0; offset < bytes.Length; offset += ChunkSize)
                {
                    var count = Math.Min(ChunkSize, bytes.Length - offset);
                    BinaryPrimitives.WriteUInt32BigEndian(header, (uint)count);
                    await stream.WriteAsync(header, deadline.Token);
                    await stream.WriteAsync(bytes.AsMemory(offset, count), deadline.Token);
                }
                Array.Clear(header);
                await stream.WriteAsync(header, deadline.Token);
                var reply = await ReadReplyAsync(stream, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (reply == "stream: OK") return new(DocumentScanStatus.Clean, version);
                if (reply.StartsWith("stream: ", StringComparison.Ordinal) && reply.EndsWith(" FOUND", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(reply[8..^6]))
                    return new(DocumentScanStatus.Rejected, version);
                return Failed(version);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failed(version); }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            ct.ThrowIfCancellationRequested();
            return Failed(version);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task<TcpClient> ConnectAsync(CancellationToken ct)
    {
        var client = new TcpClient(address!.AddressFamily) { NoDelay = true };
        try { await client.ConnectAsync(address, options.Port, ct); return client; }
        catch { client.Dispose(); throw; }
    }

    private async Task<string> ReadReplyAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[options.MaxResponseBytes + 1];
        var length = 0;
        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(length), ct);
            if (count == 0) break;
            length += count;
            if (length > options.MaxResponseBytes) throw new IOException("Scanner response exceeds the configured bound.");
        }
        if (length < 2 || buffer[length - 1] != 0)
            throw new IOException("Scanner response is incomplete.");
        var reply = StrictUtf8.GetString(buffer, 0, length - 1);
        if (reply.Any(char.IsControl)) throw new IOException("Scanner returned an invalid or multiple-record response.");
        return reply;
    }

    private static FileScanResult Failed(string version) => new(DocumentScanStatus.Failed, version);

    private static bool IsPrivateNetwork(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                                         (bytes[0] == 192 && bytes[1] == 168),
            AddressFamily.InterNetworkV6 => (bytes[0] & 0xfe) == 0xfc,
            _ => false
        };
    }
}
