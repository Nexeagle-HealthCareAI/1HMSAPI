namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// What a Health Wiki image may be. The type is decided by looking at the first bytes of the file, never by its name or the
    /// content type the browser claims, so a renamed document or script cannot be stored as an image.
    /// </summary>
    public static class HealthWikiImageRules
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const int HeaderBytes = 12;
        // Per person per day. A writer adding images to articles never gets near this; a script would.
        public const int MaxUploadsPerDay = 50;

        public const string Jpeg = "image/jpeg";
        public const string Png = "image/png";
        public const string WebP = "image/webp";

        /// <summary>The real type and the file extension to store it under, or null when it is not a JPG, PNG or WebP image.</summary>
        public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return (Jpeg, "jpg");
            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A) return (Png, "png");
            // "RIFF" <4 bytes size> "WEBP"
            if (header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return (WebP, "webp");
            return null;
        }

        /// <summary>A random, unguessable key. The uploader's file name is never used.</summary>
        public static string NewKey(string extension) => $"articles/{Guid.NewGuid():N}.{extension}";

        public static string PublicUrl(string baseUrl, string key) => $"{baseUrl.TrimEnd('/')}/{key}";
    }
}
