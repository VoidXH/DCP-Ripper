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
        const string parentMarker = "parent";
        List<string> compositions = new List<string>();
        string ffmpegPath;

        /// <summary>
        /// Forced content output path. Null means default (next to video files), <see cref="parentMarker"/> means its parent.
        /// </summary>
        string outputPath = null;

        /// <summary>
        /// Window constructor.
        /// </summary>
        public MainWindow() => InitializeComponent();

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
                foundContent.Items.Add(new ListViewItem() {
                    Content = Finder.GetCPLTitle(composition),
                    IsEnabled = false
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
            int finished = 0;
            foreach (string composition in compositions) {
                processLabel.Content = string.Format("Processing {0}...", composition.Substring(composition.LastIndexOf("\\") + 1));
                Processor processor = new Processor(ffmpegPath, composition) {
                    VideoFormat = ((ComboBoxItem)format.SelectedItem).Name.StartsWith("x265") ? "libx265" : "libx264",
                    ChromaSubsampling = ((ComboBoxItem)format.SelectedItem).Name.Contains("420"),
                    CRF = int.Parse(((ComboBoxItem)crf.SelectedItem).Name.Substring(3)),
                    AudioFormat = ((ComboBoxItem)audio.SelectedItem).Name
                };
                if (matchCRF.IsChecked.Value)
                    processor.CRF3D = processor.CRF;
                else
                    processor.CRF3D = int.Parse(((ComboBoxItem)crf3d.SelectedItem).Name.Split('_')[0].Substring(3));
                string finalOutput = outputPath;
                if (!string.IsNullOrEmpty(outputPath) && outputPath.Equals(parentMarker)) {
                    finalOutput = composition.Substring(0, composition.LastIndexOf('\\', composition.Length - 1));
                    finalOutput = finalOutput.Substring(0, finalOutput.LastIndexOf('\\', finalOutput.Length - 1));
                }
                if (processor.ProcessComposition(downscale.IsChecked.Value, finalOutput))
                    ++finished;
            }
            if (finished == compositions.Count)
                processLabel.Content = "Finished!";
            else {
                int failureCount = compositions.Count - finished;
                processLabel.Content = string.Format("Finished with {0} failure{1}!", failureCount, failureCount > 1 ? "s" : string.Empty);
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

        void Window_Closed(object sender, System.EventArgs e) {
            Settings.Default.format = ((ComboBoxItem)format.SelectedItem).Name;
            Settings.Default.crf = ((ComboBoxItem)crf.SelectedItem).Name;
            Settings.Default.matchCRF = matchCRF.IsChecked.Value;
            Settings.Default.crf3d = ((ComboBoxItem)crf3d.SelectedItem).Name;
            Settings.Default.audio = ((ComboBoxItem)audio.SelectedItem).Name;
            Settings.Default.downscale = downscale.IsChecked.Value;
            Settings.Default.outputPath = outputPath;
            Settings.Default.Save();
        }
    }
}