using Microsoft.AspNetCore.Http;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Server-side validation for user-uploaded files whose only prior check was client-side
    /// (an &lt;input accept="application/pdf"&gt; attribute restricts the file picker, not the
    /// bytes a client actually sends -- it's not a security control). Add more checks here as
    /// more upload paths need them; currently just the PDF magic-byte signature.
    /// </summary>
    public static class FileValidationHelpers
    {
        // "%PDF-" -- the first five bytes of every valid PDF file, regardless of version.
        private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };

        public static async Task<bool> IsPdfAsync(IFormFile file, CancellationToken cancellationToken)
        {
            if (file.Length < PdfMagicBytes.Length) return false;

            var buffer = new byte[PdfMagicBytes.Length];
            var totalRead = 0;
            await using var stream = file.OpenReadStream();
            // ReadAsync is not guaranteed to fill the buffer in one call even when enough bytes
            // are available -- that's a real stream contract, not a hypothetical -- so this has
            // to loop until the buffer is full or the stream genuinely ends.
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
                if (read == 0) break;
                totalRead += read;
            }
            if (totalRead < PdfMagicBytes.Length) return false;

            return buffer.AsSpan().SequenceEqual(PdfMagicBytes);
        }
    }
}
