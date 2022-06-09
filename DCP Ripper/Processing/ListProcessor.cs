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
        /// A single selected composition was successfully converted.
        /// </summary>
        public const int SingleDone = -1;

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
        /// Async process handler.
        /// </summary>
        Task task;

        /// <summary>
        /// Process a single composition.
        /// </summary>
        bool ProcessSingle(string cplPath) {
            if (!File.Exists(cplPath)) {
                failures.AppendLine(Path.GetFileName(cplPath) + " does not exist.");
                return false;
            }
            string title = Finder.GetCPLTitle(cplPath);
            OnStatusUpdate?.Invoke($"Processing {title}...");
            string finalOutput = OutputPath,
                sourceFolder = Path.GetDirectoryName(cplPath);
            if (!string.IsNullOrEmpty(OutputPath) && OutputPath.Equals(parentMarker)) {
                finalOutput = Path.GetDirectoryName(sourceFolder);
                if (finalOutput == null) {
                    failures.AppendLine($"Can't output {title} above a root folder.");
                    return false;
                }
            }

            CompositionProcessor processor = new(FFmpegPath, cplPath) {
                ForcePath = finalOutput
            };
            if (processor.ProcessComposition()) {
                if (Settings.Default.zipAfter) {
                    OnStatusUpdate?.Invoke($"Zipping {title}...");
                    Finder.ZipAssets(sourceFolder, $"{finalOutput}\\{title}.zip", textOut => OnStatusUpdate(textOut));
                }
                if (Settings.Default.deleteAftter)
                    Finder.DeleteAssets(sourceFolder);
                return true;
            } else {
                failures.AppendLine($"Conversion of {title} failed - most likely a codec error.");
                return false;
            }
        }

        /// <summary>
        /// Start processing the compositions.
        /// </summary>
        /// <returns>Number of successful conversions</returns>
        public void Process() {
            if (Compositions == null || FFmpegPath == null)
                return;
            int finished = 0;
            failures = new StringBuilder();
            foreach (string composition in Compositions)
                ProcessSingle(composition);
            OnStatusUpdate?.Invoke("Finished!");
            OnCompletion?.Invoke(finished);
        }

        /// <summary>
        /// Start processing the selected composition.
        /// </summary>
        /// <returns>Number of successful conversions</returns>
        public void ProcessSelected(string path) {
            if (Compositions == null || FFmpegPath == null)
                return;
            failures = new StringBuilder();
            int finished = ProcessSingle(path) ? SingleDone : 0;
            OnStatusUpdate?.Invoke("Finished!");
            OnCompletion?.Invoke(finished);
        }

        /// <summary>
        /// Start processing the compositions as a <see cref="Task"/>.
        /// </summary>
        /// <returns>A task with a return value of the number of successful conversions</returns>
        public Task ProcessAsync() {
            if (task != null && !task.IsCompleted)
                return task;
            task = new Task(Process);
            task.Start();
            return task;
        }

        /// <summary>
        /// Start processing the selected composition as a <see cref="Task"/>.
        /// </summary>
        /// <returns>A task with a return value of the number of successful conversions</returns>
        public Task ProcessSelectedAsync(string path) {
            if (task != null && !task.IsCompleted)
                return task;
            task = new Task(() => ProcessSelected(path));
            task.Start();
            return task;
        }

        /// <summary>
        /// Gets a string where each line is a failed content that could not be processed.
        /// </summary>
        public string GetFailedContents() => failures.ToString();
    }
}