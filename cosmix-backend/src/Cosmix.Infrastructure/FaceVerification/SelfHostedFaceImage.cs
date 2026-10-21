using System.Buffers.Binary;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

public readonly record struct FaceBounds(int XMin, int YMin, int XMax, int YMax)
{
    public int Width => XMax - XMin;
    public int Height => YMax - YMin;
    public bool IsWithin(int width, int height) => XMin >= 0 && YMin >= 0 && XMax > XMin &&
        YMax > YMin && XMax <= width && YMax <= height;
}

public sealed record SelfHostedFaceImageInfo(int Width, int Height, string MediaType, string Extension);

/// <summary>Limits encoded input before any native decoding. Only orientation-free JPEG and opaque RGB PNG.</summary>
public static class SelfHostedFaceImage
{
    public const int MaximumBytes = 1048576;
    public const int MaximumDimension = 4096;
    public const int MaximumPixels = 4194304;

    public static SelfHostedFaceImageInfo Inspect(byte[] image)
    {
        if (image is not { Length: >= 24 and <= MaximumBytes }) throw Invalid();
        ReadOnlySpan<byte> data = image;
        if (data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            if (data.Length < 45 || !data.Slice(12, 4).SequenceEqual("IHDR"u8) ||
                BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 ||
                data[24] != 8 || data[25] != 2 || data[26] != 0 || data[27] != 0 || data[28] != 0) throw Invalid();
            var width = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
            var height = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
            ValidateSize(width, height);
            var offset = 8;
            var sawData = false;
            while (offset <= data.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                if (length > data.Length - offset - 12) throw Invalid();
                var kind = data.Slice(offset + 4, 4);
                if (kind.SequenceEqual("acTL"u8) || kind.SequenceEqual("tRNS"u8) || kind.SequenceEqual("eXIf"u8) ||
                    offset != 8 && kind.SequenceEqual("IHDR"u8)) throw Invalid();
                if (kind.SequenceEqual("IDAT"u8)) sawData = true;
                offset += (int)length + 12;
                if (kind.SequenceEqual("IEND"u8))
                {
                    if (length != 0 || !sawData || offset != data.Length) throw Invalid();
                    return new((int)width, (int)height, "image/png", ".png");
                }
            }
            throw Invalid();
        }
        if (data[0] != 255 || data[1] != 216 || data[^2] != 255 || data[^1] != 217) throw Invalid();
        var position = 2;
        int jpegWidth = 0, jpegHeight = 0;
        while (position < data.Length - 2)
        {
            if (data[position++] != 255) throw Invalid();
            while (position < data.Length && data[position] == 255) position++;
            if (position >= data.Length) throw Invalid();
            var marker = data[position++];
            if (marker == 218)
            {
                if (jpegWidth == 0) throw Invalid();
                return new(jpegWidth, jpegHeight, "image/jpeg", ".jpg");
            }
            // APP1 can contain EXIF orientation; require server-owned capture to canonicalize it first.
            if (marker is 0 or 216 or 217 or 225 || position > data.Length - 2) throw Invalid();
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
            if (segmentLength < 2 || segmentLength > data.Length - position) throw Invalid();
            if (marker is 192 or 194)
            {
                if (jpegWidth != 0 || segmentLength < 8 || data[position + 2] != 8 || data[position + 7] != 3) throw Invalid();
                jpegHeight = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 3)..]);
                jpegWidth = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]);
                ValidateSize((uint)jpegWidth, (uint)jpegHeight);
            }
            else if (marker is >= 193 and <= 207 && marker is not (196 or 200 or 204)) throw Invalid();
            position += segmentLength;
        }
        throw Invalid();
    }

    private static void ValidateSize(uint width, uint height)
    {
        if (width is < 2 or > MaximumDimension || height is < 2 or > MaximumDimension ||
            (ulong)width * height > MaximumPixels) throw Invalid();
    }

    internal static AuthenticationFailure Invalid() => new("face_image_invalid", 503);
}
