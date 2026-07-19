using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Strips unpaired UTF-16 surrogate code units from free-text user input (review comments,
    /// author names). A lone surrogate is technically a valid C# `string` (it's just a char), so it
    /// stores and round-trips through SQL Server's NVARCHAR fine — but System.Text.Json throws when
    /// transcoding it to UTF-8 on the way out, which turns one bad character in one old review into
    /// a 500 on every read of that doctor's whole review list. Applied on write (to stop new bad
    /// data) and on read (to tolerate whatever's already stored).
    /// </summary>
    public static class TextSanitizer
    {
        public static string? StripInvalidSurrogates(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            StringBuilder? sb = null;
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    sb?.Append(c).Append(input[i + 1]);
                    i++;
                    continue;
                }
                if (char.IsSurrogate(c))
                {
                    // Lone/unpaired surrogate — drop it. First time we hit one, backfill everything
                    // seen so far since the common case (no bad chars) never allocates a builder.
                    sb ??= new StringBuilder(input, 0, i, input.Length);
                    continue;
                }
                sb?.Append(c);
            }
            return sb?.ToString() ?? input;
        }
    }
}
