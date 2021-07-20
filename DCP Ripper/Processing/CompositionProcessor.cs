using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Rips a composition.
    /// </summary>
    public class CompositionProcessor {
        /// <summary>
        /// FFmpeg arguments for single eye processing.
        /// </summary>
        const string singleEye = "{0} -ss {1} -i \"{2}\" -t {3} {4} -c:v {5} -crf {6} -v error -stats \"{7}\"";

        /// <summary>
        /// Composition title.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Override the working/export path.
        /// </summary>
        public string ForcePath { get; set; } = null;

        /// <summary>
        /// This composition is 4K.
        /// </summary>
        public bool Is4K { get; private set; }

        /// <summary>
        /// Video codec name for FFmpeg.
        /// </summary>
        public string VideoFormat { get; set; } = "libx265";

        /// <summary>
        /// Use chroma subsampling.
        /// </summary>
        public bool ChromaSubsampling {
            get => VideoFormat.Equals("libx264") || chromaSubsampling;
            set => chromaSubsampling = value;
        }
        bool chromaSubsampling = false;

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs.
        /// </summary>
        public int CRF { get; set; } = 23;

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs when ripping 3D content.
        /// </summary>
        public int CRF3D { get; set; } = 18;

        /// <summary>
        /// 3D ripping mode.
        /// </summary>
        public Mode3D StereoMode { get; set; } = Mode3D.HalfSideBySide;

        /// <summary>
        /// Audio codec name for FFmpeg.
        /// </summary>
        public string AudioFormat { get; set; } = "libopus";

        /// <summary>
        /// List of reel data in this composition.
        /// </summary>
        public IReadOnlyList<Reel> Contents { get; private set; }

        /// <summary>
        /// Path of FFmpeg.
        /// </summary>
        readonly string ffmpegPath;

        /// <summary>
        /// Load a composition for processing.
        /// </summary>
        public CompositionProcessor(string ffmpegPath, string cplPath) {
            this.ffmpegPath = ffmpegPath;
            PlaylistProcessor importer = new PlaylistProcessor(cplPath);
            Contents = importer.Contents;
            Is4K = (Title = importer.Title).Contains("_4K");
        }

        /// <summary>
        /// Gets where the stream or final export file should be placed.
        /// </summary>
        string GetStreamExportPath(string source) {
            string fileName = source.Replace(".mxf", ".mkv").Replace(".MXF", ".mkv");
            if (ForcePath != null)
                fileName = Path.Combine(ForcePath, Path.GetFileName(fileName));
            return fileName;
        }

        /// <summary>
        /// Launch the FFmpeg to process a file with the given arguments.
        /// </summary>
        bool LaunchFFmpeg(string arguments) {
            ProcessStartInfo start = new ProcessStartInfo {
                Arguments = arguments,
                FileName = ffmpegPath
            };
            try {
                using (Process proc = Process.Start(start)) {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Generate FFmpeg filter sets for different 3D rips and eyes.
        /// </summary>
        /// <param name="left">Filter the left eye's frames</param>
        /// <param name="halfSize">Half frame size depending on mode</param>
        /// <param name="sbs">Side-by-side division if true, over-under otherwise</param>
        string EyeFilters(bool left, bool halfSize, bool sbs = true) {
            string output = left ? "-vf select=\"mod(n-1\\,2)\"" : "-vf select=\"not(mod(n-1\\,2))\"";
            if (halfSize)
                output += sbs ? ",scale=iw/2:ih,setsar=1:1" : ",scale=iw:ih/2,setsar=1:1";
            return output;
        }

        /// <summary>
        /// Process a video file. The created file will have the same name, but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessVideo(Reel content, string extraModifiers = "") {
            if (content.videoFile == null || !File.Exists(content.videoFile))
                return null;
#if DEBUG
            if (File.Exists(fileName))
            string fileName = GetStreamExportPath(content.videoFile);
                return fileName;
#endif
            string videoStart = (content.videoStartFrame / content.framerate).ToString("0.000").Replace(',', '.');
            string length = (content.duration / content.framerate).ToString("0.000").Replace(',', '.');
            string subsampling = ChromaSubsampling ? "-pix_fmt yuv420p" : string.Empty;
            if (!content.is3D)
                return LaunchFFmpeg($"-ss {videoStart} -i \"{content.videoFile}\" -t {length} -c:v {VideoFormat} " +
                    $"{subsampling} {extraModifiers} -crf {CRF} -v error -stats \"{fileName}\"") ? fileName : null;

            string doubleRate = "-r " + (content.framerate * 2).ToString("0.000").Replace(',', '.');
            int lowerCRF = Math.Max(CRF3D - 5, 0);
            if (StereoMode == Mode3D.Interop)
                return LaunchFFmpeg($"{doubleRate} -ss {videoStart} -i \"{content.videoFile}\" -t {length} -c:v {VideoFormat} " +
                    $"-crf {CRF} -v error -stats \"{fileName}\"") ? fileName : null;
            else if (StereoMode == Mode3D.LeftEye || StereoMode == Mode3D.RightEye)
                return LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length,
                    EyeFilters(StereoMode == Mode3D.LeftEye, false), VideoFormat, CRF, fileName)) ? fileName : null;

            bool halfSize = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.HalfOverUnder;
            bool sbs = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.SideBySide;
            string leftFile = content.videoFile.Replace(".mxf", "_L.mkv").Replace(".MXF", "_L.mkv");
#if DEBUG
            if (!File.Exists(leftFile))
#endif
            if (!LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length, EyeFilters(true, halfSize, sbs),
                VideoFormat, lowerCRF, leftFile)))
                return null;
            string rightFile = content.videoFile.Replace(".mxf", "_R.mkv").Replace(".MXF", "_R.mkv");
