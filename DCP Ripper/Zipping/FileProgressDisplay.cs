using System;

namespace DCP_Ripper.Zipping {
    /// <summary>
    /// Writes file handling progress to the UI.
    /// </summary>
    public class FileProgressDisplay : IProgress<long> {
        /// <summary>
        /// Display name of the file.
        /// </summary>
        readonly string fileName;
        /// <summary>
        /// File size in bytes.
        /// </summary>
        readonly long fileSize;
        /// <summary>
        /// Message displaying method.
        /// </summary>
        readonly Action<string> uiReporter;

        /// <summary>
        /// Accumulated progress.
        /// </summary>
        long totalProgress;

        /// <summary>
        /// File handling progress display.
        /// </summary>
        /// <param name="fileName">Display name of the file</param>
        /// <param name="fileSize">File size in bytes</param>
        /// <param name="uiReporter">Message displaying method</param>
        public FileProgressDisplay(string fileName, long fileSize, Action<string> uiReporter) {
            this.fileName = fileName;
            this.fileSize = fileSize;
            this.uiReporter = uiReporter;
        }

        /// <summary>
        /// Increase progress.
        /// </summary>
        /// <param name="progress">Read/written bytes</param>
        public void Report(long progress) {
            totalProgress += progress;
            uiReporter.Invoke(string.Format("Zipping ({1}%): {0}...", fileName,
                (totalProgress * 100 / (double)fileSize).ToString("0.00")));
        }
    }
}
