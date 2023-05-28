using DCP_Ripper.Consts;
using DCP_Ripper.Properties;
using System;
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
        /// Marks if there's a running conversion.
        /// </summary>
        public bool InProgress => task != null && !task.IsCompleted;

        /// <summary>
        /// The list of compositions to process.
        /// </summary>
        public List<CompositionInfo> Compositions { get; set; }

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
        /// Called when list processing is finished.
        /// </summary>
        public event Action OnCompletion;

        /// <summary>
        /// List of failed content.
        /// </summary>
        StringBuilder failures;

        /// <summary>
        /// Async process handler.
        /// </summary>
        Task task;

        /// <summary>
        /// Start processing the compositions.
        /// </summary>
        /// <returns>Number of successful conversions</returns>
        public void Process() => ProcessSelected(Compositions);

        /// <summary>
        /// Start processing the selected composition.
        /// </summary>
        /// <returns>Number of successful conversions</returns>
        public void ProcessSelected(List<CompositionInfo> compositions) {
            failures = new StringBuilder();
            LinkedList<CompositionInfo> remains = new(compositions);
            while (remains.Count != 0) {
                CompositionInfo main = remains.First.Value; // The track that contains the video for multilingual outputs
                if (Settings.Default.multilingual) {
                    // Find other tracks
                    LinkedList<CompositionInfo>.Enumerator enumer = remains.GetEnumerator();
                    List<CompositionInfo> otherLanguages = new();
                    enumer.MoveNext(); // Skip main
                    while (enumer.MoveNext()) {
                        CompositionInfo other = enumer.Current;
                        if (main.Title.Equals(other.Title) && main.Type == other.Type && main.Modifiers.Equals(other.Modifiers) &&
                            main.AspectRatio == other.AspectRatio) { // Multiple aspect ratios might be available
                            if (other.Language.StartsWith("EN-") || (!main.Language.EndsWith("XX") && other.Language.EndsWith("XX"))) {
                                otherLanguages.Add(main); // Prioritize original or non-subtitled image
                                main = other;
                            } else {
                                otherLanguages.Add(other);
                            }
                        }
                    }

                    // Process other tracks
                    string videoReference = ProcessSingle(main);
                    remains.Remove(main);
                    if (videoReference != null) {
                        bool oldVideo = Settings.Default.ripVideo;
                        Settings.Default.ripVideo = false;
                        List<(string language, string output)> otherTracks = new();
                        foreach (CompositionInfo track in otherLanguages) {
                            string currentAudio = ProcessSingle(track);
                            if (currentAudio != null) {
                                otherTracks.Add((track.Language, currentAudio));
                                remains.Remove(track);
                            }
                        }
                        Settings.Default.ripVideo = oldVideo;
                        if (otherTracks.Count == 0) {
                            continue;
                        }

                        // Merge
                        StringBuilder args = new($"-i \"{videoReference}\"");
                        foreach ((string _, string output) in otherTracks) {
                            args.Append($" -i \"{output}\"");
                        }
                        args.Append(" -map 0:v:0 -map 0:a:0 -metadata:s:a:0 language=").Append(main.Language);
                        int i = 0;
                        foreach ((string language, string _) in otherTracks) {
                            ++i;
                            args.Append($" -map {i}:a:0 -metadata:s:a:{i} language=").Append(language);
                        }
                        string mergeFileName = videoReference.Replace(main.Language, "XX-XX");
                        if (!string.IsNullOrEmpty(main.Facility)) {
                            mergeFileName = mergeFileName.Replace(main.Facility, "VDX");
                        }
                        args.Append($" -c copy \"{mergeFileName}\"");
                        if (FFmpegCalls.LaunchFFmpeg(args.ToString()) && File.Exists(mergeFileName)) {
                            File.Delete(videoReference);
                            foreach ((string _, string output) in otherTracks) {
                                File.Delete(output);
                            }
                        }
                    }
                } else {
                    ProcessSingle(main);
                    remains.RemoveFirst();
                }
            }
            OnStatusUpdate?.Invoke("Finished!");
            OnCompletion?.Invoke();
        }

        /// <summary>
        /// Start processing the compositions as a <see cref="Task"/>.
        /// </summary>
        public Task ProcessAsync() {
            if (InProgress)
                return task;
            task = new Task(Process);
            task.Start();
            return task;
        }

        /// <summary>
        /// Start processing the selected compositions as a <see cref="Task"/>.
        /// </summary>
        public Task ProcessSelectedAsync(List<CompositionInfo> compositions) {
            if (InProgress)
                return task;
            task = new Task(() => ProcessSelected(compositions));
            task.Start();
            return task;
        }

        /// <summary>
        /// Gets a string where each line is a failed content that could not be processed.
        /// </summary>
        public string GetFailedContents() => failures.ToString();

        /// <summary>
        /// Process a single composition.
        /// </summary>
        /// <returns>The output file name for single reel exports and null for multi-reel or failure.</returns>
        string ProcessSingle(CompositionInfo composition) {
            if (!File.Exists(composition.Path)) {
                failures.AppendLine(Path.GetFileName(composition.Path) + " does not exist.");
                return null;
            }
            OnStatusUpdate?.Invoke($"Processing {composition}...");
            string finalOutput = OutputPath,
                sourceFolder = Path.GetDirectoryName(composition.Path);
            if (!string.IsNullOrEmpty(OutputPath) && OutputPath.Equals(parentMarker)) {
                finalOutput = Path.GetDirectoryName(sourceFolder);
                if (finalOutput == null) {
                    failures.AppendLine($"Can't output {composition} above a root folder.");
                    return null;
                }
            }

            CompositionProcessor processor = new(composition) {
                ForcePath = finalOutput
            };
            (bool success, string output) = processor.ProcessComposition();
            if (success) {
                if (Settings.Default.zipAfter) {
                    OnStatusUpdate?.Invoke($"Zipping {composition}...");
                    Finder.ZipAssets(sourceFolder, $"{finalOutput}\\{composition.StandardTitle}.zip",
                        textOut => OnStatusUpdate(textOut));
                }
                if (Settings.Default.deleteAftter) {
                    Finder.DeleteAssets(sourceFolder);
                }
                return output;
            } else {
                failures.AppendLine($"Conversion of {composition} failed - most likely a codec error.");
                return null;
            }
        }
    }
}