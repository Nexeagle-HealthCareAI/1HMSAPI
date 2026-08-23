using System.Reflection;
using QRCoder;
using SkiaSharp;

namespace EasyHMSAPI.Application.Services
{
    // Renders the OPD check-in QR (the poster hospitals print and display) with the NexEagle
    // eagle mark composited at the center. QRCoder's PngByteQRCode is pure-managed (no
    // System.Drawing dependency) so the plain QR renders safely on Linux; SkiaSharp handles the
    // logo compositing for the same reason -- both are safe inside the Docker image this API
    // actually deploys to (see SkiaSharp.NativeAssets.Linux.NoDependencies in the .csproj).
    public static class QrCodeGenerator
    {
        // A logo covering roughly this fraction of the QR's width is the standard safe ceiling
        // for reliable scanning at ECCLevel.H (~30% recoverable data) -- much beyond this and
        // scanners start failing even with maximum error correction.
        private const float LogoSizeFraction = 0.20f;
        private const int PixelsPerModule = 20; // print-quality resolution, not screen-only

        private static readonly Lazy<byte[]> LogoBytes = new(LoadEmbeddedLogo);

        public static byte[] GenerateWithLogo(string data)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.H);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var plainQrBytes = pngQrCode.GetGraphic(PixelsPerModule);

            return CompositeLogo(plainQrBytes, LogoBytes.Value);
        }

        private static byte[] CompositeLogo(byte[] qrPngBytes, byte[] logoPngBytes)
        {
            using var qrBitmap = SKBitmap.Decode(qrPngBytes);
            using var logoBitmap = SKBitmap.Decode(logoPngBytes);

            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

            using var surface = SKSurface.Create(new SKImageInfo(qrBitmap.Width, qrBitmap.Height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(qrBitmap, SKRect.Create(qrBitmap.Width, qrBitmap.Height), sampling);

            var logoSize = qrBitmap.Width * LogoSizeFraction;
            var logoRect = SKRect.Create(
                (qrBitmap.Width - logoSize) / 2, (qrBitmap.Height - logoSize) / 2, logoSize, logoSize);

            // White backing plate behind the logo, slightly larger than it -- without this the
            // logo's own transparent/light pixels would blend straight into the QR's dark
            // modules right at its edge instead of reading as a clean, separate mark.
            var plateMargin = logoSize * 0.12f;
            var plateRect = new SKRect(
                logoRect.Left - plateMargin, logoRect.Top - plateMargin,
                logoRect.Right + plateMargin, logoRect.Bottom + plateMargin);
            using var platePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawRoundRect(plateRect, plateMargin, plateMargin, platePaint);

            canvas.DrawBitmap(logoBitmap, logoRect, sampling);

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        private static byte[] LoadEmbeddedLogo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "EasyHMSAPI.Application.Resources.nexeagle-logo.png";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
