using Cavern.Format;
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
        /// FFmpeg argument to allow more than 8 channels of audio.
        /// </summary>
        const string rawMapping = "-mapping_family 255";

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
        /// Composition metadata.
        /// </summary>
        readonly CompositionInfo info;

        /// <summary>
        /// Load a composition for processing.
        /// </summary>
        public CompositionProcessor(CompositionInfo info) {
            this.info = info;
            PlaylistProcessor importer = new(info.Path);
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
        /// Process a video file. The created file will have the same name, but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessVideo(Reel content, string extraFilters = "") {
            if (content.videoFile == null || !File.Exists(content.videoFile))
                return null;
            string fileName = GetStreamExportPath(content.videoFile, "mkv", true);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
            if (!content.is3D)
                return FFmpegCalls.LaunchFFmpeg(FFmpegCalls.VideoToSelectedCodec(content, fileName, extraFilters)) ? fileName : null;

            int lowerCRF = Math.Max(CRF3D - 5, 0);
            if (StereoMode == Mode3D.Interop)
                return FFmpegCalls.LaunchFFmpeg(FFmpegCalls.Interop3D(content, fileName)) ? fileName : null;
            else if (StereoMode == Mode3D.LeftEye || StereoMode == Mode3D.RightEye)
                return FFmpegCalls.LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, fileName, Settings.Default.crf,
                    StereoMode == Mode3D.LeftEye, false, false, extraFilters)) ? fileName : null;

            bool halfSize = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.HalfOverUnder;
            bool sbs = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.SideBySide;
            string leftFile = GetStreamExportPath(content.videoFile, "L.mkv", true);
            if (Settings.Default.overwrite || !File.Exists(leftFile))
                if (!FFmpegCalls.LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, leftFile, lowerCRF, true, halfSize, sbs)))
                    return null;
            string rightFile = GetStreamExportPath(content.videoFile, "R.mkv", true);
            if (Settings.Default.overwrite || !File.Exists(rightFile))
                if (!FFmpegCalls.LaunchFFmpeg(FFmpegCalls.SingleEye3D(content, rightFile, lowerCRF, false, halfSize, sbs)))
                    return null;
            if (FFmpegCalls.LaunchFFmpeg(FFmpegCalls.Merge3D(leftFile, rightFile, sbs, fileName, extraFilters))) {
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
        /// Apply the selected downmixing method.
        /// </summary>
        static bool ApplyDownmix(string path, bool auro) {
            RIFFWaveReader reader = new(path);
            reader.ReadHeader();

            if (reader.ChannelCount <= 6) {
                reader.Dispose();
                return true;
            }

            string newPath = Path.Combine(Path.GetDirectoryName(path), "_temp.wav");
            if (Settings.Default.downmix == (int)Downmixer.Surround)
                Downmix.Surround(reader, auro, newPath);
            else if (Settings.Default.downmix == (int)Downmixer.GainKeeping51)
                Downmix.GainKeeping51(reader, newPath);
            else if (Settings.Default.downmix == (int)Downmixer.Cavern)
                Downmix.Cavern(reader, newPath);

            reader.Dispose();
            if (File.Exists(newPath)) {
                File.Delete(path);
                File.Move(newPath, path);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Process the audio file of a content. The created file will have the same name,
        /// but in Matroska format, which is the returned value.
        /// </summary>
        public string ProcessAudio(Reel content, bool auro) {
            if (content.audioFile == null || !File.Exists(content.audioFile))
                return null;
            string fileName = GetStreamExportPath(content.audioFile, "mkv", false);
            if (!Settings.Default.overwrite && File.Exists(fileName))
                return fileName;
            if (Settings.Default.downmix != (int)Downmixer.Bypass &&
                Settings.Default.downmix != (int)Downmixer.RawMapping) {
                string tempName = fileName[..(fileName.LastIndexOf('.') + 1)] + "wav";
                if ((Settings.Default.overwrite || !File.Exists(tempName)) &&
                    !FFmpegCalls.LaunchFFmpeg(FFmpegCalls.AudioToPCM(content, tempName)))
                    return null;
                if (!ApplyDownmix(tempName, auro))
                    return null;
                if (FFmpegCalls.LaunchFFmpeg(FFmpegCalls.ApplyCodec(tempName, fileName))) {
                    File.Delete(tempName);
                    return fileName;
                }
                return null;
            }
            string mapping = Settings.Default.downmix != (int)Downmixer.RawMapping ? string.Empty : rawMapping;
            return FFmpegCalls.LaunchFFmpeg(FFmpegCalls.AudioToSelectedCodec(content, fileName, mapping)) ? fileName : null;
        }

        /// <summary>
        /// Process the video/audio files of this DCP and merge them if set up that way.
        /// </summary>
        /// <returns>All reels were successfully processed, and the output path of the only reel for single-reel content.</returns>
        public (bool success, string output) ProcessComposition() {
            int reelsDone = 0;
            string fileName = null;
            for (int i = 0, length = Contents.Count; i < length; ++i) {
                if (Contents[i].needsKey || Contents[i].videoFile == null)
                    continue;
                string path = ForcePath ?? Path.GetDirectoryName(Contents[i].videoFile);
                string outputTitle = Settings.Default.downscale ? Title.Replace("_4K", "_2K") : Title;
                fileName = Path.Combine(path, length == 1 ? outputTitle + ".mkv" : $"{outputTitle}_{i + 1}.mkv");
                if (!Settings.Default.overwrite && File.Exists(fileName)) {
                    ++reelsDone;
                    continue;
                }
                string video = null;
                if (Settings.Default.ripVideo)
                    video = Settings.Default.downscale ? ProcessVideo2K(Contents[i]) : ProcessVideo(Contents[i]);
                string audio = null;
                if (Settings.Default.ripAudio)
                    audio = ProcessAudio(Contents[i], info.Audio == AudioTrack.Auro);

                if (Settings.Default.ripVideo && Settings.Default.ripAudio) {
                    if (video != null && audio != null && FFmpegCalls.Merge(video, audio, fileName))
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
            return (reelsDone == Contents.Count, Contents.Count == 1 ? fileName : null);
        }
    }
}