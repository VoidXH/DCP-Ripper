using Cavern.Format;
using Cavern.Utilities;
using DCP_Ripper.Consts;
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
        /// Constant Rate Factor for AVC/HEVC codecs when ripping 3D content.
        /// </summary>
        static int CRF3D => Settings.Default.crf3d;

        /// <summary>
        /// 3D ripping mode.
        /// </summary>
        static Mode3D StereoMode => (Mode3D)Enum.Parse(typeof(Mode3D), Settings.Default.mode3d);

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
        string GetStreamExportPath(string source, string extension, bool video) {
            string directory = Path.GetDirectoryName(source),
                file = $"{(video ? "_v-" : "_a-")}{Path.GetFileName(directory)}.{extension}";
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
        /// Process a video file. The created file will have the same name, but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessVideo(Reel content, string extraFilters = "") {
            if (content.videoFile == null || !File.Exists(content.videoFile))
                return null;
            string fileName = GetStreamExportPath(content.videoFile, "mkv", true);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
            if (!content.is3D)
                return LaunchFFmpeg(FFmpegCalls.VideoToSelectedCodec(content, fileName, extraFilters)) ? fileName : null;

            int lowerCRF = Math.Max(CRF3D - 5, 0);
            if (StereoMode == Mode3D.Interop)
                return LaunchFFmpeg(FFmpegCalls.Interop3D(content, fileName)) ? fileName : null;
            else if (StereoMode == Mode3D.LeftEye || StereoMode == Mode3D.RightEye)
                return LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, fileName, Settings.Default.crf,
                    StereoMode == Mode3D.LeftEye, false, false, extraFilters)) ? fileName : null;

            bool halfSize = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.HalfOverUnder;
            bool sbs = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.SideBySide;
            string leftFile = GetStreamExportPath(content.videoFile, "L.mkv", true);
            if (Settings.Default.overwrite || !File.Exists(leftFile))
                if (!LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, leftFile, lowerCRF, true, halfSize, sbs, extraFilters)))
                    return null;
            string rightFile = GetStreamExportPath(content.videoFile, "R.mkv", true);
            if (Settings.Default.overwrite || !File.Exists(rightFile))
                if (!LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, rightFile, lowerCRF, false, halfSize, sbs, extraFilters)))
                    return null;
            if (LaunchFFmpeg(FFmpegCalls.Merge3D(leftFile, rightFile, sbs, fileName, extraFilters))) {
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
        public string ProcessVideo2K(Reel content) => ProcessVideo(content, Is4K ? "scale=iw/2:ih/2" : string.Empty);

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
            string fileName = GetStreamExportPath(content.audioFile, "mkv", false);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
            if (Settings.Default.downmix) {
                string tempName = fileName[..(fileName.LastIndexOf('.') + 1)] + "wav";
                if ((Settings.Default.overwrite || !File.Exists(tempName)) &&
                    !LaunchFFmpeg(FFmpegCalls.AudioToPCM(content, tempName)))
                    return null;
                DownmixTo51(tempName);
                if (LaunchFFmpeg(FFmpegCalls.ApplyCodec(tempName, fileName))) {
                    File.Delete(tempName);
                    return fileName;
                }
                return null;
            }
            return LaunchFFmpeg(FFmpegCalls.AudioToSelectedCodec(content, fileName)) ? fileName : null;
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