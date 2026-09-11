using System.Buffers.Binary;

namespace Ter22.Core;

public static class JpegGuard
{
    // Bound the decoded dimensions before handing remote data to Windows GDI+.
    public static void Validate(ReadOnlySpan<byte> bytes, int width, int height)
    {
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8)
            throw new InvalidDataException("Format JPEG invalid.");
        int offset = 2;
        while (offset < bytes.Length - 3)
        {
            if (bytes[offset++] != 0xff) throw new InvalidDataException("Marker JPEG invalid.");
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) break;
            byte marker = bytes[offset++];
            if (marker is 0xda or 0xd9) break;
            if (offset + 2 > bytes.Length) break;
            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
            if (length < 2 || length > bytes.Length - offset) throw new InvalidDataException("Segment JPEG invalid.");
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8 || bytes[offset + 2] != 8
                    || BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..]) != height
                    || BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]) != width)
                    throw new InvalidDataException("Dimensiunile JPEG nu corespund cadrului.");
                if (width is < 1 or > 2560 || height is < 1 or > 1600)
                    throw new InvalidDataException("Imagine prea mare.");
                return;
            }
            offset += length;
        }
        throw new InvalidDataException("JPEG fără dimensiuni acceptate.");
    }
}
