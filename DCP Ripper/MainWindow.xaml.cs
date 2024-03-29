﻿using DCP_Ripper.Processing;
using DCP_Ripper.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace DCP_Ripper {
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// Selected 2D constant rate factor.
        /// </summary>
        int CRF => int.Parse(((ComboBoxItem)crf.SelectedItem).Name[3..]);

        /// <summary>
        /// List of content that could not be processed.
        /// </summary>
        string failedContent;

        /// <summary>
        /// Content conversion manager.
        /// </summary>
        readonly ListProcessor processor = new();

        /// <summary>
        /// Window constructor.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            Width = Settings.Default.width;
            Height = Settings.Default.height;
            failureList.Visibility = Visibility.Hidden;
            processor.OnStatusUpdate += ProcessStatusUpdate;
            processor.OnCompletion += AfterProcess;
            version.Content = 'v' + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        }

        void CheckFFmpeg() {
            if (start.IsEnabled = startSelected.IsEnabled = File.Exists(Settings.Default.ffmpegLocation))
                processLabel.Text = "Ready.";
            else
                processLabel.Text = "FFmpeg isn't found, please locate.";
        }

        static void ComboBoxSelect(ComboBox source, string value) {
            foreach (ComboBoxItem item in source.Items)
                item.IsSelected = item.Name.Equals(value);
        }

        void OpenFolder(string path) => foundContent.ItemsSource = processor.Compositions = Finder.ProcessFolder(path);

        void Window_Loaded(object sender, RoutedEventArgs e) {
            ripVideo.IsChecked = Settings.Default.ripVideo;
            ComboBoxSelect(format, Settings.Default.format);
            ComboBoxSelect(crf, "crf" + Settings.Default.crf);
            matchCRF.IsChecked = Settings.Default.matchCRF;
            ComboBoxSelect(crf3d, $"crf{Settings.Default.crf3d}_3d");
            ComboBoxSelect(mode3d, Settings.Default.mode3d);
            downscale.IsChecked = Settings.Default.downscale;
            ripAudio.IsChecked = Settings.Default.ripAudio;
            multilingual.IsChecked = Settings.Default.multilingual;
            ComboBoxSelect(audio, Settings.Default.audio);
            downmix.SelectedIndex = Settings.Default.downmix;
            zipAfter.IsChecked = Settings.Default.zipAfter;
            deleteAfter.IsChecked = Settings.Default.deleteAftter;
            overwrite.IsChecked = Settings.Default.overwrite;
            CheckFFmpeg();
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
            Refresh_Click(null, null);
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e) {
            using var dialog = new FolderBrowserDialog();
            if (Directory.Exists(Settings.Default.lastOpenFolder))
                dialog.SelectedPath = Settings.Default.lastOpenFolder;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                OpenFolder(dialog.SelectedPath);
                Settings.Default.lastOpenFolder = dialog.SelectedPath;
                Settings.Default.Save();
            }
        }

        void AboutLink_Click(object sender, RoutedEventArgs e) => Process.Start("http://en.sbence.hu");

        void Refresh_Click(object sender, RoutedEventArgs e) {
            if (Directory.Exists(Settings.Default.lastOpenFolder))
                OpenFolder(Settings.Default.lastOpenFolder);
        }

        void MatchCRF() => ComboBoxSelect(crf3d, $"crf{CRF}_3d");

        void CRF_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (matchCRF != null && matchCRF.IsChecked.Value)
                MatchCRF();
        }

        void MatchCRF_Checked(object sender, RoutedEventArgs e) {
            crf3d.IsEnabled = false;
            MatchCRF();
        }

        void MatchCRF_Unchecked(object sender, RoutedEventArgs e) {
            crf3d.IsEnabled = true;
            MatchCRF();
        }

        async void Start_Click(object sender, RoutedEventArgs e) {
            if (processor.InProgress) {
                MessageBox.Show("A conversion is already in progress.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            failureList.Visibility = Visibility.Hidden;
            ApplySettings();
            await processor.ProcessAsync();
        }

        async void StartSelected_Click(object sender, RoutedEventArgs e) {
            if (processor.InProgress) {
                MessageBox.Show("A conversion is already in progress.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            failureList.Visibility = Visibility.Hidden;
            ApplySettings();
            List<CompositionInfo> compositions = new();
            foreach (CompositionInfo info in foundContent.SelectedItems)
                compositions.Add(info);
            if (compositions.Count == 0) {
                MessageBox.Show("No content was selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            await processor.ProcessSelectedAsync(compositions);
        }

        /// <summary>
        /// Set the content of <see cref="processLabel"/> from another thread.
        /// </summary>
        void ProcessStatusUpdate(string status) => Dispatcher.Invoke(() => processLabel.Text = status);

        /// <summary>
        /// Called after ripping the selected folder.
        /// </summary>
        void AfterProcess() {
            int processed = processor.Compositions.Count;
            if (Settings.Default.deleteAftter)
                Dispatcher.Invoke(() => Refresh_Click(null, null));
            failedContent = processor.GetFailedContents();
            int failureCount = failedContent != null ? failedContent.Count(c => c == '\n') : 0;
            if (failureCount == 0)
                return;
            failedContent = processor.GetFailedContents();
            ProcessStatusUpdate($"Finished with {failureCount} failure{(failureCount > 1 ? "s" : string.Empty)}!");
            Dispatcher.Invoke(() => failureList.Visibility = Visibility.Visible);
        }

        void LocateFFmpeg_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Filter = "FFmpeg|ffmpeg.exe"
            };
            if (dialog.ShowDialog().Value) {
                Settings.Default.ffmpegLocation = dialog.FileName;
                Settings.Default.Save();
                CheckFFmpeg();
            }
        }

        void OutputDefault_Checked(object sender, RoutedEventArgs e) {
            processor.OutputPath = null;
            if (outputCustom != null)
                outputCustom.Content = "Custom";
        }

        void OutputParent_Checked(object sender, RoutedEventArgs e) {
            processor.OutputPath = ListProcessor.parentMarker;
            if (outputCustom != null)
                outputCustom.Content = "Custom";
        }

        void OutputCustom_Checked(object sender, RoutedEventArgs e) {
            if (sender is string senderStr) {
                if (Directory.Exists(senderStr))
                    processor.OutputPath = senderStr;
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
            if (outputCustom.IsChecked.Value) {
                string outPath = processor.OutputPath;
                outputCustom.Content = $"Custom ({(outPath.Length < 4 ? outPath : Path.GetFileName(outPath))})";
            }
        }

        void FailureList_Click(object sender, RoutedEventArgs e) => MessageBox.Show(failedContent, "Failed contents");

        void ApplySettings() {
            Settings.Default.ripVideo = ripVideo.IsChecked.Value;
            Settings.Default.format = ((ComboBoxItem)format.SelectedItem).Name;
            Settings.Default.crf = int.Parse(((ComboBoxItem)crf.SelectedItem).Name[3..]);
            Settings.Default.matchCRF = matchCRF.IsChecked.Value;
            Settings.Default.crf3d = int.Parse(((ComboBoxItem)crf3d.SelectedItem).Name[3..^3]);
            Settings.Default.mode3d = ((ComboBoxItem)mode3d.SelectedItem).Name;
            Settings.Default.downscale = downscale.IsChecked.Value;
            Settings.Default.ripAudio = ripAudio.IsChecked.Value;
            Settings.Default.multilingual = multilingual.IsChecked.Value;
            Settings.Default.audio = ((ComboBoxItem)audio.SelectedItem).Name;
            Settings.Default.downmix = downmix.SelectedIndex;
            Settings.Default.outputPath = processor.OutputPath;
            Settings.Default.zipAfter = zipAfter.IsChecked.Value;
            Settings.Default.deleteAftter = deleteAfter.IsChecked.Value;
            Settings.Default.overwrite = overwrite.IsChecked.Value;
            Settings.Default.width = Width;
            Settings.Default.height = Height;
        }

        void Window_Closed(object sender, EventArgs e) {
            ApplySettings();
            Settings.Default.Save();
        }
    }
}