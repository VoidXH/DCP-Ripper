using System.Collections.Generic;
using System.Text;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Processes a list of compositions.
    /// </summary>
    public class ListProcessor {
        /// <summary>
        /// The list of compositions to process.
        /// </summary>
        public List<string> Compositions { get; private set; }

        /// <summary>
        /// Operation status.
        /// </summary>
        public string Status { get; private set; } = "Ready.";

        StringBuilder failures;

        /// <summary>
        /// Create a processor for a list of compositions.
        /// </summary>
        public ListProcessor(List<string> compositions) {
            Compositions = compositions;
        }

        /// <summary>
        /// Start processing the compositions. Returns the number of successful conversions.
        /// </summary>
        /// <returns></returns>
        public int Process(string ffmpegPath, string outputPath, string videoFormat, bool subsampling, int crf, int crf3d,
            string audioFormat, bool force2K, bool zip, bool delete) {
            int finished = 0;
            failures = new StringBuilder();
            foreach (string composition in Compositions) {
                string title = Finder.GetCPLTitle(composition);
                Status = string.Format("Processing {0}...", title);
                CompositionProcessor processor = new CompositionProcessor(ffmpegPath, composition) {
                    VideoFormat = videoFormat,
                    ChromaSubsampling = subsampling,
                    CRF = crf,
                    CRF3D = crf3d,
                    AudioFormat = audioFormat,
                };
                string finalOutput = outputPath, sourceFolder = composition.Substring(0, composition.LastIndexOf('\\'));
                if (!string.IsNullOrEmpty(outputPath) && outputPath.Equals(MainWindow.parentMarker))
                    finalOutput = sourceFolder.Substring(0, sourceFolder.LastIndexOf('\\'));
                if (processor.ProcessComposition(force2K, finalOutput)) {
                    ++finished;
                    if (zip) {
                        Status = string.Format("Zipping {0}...", title);
                        Finder.ZipAssets(sourceFolder, string.Format("{0}\\{1}.zip", finalOutput, title));
                    }
                    if (delete)
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