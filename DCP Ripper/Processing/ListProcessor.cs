using System.Collections.Generic;
using System.Text;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Processes a list of compositions.
    /// </summary>
    public class ListProcessor {
        /// <summary>
        /// Marks the content parent folder for output path.
        /// </summary>
        public const string parentMarker = "parent";

        /// <summary>
        /// The list of compositions to process.
        /// </summary>
        public List<string> Compositions { get; set; }

        /// <summary>
        /// Operation status.
        /// </summary>
        public string Status { get; private set; } = "Ready.";

        /// <summary>
        /// Launch location of ffmpeg.exe.
        /// </summary>
        public string FFmpegPath { get; set; } = null;

        /// <summary>
        /// Forced content output path. Null means default (next to video files), <see cref="parentMarker"/> means its parent.
        /// </summary>
        public string OutputPath { get; set; } = null;

        /// <summary>
        /// Video codec name for FFmpeg.
        /// </summary>
        public string VideoFormat { get; set; } = "libx265";

        /// <summary>
        /// Use chroma subsampling.
        /// </summary>
        public bool ChromaSubsampling { get; set; } = false;

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs.
        /// </summary>
        public int CRF { get; set; } = 23;

        /// <summary>
        /// Constant Rate Factor for AVC/HEVC codecs when ripping 3D content.
        /// </summary>
        public int CRF3D { get; set; } = 18;

        /// <summary>
        /// Audio codec name for FFmpeg.
        /// </summary>
        public string AudioFormat { get; set; } = "libopus";

        /// <summary>
        /// Downscale 4K content to 2K.
        /// </summary>
        public bool Force2K { get; set; } = true;

        /// <summary>
        /// Zip the composition after conversion.
        /// </summary>
        public bool ZipAfter { get; set; } = false;

        /// <summary>
        /// Delete the composition after conversion.
        /// </summary>
        public bool DeleteAfter { get; set; } = false;

        /// <summary>
        /// List of failed content.
        /// </summary>
        StringBuilder failures;

        /// <summary>
        /// Start processing the compositions.
        /// </summary>
        /// <returns>Nnumber of successful conversions</returns>
        public int Process() {
            if (Compositions == null || FFmpegPath == null)
                return 0;
            int finished = 0;
            failures = new StringBuilder();
            foreach (string composition in Compositions) {
                string title = Finder.GetCPLTitle(composition);
                Status = string.Format("Processing {0}...", title);
                CompositionProcessor processor = new CompositionProcessor(FFmpegPath, composition) {
                    VideoFormat = VideoFormat,
                    ChromaSubsampling = ChromaSubsampling,
                    CRF = CRF,
                    CRF3D = CRF3D,
                    AudioFormat = AudioFormat
                };
                string finalOutput = OutputPath, sourceFolder = composition.Substring(0, composition.LastIndexOf('\\'));
                if (!string.IsNullOrEmpty(OutputPath) && OutputPath.Equals(parentMarker))
                    finalOutput = sourceFolder.Substring(0, sourceFolder.LastIndexOf('\\'));
                if (processor.ProcessComposition(Force2K, finalOutput)) {
                    ++finished;
                    if (ZipAfter) {
                        Status = string.Format("Zipping {0}...", title);
                        Finder.ZipAssets(sourceFolder, string.Format("{0}\\{1}.zip", finalOutput, title));
                    }
                    if (DeleteAfter)
                        Finder.DeleteAssets(sourceFolder);
                } else
                    failures.AppendLine(title);
            }
            return finished;
        }

        /// <summary>
        /// Gets a string where each line is a failed content that could not be processed.
        /// </summary>
        public string GetFailedContents() => failures.ToString();
    }
}