#if DEBUG
            if (!File.Exists(rightFile))
#endif
            if (!LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length, EyeFilters(false, halfSize, sbs),
                VideoFormat, lowerCRF, rightFile))) {
                return null;
            }
            if (LaunchFFmpeg($"-i \"{leftFile}\" -i \"{rightFile}\" -filter_complex [0:v][1:v]{(sbs ? 'h' : 'v')}stack=inputs=2[v] " +
                $"-map [v] -c:v {VideoFormat} {subsampling} {extraModifiers} -crf {CRF3D} -v error -stats \"{fileName}\"")) {
                if (!File.Exists(fileName))
                    return null;
                File.Delete(leftFile);
                File.Delete(rightFile);
                return fileName;
            }
            return null;
        }

        /// <summary>
        /// Process a video file. If the resolution is 4K, it will be downscaled to 2K.
        /// The created file will have the same name, but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessVideo2K(Reel content) => ProcessVideo(content, Is4K ? "-vf scale=iw/2:ih/2" : string.Empty);

        /// <summary>
        /// Process the audio file of a content. The created file will have the same name,
        /// but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessAudio(Reel content) {
            if (content.audioFile == null || !File.Exists(content.audioFile))
                return null;
            string fileName = content.audioFile.Replace(".mxf", ".mkv").Replace(".MXF", ".mkv");
#if DEBUG
            if (File.Exists(fileName))
                return fileName;
#endif
            return LaunchFFmpeg(string.Format("-i \"{0}\" -ss {1} -t {2} -c:a {3} -v error -stats \"{4}\"",
                content.audioFile,
                (content.audioStartFrame / content.framerate).ToString("0.000").Replace(',', '.'),
                (content.duration / content.framerate).ToString("0.000").Replace(',', '.'),
                AudioFormat,
                fileName)) ? fileName : null;
        }

        /// <summary>
        /// Merge a converted video and audio file, deleting the sources.
        /// </summary>
        public bool Merge(string video, string audio, string fileName) {
            if (File.Exists(video) && File.Exists(audio) &&
                LaunchFFmpeg($"-i \"{video}\" -i \"{audio}\" -c copy -v error -stats \"{fileName}\"")) {
                File.Delete(video);
                File.Delete(audio);
                return File.Exists(fileName);
            }
            return false;
        }

        /// <summary>
        /// Process the video files of this DCP. Returns if all reels were successfully processed.
        /// </summary>
        /// <param name="force2K">Downscale 4K content to 2K</param>
        /// <param name="forcePath">Change the default output directory, which is the container of the video file</param>
        public bool ProcessComposition(bool force2K = false, string forcePath = null) {
            int reelsDone = 0;
            for (int i = 0, length = Contents.Count; i < length; ++i) {
                if (Contents[i].needsKey || Contents[i].videoFile == null)
                    continue;
                string path = forcePath;
                if (path == null)
                    path = Contents[i].videoFile.Substring(0, Contents[i].videoFile.LastIndexOf("\\") + 1);
                else if (!path.EndsWith("\\"))
                    path += '\\';
                string outputTitle = force2K ? Title.Replace("_4K", "_2K") : Title;
                string fileName = path + (length == 1 ? outputTitle + ".mkv" : string.Format("{0}_{1}.mkv", outputTitle, i + 1));
#if DEBUG
                if (File.Exists(fileName)) {
                    ++reelsDone;
                    continue;
                }
#endif
                string video = force2K ? ProcessVideo2K(Contents[i]) : ProcessVideo(Contents[i]);
                string audio = ProcessAudio(Contents[i]);
                if (video != null && audio != null && Merge(video, audio, fileName))
                    ++reelsDone;
            }
            return reelsDone == Contents.Count;
        }
    }
}