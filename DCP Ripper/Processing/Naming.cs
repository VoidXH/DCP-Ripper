using System;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Naming convention helper functions.
    /// </summary>
    public static class Naming {
        /// <summary>
        /// Converts a float to the format required by FFmpeg.
        /// </summary>
        public static string ToFFmpegNumber(this float x) => x.ToString("0.000").Replace(',', '.');

        /// <summary>
        /// Changes the extension in a file name.
        /// </summary>
        public static string ChangeExtension(this string of, string oldExtension, string newExtension) {
            if (of.Length >= oldExtension.Length &&
                of[^oldExtension.Length..].Equals(oldExtension, StringComparison.InvariantCultureIgnoreCase))
                return of[..^oldExtension.Length] + newExtension;
            return of;
        }
    }
}