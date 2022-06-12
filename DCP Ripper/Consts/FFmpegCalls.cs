using DCP_Ripper.Processing;
using DCP_Ripper.Properties;
using System.Linq;

namespace DCP_Ripper.Consts {
    /// <summary>
    /// Arguments to call FFmpeg with.
    /// </summary>
    public static class FFmpegCalls {
        /// <summary>
        /// Precedes the filter list.
        /// </summary>
        const string videoFilterTag = "-vf ";

        /// <summary>
        /// Applies the selected audio codec on an audio file.
        /// </summary>
        public static string ApplyCodec(string oldName, string newName) {
            const string args = "-i \"{0}\" -c:a {1} -v error -stats \"{2}\"";
            return string.Format(args, oldName, Settings.Default.audio, newName);
        }

        /// <summary>
        /// Converts the audio track to 24-bit PCM.
        /// </summary>
        public static string AudioToPCM(Reel content, string outputFile) {
            const string args = "-i \"{0}\" -ss {1} -t {2} -c:a pcm_s24le -v error -stats \"{3}\"";
            return string.Format(args,
                content.audioFile,
                (content.audioStartFrame / content.framerate).ToFFmpegNumber(),
                (content.duration / content.framerate).ToFFmpegNumber(),
                outputFile);
        }

        /// <summary>
        /// Converts the audio track to the selected codec.
        /// </summary>
        public static string AudioToSelectedCodec(Reel content, string outputFile) {
            const string args = "-i \"{0}\" -ss {1} -t {2} -c:a {3} -v error -stats \"{4}\"";
            return string.Format(args,
                content.audioFile,
                (content.audioStartFrame / content.framerate).ToFFmpegNumber(),
                (content.duration / content.framerate).ToFFmpegNumber(),
                Settings.Default.audio,
                outputFile);
        }

        /// <summary>
        /// Processes the raw interop 3D stream.
        /// </summary>
        public static string Interop3D(Reel content, string outputFile) {
            const string args = "{0} -ss {1} -i \"{2}\" -t {3} -c:v {4} -crf {5} -v error -stats \"{6}\"";
            return string.Format(args,
                "-r " + (content.framerate * 2).ToFFmpegNumber(), // Set framerate to double: DCP 3D is interop (altering frames)
                (content.videoStartFrame / content.framerate).ToFFmpegNumber(),
                content.videoFile,
                (content.duration / content.framerate).ToFFmpegNumber(),
                Settings.Default.format.StartsWith("x265") ? "libx265" : "libx264",
                Settings.Default.crf,
                outputFile);
        }

        /// <summary>
        /// Combines left and right eyes for an SBS or OU 3D output.
        /// </summary>
        public static string Merge3D(string leftFile, string rightFile, bool sbs, string outputFile, params string[] filters) {
            const string args = "-i \"{0}\" -i \"{1}\" -filter_complex" +
                " [0:v][1:v]{2}stack=inputs=2[v] -map [v] -c:v {3} {4} {5}" +
                " -crf {6} -v error -stats \"{7}\"";
            return string.Format(args,
                leftFile,
                rightFile,
                sbs ? 'h' : 'v',
                Settings.Default.format.StartsWith("x265") ? "libx265" : "libx264",
                Settings.Default.format.Contains("420") ? "-pix_fmt yuv420p" : string.Empty,
                JoinFilters(filters),
                Settings.Default.crf3d,
                outputFile);
        }

        /// <summary>
        /// Processes one eye's content from a 3D stream.
        /// </summary>
        public static string SingleEye3D(Reel content, string outputFile, int crf, bool leftEye, bool halfSize, bool sbs,
            string extraFilters) {
            const string args = "{0} -ss {1} -i \"{2}\" -t {3} {4} -c:v {5} {6} -crf {7} -v error -stats \"{8}\"";
            return string.Format(args,
                "-r " + (content.framerate * 2).ToFFmpegNumber(), // Set framerate to double: DCP 3D is interop (altering frames)
                (content.videoStartFrame / content.framerate).ToFFmpegNumber(),
                content.videoFile,
                (content.duration / content.framerate).ToFFmpegNumber(),
                JoinFilters(EyeFilters(leftEye, halfSize, sbs), extraFilters),
                Settings.Default.format.StartsWith("x265") ? "libx265" : "libx264",
                Settings.Default.format.Contains("420") ? "-pix_fmt yuv420p" : string.Empty,
                crf,
                outputFile);
        }

        /// <summary>
        /// Converts the video track to the selected audio codec.
        /// </summary>
        public static string VideoToSelectedCodec(Reel content, string outputFile, params string[] filters) {
            const string args = "-ss {0} -i \"{1}\" -t {2} -c:v {3} {4} {5} -crf {6} -v error -stats \"{7}\"";
            return string.Format(args,
                (content.videoStartFrame / content.framerate).ToFFmpegNumber(),
                content.videoFile,
                (content.duration / content.framerate).ToFFmpegNumber(),
                Settings.Default.format.StartsWith("x265") ? "libx265" : "libx264",
                Settings.Default.format.Contains("420") ? "-pix_fmt yuv420p" : string.Empty,
                JoinFilters(filters),
                Settings.Default.crf,
                outputFile);
        }

        /// <summary>
        /// Generate FFmpeg filter sets for different 3D rips and eyes.
        /// </summary>
        /// <param name="left">Filter the left eye's frames</param>
        /// <param name="halfSize">Half frame size depending on mode</param>
        /// <param name="sbs">Side-by-side division if true, over-under otherwise</param>
        static string EyeFilters(bool left, bool halfSize, bool sbs = true) {
            string output = left ? "select=\"mod(n-1\\,2)\"" : "select=\"not(mod(n-1\\,2))\"";
            if (halfSize)
                return output + (sbs ? ",scale=iw/2:ih,setsar=1:1" : ",scale=iw:ih/2,setsar=1:1");
            return output;
        }

        /// <summary>
        /// Separates FFmpeg filters with a comma if they exist.
        /// </summary>
        static string JoinFilters(params string[] filters) {
            string result = string.Join(',', filters.Where(x => !string.IsNullOrEmpty(x)));
            if (result.Length > 0)
                return videoFilterTag + result;
            return result;
        }
    }
}