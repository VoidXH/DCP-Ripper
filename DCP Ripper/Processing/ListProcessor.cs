using DCP_Ripper.Properties;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
        /// Launch location of ffmpeg.exe.
        /// </summary>
        public string FFmpegPath { get; set; } = null;

        /// <summary>
        /// Forced content output path. Null means default (next to video files), <see cref="parentMarker"/> means its parent.
        /// </summary>
        public string OutputPath { get; set; } = null;

        /// <summary>
        /// Process state update.
        /// </summary>
        /// <param name="status">Current job</param>
        public delegate void StatusUpdate(string status);

        /// <summary>
        /// Called when a new job is started.
        /// </summary>
        public event StatusUpdate OnStatusUpdate;

        /// <summary>
        /// Process completion delegate.
        /// </summary>
        /// <param name="finished">Number of successful conversions</param>
        public delegate void AfterProcess(int finished);

        /// <summary>
        /// Called when list processing is finished.
        /// </summary>
        public event AfterProcess OnCompletion;

        /// <summary>
        /// List of failed content.
        /// </summary>
        StringBuilder failures;

        /// <summary>
        /// Async <see cref="Process"/> handler.
        /// </summary>
        Task<int> task;

        /// <summary>
        /// Start processing the compositions.
        /// </summary>
        /// <returns>Number of successful conversions</returns>
        public int Process() {
            if (Compositions == null || FFmpegPath == null)
                return 0;
            int finished = 0;
            failures = new StringBuilder();
            foreach (string composition in Compositions) {
                if (!File.Exists(composition))
                    continue;
                string title = Finder.GetCPLTitle(composition);
                OnStatusUpdate?.Invoke($"Processing {title}...");
                string finalOutput = OutputPath, sourceFolder = composition[..composition.LastIndexOf('\\')];
                if (!string.IsNullOrEmpty(OutputPath) && OutputPath.Equals(parentMarker)) {
                    int index = sourceFolder.LastIndexOf('\\');
                    if (index < 0) {
                        failures.AppendLine("Drive root is an invalid directory: " + title);
                        continue;
                    }
                    finalOutput = sourceFolder[..index];
                }
                CompositionProcessor processor = new(FFmpegPath, composition) {
                    ForcePath = finalOutput
                };
                if (processor.ProcessComposition()) {
                    ++finished;
                    if (Settings.Default.zipAfter) {
                        OnStatusUpdate?.Invoke($"Zipping {title}...");
                        Finder.ZipAssets(sourceFolder, $"{finalOutput}\\{title}.zip", textOut => OnStatusUpdate(textOut));
                    }
                    if (Settings.Default.deleteAftter)
                        Finder.DeleteAssets(sourceFolder);
                } else
                    failures.AppendLine("Conversion error: " + title);
            }
            OnStatusUpdate?.Invoke("Finished!");
            OnCompletion?.Invoke(finished);
            return finished;
        }

        /// <summary>
        /// Start processing the compositions as a <see cref="Task"/>.
        /// </summary>
        /// <returns>A task with a return value of the number of successful conversions</returns>
        public Task<int> ProcessAsync() {
            if (task != null && !task.IsCompleted)
                return task;
            task = new Task<int>(Process);
            task.Start();
            return task;
        }

        /// <summary>
        /// Gets a string where each line is a failed content that could not be processed.
        /// </summary>
        public string GetFailedContents() => failures.ToString();
    }
}