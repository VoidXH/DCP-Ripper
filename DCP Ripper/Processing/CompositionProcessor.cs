using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

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
            get => VideoFormat.Equals("libx264") ? true : chromaSubsampling;
            set => chromaSubsampling = value;
        }

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
        public IReadOnlyList<Reel> Contents => contents;

        /// <summary>
        /// Use chroma subsampling.
        /// </summary>
        bool chromaSubsampling = false;

        /// <summary>
        /// List of reel data in this composition.
        /// </summary>
        readonly List<Reel> contents = new List<Reel>();

        /// <summary>
        /// Path of FFmpeg.
        /// </summary>
        readonly string ffmpegPath;

        /// <summary>
        /// Get the file names for UUIDs in a given composition.
        /// </summary>
        Dictionary<string, string> ParseAssetMap(string directory) {
            Dictionary<string, string> map = new Dictionary<string, string>();
            string fileName = directory + "ASSETMAP";
            if (!File.Exists(fileName))
                return map;
            string nextId = string.Empty;
            using (XmlReader reader = XmlReader.Create(fileName)) {
                while (reader.Read()) {
                    if (reader.NodeType != XmlNodeType.Element)
                        continue;
                    switch (reader.Name) {
                        case "Id":
                            reader.Read();
                            nextId = reader.Value;
                            break;
                        case "Path":
                            reader.Read();
                            map.Add(nextId, reader.Value);
                            break;
                        default:
                            break;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Load a composition for processing.
        /// </summary>
        public CompositionProcessor(string ffmpegPath, string cplPath) {
            this.ffmpegPath = ffmpegPath;
            string directory = cplPath.Substring(0, cplPath.LastIndexOf('\\') + 1);
            Dictionary<string, string> assets = ParseAssetMap(directory);
            Reel reel = new Reel();
            using (XmlReader reader = XmlReader.Create(cplPath)) {
                bool video = true;
                while (reader.Read()) {
                    if (reader.NodeType != XmlNodeType.Element) {
                        if (reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals("Reel"))
                            contents.Add(reel);
                        continue;
                    } else if (reader.Name.EndsWith("MainStereoscopicPicture")) {
                        video = true;
                        reel.is3D = true;
                        continue;
                    }
                    switch (reader.Name) {
                        case "Id":
                            reader.Read();
                            if (assets.ContainsKey(reader.Value)) {
                                if (video)
                                    reel.videoFile = directory + assets[reader.Value];
                                else
                                    reel.audioFile = directory + assets[reader.Value];
                            } else { // Try to parse a single reel content with a missing asset map
                                List<string> bulkAssets = Finder.ForceGetAssets(directory);
                                if (bulkAssets.Count == 2) {
                                    long size0 = new FileInfo(bulkAssets[0]).Length, size1 = new FileInfo(bulkAssets[1]).Length;
                                    if (video)
                                        reel.videoFile = bulkAssets[size0 < size1 ? 1 : 0];
                                    else
                                        reel.audioFile = bulkAssets[size1 < size0 ? 1 : 0];
                                }
                            }
                            break;
                        case "EntryPoint":
                            reader.Read();
                            if (video)
                                reel.videoStartFrame = int.Parse(reader.Value);
                            else
                                reel.audioStartFrame = int.Parse(reader.Value);
                            break;
                        case "Duration":
                            reader.Read();
                            reel.duration = int.Parse(reader.Value);
                            break;
                        case "FrameRate":
                            reader.Read();
                            reel.framerate = int.Parse(reader.Value.Substring(0, reader.Value.IndexOf(' ')));
                            if (reel.is3D)
                                reel.framerate /= 2;
                            break;
                        case "Reel":
                            reel = new Reel();
                            break;
                        case "MainPicture":
                            video = true;
                            break;
                        case "MainSound":
                            video = false;
                            break;
                        case "MainSubtitle":
                            while (reader.Read() && (reader.NodeType != XmlNodeType.EndElement || !reader.Name.Equals("MainSubtitle"))) ;
                            break;
                        case "KeyId":
                            reel.needsKey = true;
                            break;
                        case "ContentTitleText":
                            reader.Read();
                            Is4K = (Title = reader.Value).Contains("_4K");
                            break;
                        default:
                            break;
                    }
                }
            }
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
            string videoStart = (content.videoStartFrame / (float)content.framerate).ToString("0.000").Replace(',', '.');
            string length = (content.duration / (float)content.framerate).ToString("0.000").Replace(',', '.');
            string subsampling = ChromaSubsampling ? "-pix_fmt yuv420p" : string.Empty;
            string fileName = content.videoFile.Replace(".mxf", ".mkv").Replace(".MXF", ".mkv");
#if DEBUG
            if (File.Exists(fileName))
                return fileName;
#endif
            if (!content.is3D) {
                return LaunchFFmpeg(string.Format("-ss {0} -i \"{1}\" -t {2} -c:v {3} {4} {5} -crf {6} -v error -stats \"{7}\"",
                    videoStart, content.videoFile, length, VideoFormat, subsampling, extraModifiers, CRF, fileName)) ? fileName : null;
            }
            string doubleRate = "-r " + content.framerate * 2;
            string leftFile = content.videoFile.Replace(".mxf", "_L.mkv").Replace(".MXF", "_L.mkv");
            int lowerCRF = Math.Max(CRF3D - 5, 0);
            if (StereoMode == Mode3D.Interop) {
                if (LaunchFFmpeg(string.Format("{0} -ss {1} -i \"{2}\" -t {3} -c:v {4} -crf {5} -v error -stats \"{6}\"",
                    doubleRate, videoStart, content.videoFile, length, VideoFormat, CRF, fileName)))
                    return fileName;
                return null;
            } else if (StereoMode == Mode3D.LeftEye) {
                if (LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length, EyeFilters(true, false),
                    VideoFormat, CRF, fileName)))
                    return fileName;
                return null;
            } else if (StereoMode == Mode3D.RightEye) {
                if (LaunchFFmpeg(string.Format(singleEye, doubleRate, videoStart, content.videoFile, length, EyeFilters(false, false),
                    VideoFormat, CRF, fileName)))
                    return fileName;
                return null;
            }
            bool halfSize = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.HalfOverUnder;
            bool sbs = StereoMode == Mode3D.HalfSideBySide || StereoMode == Mode3D.SideBySide;
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
            if (LaunchFFmpeg(string.Format("-i \"{0}\" -i \"{1}\" -filter_complex [0:v][1:v]{2}stack=inputs=2[v] -map [v] " +
                "-c:v {3} -crf {4} -v error -stats \"{5}\"",
                leftFile, rightFile, sbs ? 'h' : 'v', VideoFormat, CRF3D, fileName))) {
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
                (content.audioStartFrame / (float)content.framerate).ToString("0.000").Replace(',', '.'),
                (content.duration / (float)content.framerate).ToString("0.000").Replace(',', '.'),
                AudioFormat,
                fileName)) ? fileName : null;
        }

        /// <summary>
        /// Merge a converted video and audio file, deleting the sources.
        /// </summary>
        public bool Merge(string video, string audio, string fileName) {
            LaunchFFmpeg(string.Format("-i \"{0}\" -i \"{1}\" -c copy -v error -stats \"{2}\"", video, audio, fileName));
            if (File.Exists(video) && File.Exists(audio)) {
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
            for (int i = 0, length = contents.Count; i < length; ++i) {
                if (contents[i].needsKey || contents[i].videoFile == null)
                    continue;
                string path = forcePath;
                if (path == null)
                    path = contents[i].videoFile.Substring(0, contents[i].videoFile.LastIndexOf("\\") + 1);
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
                string video = force2K ? ProcessVideo2K(contents[i]) : ProcessVideo(contents[i]);
                string audio = ProcessAudio(contents[i]);
                if (video != null && audio != null && Merge(video, audio, fileName))
                    ++reelsDone;
            }
            return reelsDone == contents.Count;
        }
    }
}