using System.Buffers.Binary;
using System.Text.Json;

namespace Ter22.Core;

public enum Kind : byte
{
    Auth = 1, Accepted, Displays, FrameRequest, Frame, Input, Release,
    Ping, Pong, Error, RelayHello, RelayReady
}

public sealed record Packet(Kind Kind, byte[] Payload)
{
    public T Json<T>() => JsonSerializer.Deserialize<T>(Payload, Wire.JsonOptions)
        ?? throw new InvalidDataException("Mesaj incomplet.");
}

public static class Wire
{
    public const int Version = 1;
    public const int MaxPacket = 8 * 1024 * 1024;
    public static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 8 };

    public static byte[] Json<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    // A single reader and a single writer own each stream. Writers are serialized by the caller.
    public static async Task WriteAsync(Stream stream, Kind kind, ReadOnlyMemory<byte> payload,
        CancellationToken ct)
    {
        if (payload.Length > MaxPacket - 1) throw new InvalidDataException("Mesaj prea mare.");
        byte[] header = new byte[5];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length + 1);
        header[4] = (byte)kind;
        await stream.WriteAsync(header, ct);
        if (!payload.IsEmpty) await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<Packet> ReadAsync(Stream stream, CancellationToken ct,
        int maximum = MaxPacket)
    {
        byte[] header = new byte[5];
        await stream.ReadExactlyAsync(header, ct);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 1 || length > maximum || !Enum.IsDefined((Kind)header[4]))
            throw new InvalidDataException("Antet invalid.");
        byte[] payload = new byte[length - 1];
        await stream.ReadExactlyAsync(payload, ct);
        return new Packet((Kind)header[4], payload);
    }

    public static byte[] PackFrame(FrameHeader frame, byte[] jpeg)
    {
        byte[] metadata = Json(frame);
        if (metadata.Length > 4096 || jpeg.Length > MaxPacket - 5 - metadata.Length)
            throw new InvalidDataException("Cadru prea mare.");
        byte[] result = new byte[4 + metadata.Length + jpeg.Length];
        BinaryPrimitives.WriteInt32BigEndian(result, metadata.Length);
        metadata.CopyTo(result, 4);
        jpeg.CopyTo(result, 4 + metadata.Length);
        return result;
    }

    public static (FrameHeader Header, byte[] Jpeg) UnpackFrame(byte[] payload)
    {
        if (payload.Length < 8) throw new InvalidDataException("Cadru incomplet.");
        int n = BinaryPrimitives.ReadInt32BigEndian(payload);
        if (n < 1 || n > 4096 || n >= payload.Length - 4)
            throw new InvalidDataException("Metadate cadru invalide.");
        var frame = JsonSerializer.Deserialize<FrameHeader>(payload.AsSpan(4, n), JsonOptions)
            ?? throw new InvalidDataException("Metadate cadru absente.");
        frame.Validate();
        return (frame, payload[(4 + n)..]);
    }
}

public sealed record Authentication(int Version, string Token);
public sealed record DisplayInfo(string Id, string Name, int X, int Y, int Width, int Height)
{
    public override string ToString() => $"{Name} · {Width}×{Height}";
    public bool Contains(int x, int y) => x >= X && y >= Y && (long)x < (long)X + Width && (long)y < (long)Y + Height;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 || string.IsNullOrWhiteSpace(Name)
            || Name.Length > 128 || Width is < 1 or > 32768 || Height is < 1 or > 32768
            || X is < -65536 or > 65536 || Y is < -65536 or > 65536)
            throw new InvalidDataException("Geometrie monitor invalidă.");
    }
}
public sealed record DisplayLayout(string Revision, DisplayInfo[] Displays)
{
    public void Validate()
    {
        if (string.IsNullOrEmpty(Revision) || Revision.Length > 64 || Displays is null || Displays.Length is < 1 or > 32)
            throw new InvalidDataException("Configurație monitoare invalidă.");
        foreach (var display in Displays) display.Validate();
        if (Displays.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != Displays.Length)
            throw new InvalidDataException("Identificatori monitoare duplicați.");
    }
}
public sealed record FrameRequest(Guid ViewId, string DisplayId);
public sealed record FrameHeader(Guid ViewId, string DisplayId, string Revision,
    int X, int Y, int Width, int Height, int ImageWidth, int ImageHeight)
{
    public void Validate()
    {
        new DisplayInfo(DisplayId, "Frame", X, Y, Width, Height).Validate();
        if (ViewId == Guid.Empty || string.IsNullOrEmpty(Revision) || Revision.Length > 64
            || ImageWidth is < 1 or > 2560 || ImageHeight is < 1 or > 1600)
            throw new InvalidDataException("Dimensiuni imagine invalide.");
    }
}
public enum InputAction { Move, LeftDown, LeftUp, RightDown, RightUp, MiddleDown, MiddleUp, Wheel, KeyDown, KeyUp }
public sealed record RemoteInput(InputAction Action, string DisplayId, string Revision, int X, int Y, int Value);
public sealed record RelayHello(int Version, string Key, string Route, bool Host);

public static class Geometry
{
    public static (int X, int Y) Normalize(int x, int y, DisplayInfo desktop) =>
        ((int)Math.Clamp(Math.Round(((double)x - desktop.X) * 65535 / Math.Max(1, desktop.Width - 1)), 0, 65535),
         (int)Math.Clamp(Math.Round(((double)y - desktop.Y) * 65535 / Math.Max(1, desktop.Height - 1)), 0, 65535));

    // Map a letterboxed preview to physical remote pixels; black margins never send input.
    public static bool TryMap(double x, double y, int viewWidth, int viewHeight,
        FrameHeader frame, out int remoteX, out int remoteY)
    {
        remoteX = remoteY = 0;
        if (!double.IsFinite(x) || !double.IsFinite(y) || viewWidth <= 0 || viewHeight <= 0) return false;
        double scale = Math.Min((double)viewWidth / frame.ImageWidth, (double)viewHeight / frame.ImageHeight);
        double w = frame.ImageWidth * scale, h = frame.ImageHeight * scale;
        double left = (viewWidth - w) / 2, top = (viewHeight - h) / 2;
        if (x < left || y < top || x >= left + w || y >= top + h) return false;
        remoteX = frame.X + Math.Min(frame.Width - 1, (int)((x - left) / w * frame.Width));
        remoteY = frame.Y + Math.Min(frame.Height - 1, (int)((y - top) / h * frame.Height));
        return true;
    }
}
