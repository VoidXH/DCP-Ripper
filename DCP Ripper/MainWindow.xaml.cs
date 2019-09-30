using DCP_Ripper.Processing;
using DCP_Ripper.Properties;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace DCP_Ripper {
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window { // TODO: async process
        /// <summary>
        /// Marks the content parent folder for output path.
        /// </summary>
        public const string parentMarker = "parent";

        /// <summary>
        /// List of composition files in the selected folder.
        /// </summary>
        List<string> compositions = new List<string>();

        /// <summary>
        /// Launch location of ffmpeg.exe.
        /// </summary>
        string ffmpegPath;

        /// <summary>
        /// List of content that could not be processed.
        /// </summary>
        string failedContent;

        /// <summary>
        /// Forced content output path. Null means default (next to video files), <see cref="parentMarker"/> means its parent.
        /// </summary>
        string outputPath = null;

        /// <summary>
        /// Window constructor.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            failureList.Visibility = Visibility.Hidden;
        }

        void CheckFFmpeg(string dir) {
            if (start.IsEnabled = File.Exists(ffmpegPath = dir + "\\ffmpeg.exe"))
                processLabel.Content = "Ready.";
            else
                processLabel.Content = "FFmpeg isn't found, please locate.";
        }

        void ComboBoxSelect(ComboBox source, string value) {
            foreach (ComboBoxItem item in source.Items)
                item.IsSelected = item.Name.Equals(value);
        }

        void OpenFolder(string path) {
            compositions = Finder.ProcessFolder(path);
            foundContent.Items.Clear();
            foreach (string composition in compositions) {
                string title = Finder.GetCPLTitle(composition);
                CompositionInfo info = new CompositionInfo(title);
                foundContent.Items.Add(new ListViewItem() {
                    Background = info.GetBrush(),
                    Content = title
                });
            }
        }

        void Window_Loaded(object sender, RoutedEventArgs e) {
            ComboBoxSelect(format, Settings.Default.format);
            ComboBoxSelect(crf, Settings.Default.crf);
            matchCRF.IsChecked = Settings.Default.matchCRF;
            ComboBoxSelect(crf3d, Settings.Default.crf3d);
            ComboBoxSelect(audio, Settings.Default.audio);
            downscale.IsChecked = Settings.Default.downscale;
            zipAfter.IsChecked = Settings.Default.zipAfter;
            deleteAfter.IsChecked = Settings.Default.deleteAftter;
            CheckFFmpeg(Settings.Default.ffmpegLocation);
            switch (Settings.Default.outputPath) {
                case "":
                    outputDefault.IsChecked = true;
                    break;
                case parentMarker:
                    outputParent.IsChecked = true;
                    break;
                default:
                    outputCustom.IsChecked = true;
                    OutputCustom_Checked(Settings.Default.outputPath, null);
                    break;
            }
            outputCustom.Checked += OutputCustom_Checked;
            Refresh_Click(null, null);
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    OpenFolder(dialog.SelectedPath);
                    Settings.Default.lastOpenFolder = dialog.SelectedPath;
                    Settings.Default.Save();
                }
            }
        }

        void AboutLink_Click(object sender, RoutedEventArgs e) => Process.Start("http://en.sbence.hu");

        void Refresh_Click(object sender, RoutedEventArgs e) {
            if (Directory.Exists(Settings.Default.lastOpenFolder))
                OpenFolder(Settings.Default.lastOpenFolder);
        }

        void MatchCRF_Checked(object sender, RoutedEventArgs e) => crf3d.IsEnabled = false;
        void MatchCRF_Unchecked(object sender, RoutedEventArgs e) => crf3d.IsEnabled = true;

        void Start_Click(object sender, RoutedEventArgs e) {
            failureList.Visibility = Visibility.Hidden;
            string videoFormat = ((ComboBoxItem)format.SelectedItem).Name.StartsWith("x265") ? "libx265" : "libx264";
            int crfTarget = int.Parse(((ComboBoxItem)crf.SelectedItem).Name.Substring(3));
            int crf3dTarget = crfTarget;
            if (matchCRF.IsChecked.Value)
                crf3dTarget = int.Parse(((ComboBoxItem)crf3d.SelectedItem).Name.Split('_')[0].Substring(3));
            ListProcessor processor = new ListProcessor(compositions);
            int finished = processor.Process(ffmpegPath, outputPath, videoFormat, ((ComboBoxItem)format.SelectedItem).Name.Contains("420"),
                crfTarget, crf3dTarget, ((ComboBoxItem)audio.SelectedItem).Name, downscale.IsChecked.Value,
                zipAfter.IsChecked.Value, deleteAfter.IsChecked.Value);
            if (finished == compositions.Count)
                processLabel.Content = "Finished!";
            else {
                int failureCount = compositions.Count - finished;
                processLabel.Content = string.Format("Finished with {0} failure{1}!", failureCount, failureCount > 1 ? "s" : string.Empty);
                failedContent = processor.GetFailedContents();
                failureList.Visibility = Visibility.Visible;
            }
        }

        void LocateFFmpeg_Click(object sender, RoutedEventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    CheckFFmpeg(dialog.SelectedPath);
                    Settings.Default.ffmpegLocation = dialog.SelectedPath;
                    Settings.Default.Save();
                }
            }
        }

        void OutputDefault_Checked(object sender, RoutedEventArgs e) => outputPath = null;

        void OutputParent_Checked(object sender, RoutedEventArgs e) => outputPath = parentMarker;

        void OutputCustom_Checked(object sender, RoutedEventArgs e) {
            if (sender is string) {
                if (Directory.Exists((string)sender))
                    outputPath = (string)sender;
                else
                    outputDefault.IsChecked = true;
            } else using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    outputPath = dialog.SelectedPath;
                else if (string.IsNullOrEmpty(outputPath))
                    outputDefault.IsChecked = true;
                else
                    outputParent.IsChecked = true;
            }
        }

        void FailureList_Click(object sender, RoutedEventArgs e) => MessageBox.Show(failedContent, "Failed contents");

        void Window_Closed(object sender, System.EventArgs e) {
            Settings.Default.format = ((ComboBoxItem)format.SelectedItem).Name;
            Settings.Default.crf = ((ComboBoxItem)crf.SelectedItem).Name;
            Settings.Default.matchCRF = matchCRF.IsChecked.Value;
            Settings.Default.crf3d = ((ComboBoxItem)crf3d.SelectedItem).Name;
            Settings.Default.audio = ((ComboBoxItem)audio.SelectedItem).Name;
            Settings.Default.downscale = downscale.IsChecked.Value;
            Settings.Default.outputPath = outputPath;
            Settings.Default.zipAfter = zipAfter.IsChecked.Value;
            Settings.Default.deleteAftter = deleteAfter.IsChecked.Value;
            Settings.Default.Save();
        }
    }
}