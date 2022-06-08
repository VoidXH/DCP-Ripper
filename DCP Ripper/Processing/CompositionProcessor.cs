using Cavern.Format;
using Cavern.Utilities;
using DCP_Ripper.Properties;
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
        /// List of reel data in this composition.
        /// </summary>
        public IReadOnlyList<Reel> Contents { get; private set; }

        /// <summary>
        /// Video codec name for FFmpeg.
        /// </summary>
        static string VideoFormat => Settings.Default.format.StartsWith("x265") ? "libx265" : "libx264";

        /// <summary>
        /// Use chroma subsampling.
        /// </summary>
        static bool ChromaSubsampling => Settings.Default.format.Contains("420");

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs.
        /// </summary>
        static int CRF => Settings.Default.crf;

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs when ripping 3D content.
        /// </summary>
        static int CRF3D => Settings.Default.crf3d;

        /// <summary>
        /// 3D ripping mode.
        /// </summary>
        static Mode3D StereoMode => (Mode3D)Enum.Parse(typeof(Mode3D), Settings.Default.mode3d);

        /// <summary>
        /// Audio codec name for FFmpeg.
        /// </summary>
        static string AudioFormat => Settings.Default.audio;

        /// <summary>
        /// Path of FFmpeg.
        /// </summary>
        readonly string ffmpegPath;

        /// <summary>
        /// Load a composition for processing.
        /// </summary>
        public CompositionProcessor(string ffmpegPath, string cplPath) {
            this.ffmpegPath = ffmpegPath;
            PlaylistProcessor importer = new(cplPath);
            Contents = importer.Contents;
            Is4K = (Title = importer.Title).Contains("_4K");
        }

        /// <summary>
        /// Gets where the stream or final export file should be placed.
        /// </summary>
        string GetStreamExportPath(string source) {
            string directory = Path.GetDirectoryName(source),
                file = "stream-" + Path.GetFileName(source).Replace(".mxf", ".mkv").Replace(".MXF", ".mkv");
            if (ForcePath != null)
                directory = ForcePath;
            return Path.Combine(directory, file);
        }

        /// <summary>
        /// Launch the FFmpeg to process a file with the given arguments.
        /// </summary>
        bool LaunchFFmpeg(string arguments) {
            ProcessStartInfo start = new() {
                Arguments = arguments,
                FileName = ffmpegPath
            };
            try {
                using Process proc = Process.Start(start);
                proc.WaitForExit();
                return proc.ExitCode == 0;
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
        static string EyeFilters(bool left, bool halfSize, bool sbs = true) {
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
            string fileName = GetStreamExportPath(content.videoFile);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
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
            if (Settings.Default.overwrite || !File.Exists(leftFile))
                if (!LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length,
                    EyeFilters(true, halfSize, sbs), VideoFormat, lowerCRF, leftFile)))
                    return null;
            string rightFile = content.videoFile.Replace(".mxf", "_R.mkv").Replace(".MXF", "_R.mkv");
            if (Settings.Default.overwrite || !File.Exists(rightFile))
                if (!LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length,
                    EyeFilters(false, halfSize, sbs), VideoFormat, lowerCRF, rightFile)))
                    return null;
            if (LaunchFFmpeg($"-i \"{leftFile}\" -i \"{rightFile}\" -filter_complex" +
                $" [0:v][1:v]{(sbs ? 'h' : 'v')}stack=inputs=2[v] -map [v] -c:v {VideoFormat} {subsampling} {extraModifiers}" +
                $" -crf {CRF3D} -v error -stats \"{fileName}\"")) {
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
        /// Downmix a WAV file to 5.1 while keeping the gains.
        /// </summary>
        static void DownmixTo51(string path) {
            RIFFWaveReader reader = new(path);
            reader.ReadHeader();

            if (reader.ChannelCount <= 6) {
                reader.Dispose();
                return;
            }

            string newPath = Path.Combine(Path.GetDirectoryName(path), "_temp2.wav");
            RIFFWaveWriter writer = new(newPath, 6, reader.Length, reader.SampleRate, reader.Bits);
            writer.WriteHeader();

            long progress = 0;
            const long blockSize = 1 << 18; // 1 MB/channel @ 32 bits
            float[][] inData = new float[reader.ChannelCount][],
                outData = new float[6][];
            for (int i = 0; i < reader.ChannelCount; ++i)
                inData[i] = new float[blockSize];
            while (progress < reader.Length) {
                reader.ReadBlock(inData, 0, blockSize);
                // 6-7 are hearing/visually impaired tracks, 12+ are sync signals
                for (int i = 8; i < Math.Min(inData.Length, 12); ++i)
                    WaveformUtils.Mix(inData[i], inData[4 + i % 2]);
                Array.Copy(inData, outData, 6);
                writer.WriteBlock(outData, 0, Math.Min(blockSize, reader.Length - progress));
                progress += blockSize;
            }

            reader.Dispose();
            writer.Dispose();
            File.Delete(path);
            File.Move(newPath, path);
        }

        /// <summary>
        /// Process the audio file of a content. The created file will have the same name,
        /// but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessAudio(Reel content) {
            if (content.audioFile == null || !File.Exists(content.audioFile))
                return null;
            string fileName = GetStreamExportPath(content.audioFile);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
            if (Settings.Default.downmix) {
                string tempName = fileName[..(fileName.LastIndexOf('.') + 1)] + "wav";
                if (Settings.Default.overwrite || !File.Exists(tempName)) {
                    string args = string.Format("-i \"{0}\" -ss {1} -t {2} -c:a pcm_s24le -v error -stats \"{3}\"",
                        content.audioFile,
                        (content.audioStartFrame / content.framerate).ToString("0.000").Replace(',', '.'),
                        (content.duration / content.framerate).ToString("0.000").Replace(',', '.'),
                        tempName);
                    if (!LaunchFFmpeg(args))
                        return null;
                }
                DownmixTo51(tempName);
                string result =
                    LaunchFFmpeg($"-i \"{tempName}\" -c:a {AudioFormat} -v error -stats \"{fileName}\"") ? fileName : null;
                File.Delete(tempName);
                return result;
            }
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
        public bool ProcessComposition() {
            int reelsDone = 0;
            for (int i = 0, length = Contents.Count; i < length; ++i) {
                if (Contents[i].needsKey || Contents[i].videoFile == null)
                    continue;
                string path = ForcePath;
                if (path == null)
                    path = Path.GetDirectoryName(Contents[i].videoFile);
                string outputTitle = Settings.Default.downscale ? Title.Replace("_4K", "_2K") : Title;
                string fileName = Path.Combine(path, length == 1 ? outputTitle + ".mkv" : $"{outputTitle}_{i + 1}.mkv");
                if (!Settings.Default.overwrite && File.Exists(fileName)) {
                    ++reelsDone;
                    continue;
                }
                string video = null;
                if (Settings.Default.ripVideo)
                    video = Settings.Default.downscale ? ProcessVideo2K(Contents[i]) : ProcessVideo(Contents[i]);
                string audio = null;
                if (Settings.Default.ripAudio)
                    audio = ProcessAudio(Contents[i]);

                if (Settings.Default.ripVideo && Settings.Default.ripAudio) {
                    if (video != null && audio != null && Merge(video, audio, fileName))
                        ++reelsDone;
                } else if (Settings.Default.ripVideo) {
                    if (video != null) {
                        File.Move(video, fileName);
                        ++reelsDone;
                    }
                } else if (Settings.Default.ripAudio) {
                    if (audio != null) {
                        File.Move(audio, fileName);
                        ++reelsDone;
                    }
                } else
                    ++reelsDone;
            }
            return reelsDone == Contents.Count;
        }
    }
}