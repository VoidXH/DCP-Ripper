using DCP_Ripper.Processing;
using DCP_Ripper.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace DCP_Ripper {
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// List of content that could not be processed.
        /// </summary>
        string failedContent;

        /// <summary>
        /// Content conversion manager.
        /// </summary>
        readonly ListProcessor processor = new ListProcessor();

        /// <summary>
        /// Window constructor.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            failureList.Visibility = Visibility.Hidden;
            processor.OnStatusUpdate += ProcessStatusUpdate;
            processor.OnCompletion += AfterProcess;
        }

        void CheckFFmpeg(string dir) {
            if (start.IsEnabled = File.Exists(processor.FFmpegPath = dir + "\\ffmpeg.exe"))
                processLabel.Text = "Ready.";
            else
                processLabel.Text = "FFmpeg isn't found, please locate.";
        }

        void ComboBoxSelect(ComboBox source, string value) {
            foreach (ComboBoxItem item in source.Items)
                item.IsSelected = item.Name.Equals(value);
        }

        void OpenFolder(string path) {
            processor.Compositions = Finder.ProcessFolder(path);
            foundContent.Items.Clear();
            foreach (string composition in processor.Compositions) {
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
            ComboBoxSelect(mode3d, Settings.Default.mode3d);
            downscale.IsChecked = processor.Force2K = Settings.Default.downscale;
            ComboBoxSelect(audio, Settings.Default.audio);
            zipAfter.IsChecked = processor.ZipAfter = Settings.Default.zipAfter;
            deleteAfter.IsChecked = processor.DeleteAfter = Settings.Default.deleteAftter;
            CheckFFmpeg(Settings.Default.ffmpegLocation);
            switch (Settings.Default.outputPath) {
                case "":
                    outputDefault.IsChecked = true;
                    break;
                case ListProcessor.parentMarker:
                    outputParent.IsChecked = true;
                    break;
                default:
                    outputCustom.IsChecked = true;
                    OutputCustom_Checked(Settings.Default.outputPath, null);
                    break;
            }
            outputCustom.Checked += OutputCustom_Checked;
            mode3d.SelectionChanged += Mode3D_SelectionChanged;
            Refresh_Click(null, null);
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                if (Directory.Exists(Settings.Default.lastOpenFolder))
                    dialog.SelectedPath = Settings.Default.lastOpenFolder;
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

        void Format_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            string format = ((ComboBoxItem)((ComboBox)sender).SelectedItem).Name;
            processor.VideoFormat = format.StartsWith("x265") ? "libx265" : "libx264";
            processor.ChromaSubsampling = format.Contains("420");
        }

        void CRF_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            processor.CRF = int.Parse(((ComboBoxItem)((ComboBox)sender).SelectedItem).Name.Substring(3));
            if (matchCRF != null && matchCRF.IsChecked.Value)
                processor.CRF3D = processor.CRF;
        }

        void MatchCRF_Checked(object sender, RoutedEventArgs e) {
            crf3d.IsEnabled = false;
            CRF3D_SelectionChanged(crf3d, null);
        }

        void MatchCRF_Unchecked(object sender, RoutedEventArgs e) {
            crf3d.IsEnabled = true;
            CRF3D_SelectionChanged(crf3d, null);
        }

        void CRF3D_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (matchCRF != null && matchCRF.IsChecked.Value)
                processor.CRF3D = processor.CRF;
            else
                processor.CRF3D = int.Parse(((ComboBoxItem)((ComboBox)sender).SelectedItem).Name.Split('_')[0].Substring(3));
        }

        void Mode3D_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            processor.StereoMode = (Mode3D)Enum.Parse(typeof(Mode3D), ((ComboBoxItem)((ComboBox)sender).SelectedItem).Name);
        void Downscale_Checked(object sender, RoutedEventArgs e) => processor.Force2K = true;
        void Downscale_Unchecked(object sender, RoutedEventArgs e) => processor.Force2K = false;
        void Audio_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            processor.AudioFormat = ((ComboBoxItem)((ComboBox)sender).SelectedItem).Name;
        void ZipAfter_Checked(object sender, RoutedEventArgs e) => processor.ZipAfter = true;
        void ZipAfter_Unchecked(object sender, RoutedEventArgs e) => processor.ZipAfter = false;
        void DeleteAfter_Checked(object sender, RoutedEventArgs e) => processor.DeleteAfter = true;
        void DeleteAfter_Unchecked(object sender, RoutedEventArgs e) => processor.DeleteAfter = false;

        async void Start_Click(object sender, RoutedEventArgs e) {
            failureList.Visibility = Visibility.Hidden;
            await processor.ProcessAsync();
        }

        /// <summary>
        /// Set the content of <see cref="processLabel"/> from another thread.
        /// </summary>
        void ProcessStatusUpdate(string status) => Dispatcher.Invoke(() => processLabel.Text = status);

        /// <summary>
        /// Called after ripping the selected folder.
        /// </summary>
        /// <param name="finished">Number of successful conversions</param>
        void AfterProcess(int finished) {
            int processed = processor.Compositions.Count;
            if (processor.DeleteAfter)
                Dispatcher.Invoke(() => Refresh_Click(null, null));
            if (finished == processed)
                return;
            int failureCount = processor.Compositions.Count - finished;
            ProcessStatusUpdate(string.Format("Finished with {0} failure{1}!", failureCount, failureCount > 1 ? "s" : string.Empty));
            failedContent = processor.GetFailedContents();
            Dispatcher.Invoke(() => failureList.Visibility = Visibility.Visible);
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

        void OutputDefault_Checked(object sender, RoutedEventArgs e) => processor.OutputPath = null;
        void OutputParent_Checked(object sender, RoutedEventArgs e) => processor.OutputPath = ListProcessor.parentMarker;

        void OutputCustom_Checked(object sender, RoutedEventArgs e) {
            if (sender is string) {
                if (Directory.Exists((string)sender))
                    processor.OutputPath = (string)sender;
                else
                    outputDefault.IsChecked = true;
            } else using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    processor.OutputPath = dialog.SelectedPath;
                else if (string.IsNullOrEmpty(processor.OutputPath))
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
            Settings.Default.mode3d = ((ComboBoxItem)mode3d.SelectedItem).Name;
            Settings.Default.downscale = processor.Force2K;
            Settings.Default.audio = ((ComboBoxItem)audio.SelectedItem).Name;
            Settings.Default.outputPath = processor.OutputPath;
            Settings.Default.zipAfter = processor.ZipAfter;
            Settings.Default.deleteAftter = processor.DeleteAfter;
            Settings.Default.Save();
        }
    }
}