using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using Ter22.Core;

namespace Ter22.Windows.Desktop;

internal interface IScreenCapture
{
    DisplayLayout GetLayout();
    byte[] Capture(FrameRequest request, DisplayLayout layout);
}

internal sealed class ScreenCapture : IScreenCapture
{
    private static readonly ImageCodecInfo Jpeg = ImageCodecInfo.GetImageEncoders().Single(c => c.FormatID == ImageFormat.Jpeg.Guid);
    public DisplayLayout GetLayout()
    {
        var screens = Screen.AllScreens.OrderByDescending(s => s.Primary).ThenBy(s => s.DeviceName, StringComparer.Ordinal).ToArray();
        if (screens.Length == 0) throw new IOException("Nu există niciun monitor disponibil.");
        var displays = screens.Select((s, i) => new DisplayInfo(s.DeviceName,
            $"Monitor {i + 1}{(s.Primary ? " (principal)" : "")}", s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)).ToList();
        Rectangle all = screens.Select(s => s.Bounds).Aggregate(Rectangle.Union);
        displays.Add(new DisplayInfo("*", "Toate monitoarele", all.X, all.Y, all.Width, all.Height));
        string revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", displays.Select(d => $"{d.Id}:{d.X}:{d.Y}:{d.Width}:{d.Height}")))));
        var layout = new DisplayLayout(revision, displays.ToArray());
        layout.Validate(); return layout;
    }

    public byte[] Capture(FrameRequest request, DisplayLayout layout)
    {
        NativeInput.EnsureInteractiveDesktop();
        var display = layout.Displays.SingleOrDefault(d => d.Id == request.DisplayId)
            ?? throw new InvalidDataException("Monitorul nu mai este disponibil.");
        if ((long)display.Width * display.Height > 34_000_000)
            throw new InvalidDataException("Suprafața depășește limita de 34 megapixeli. Selectează un singur monitor.");
        using var bitmap = new Bitmap(display.Width, display.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            foreach (var physical in layout.Displays.Where(d => d.Id != "*" && (display.Id == "*" || d.Id == display.Id)))
                NativeInput.CaptureRegion(graphics, physical, display.X, display.Y);
            NativeInput.DrawCursor(graphics, display.X, display.Y);
        }
        double factor = Math.Min(1d, Math.Min(2560d / display.Width, 1600d / display.Height));
        int width = Math.Max(1, (int)Math.Round(display.Width * factor));
        int height = Math.Max(1, (int)Math.Round(display.Height * factor));
        using var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height));
        }
        using var buffer = new MemoryStream();
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
        scaled.Save(buffer, Jpeg, parameters);
        return Wire.PackFrame(new FrameHeader(request.ViewId, display.Id, layout.Revision,
            display.X, display.Y, display.Width, display.Height, width, height), buffer.ToArray());
    }
}
