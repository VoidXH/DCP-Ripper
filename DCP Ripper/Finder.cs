﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

using DCP_Ripper.Zipping;

namespace DCP_Ripper {
    /// <summary>
    /// Composition locator.
    /// </summary>
    public static class Finder {
        /// <summary>
        /// Checks if a file is a composition playlist.
        /// </summary>
        static bool IsCPL(string path) {
            if (path.ToLower().EndsWith("xml")) {
                using XmlReader reader = XmlReader.Create(path);
                try {
                    if (!reader.Read())
                        return false;
                } catch {
                    return false;
                }
                while (reader.Name.Equals("xml") || string.IsNullOrEmpty(reader.Name.Trim()))
                    if (!reader.Read())
                        return false;
                return reader.Name.Equals("CompositionPlaylist");
            }
            return false;
        }

        /// <summary>
        /// Checks a folder and its subfolders for composition playlist files.
        /// </summary>
        static void ProcessFolder(string path, List<CompositionInfo> collection) {
            string[] files, dirs;
            try {
                files = Directory.GetFiles(path);
                dirs = Directory.GetDirectories(path);
            } catch {
                return;
            }
            foreach (string file in files)
                if (IsCPL(file))
                    collection.Add(new CompositionInfo(file));
            foreach (string dir in dirs)
                ProcessFolder(dir, collection);
        }

        /// <summary>
        /// Starts checking a folder and its subfolders for composition playlist files.
        /// </summary>
        public static List<CompositionInfo> ProcessFolder(string path) {
            List<CompositionInfo> result = new();
            ProcessFolder(path, result);
            return result;
        }

        /// <summary>
        /// Delete all assets from a composition, and delete the folder if it's empty.
        /// </summary>
        public static void DeleteAssets(string path) {
            bool hasOutput = false;
            string[] directories = Directory.GetDirectories(path);
            foreach (string subdirectory in directories)
                DeleteAssets(subdirectory);
            string[] allFiles = Directory.GetFiles(path);
            foreach (string asset in allFiles) {
                if (!asset.EndsWith(".mkv") && !asset.EndsWith(".zip"))
                    File.Delete(asset);
                else
                    hasOutput = true;
            }
            if (!hasOutput)
                Directory.Delete(path);
        }

        static void AppendFolderToZip(string path, ZipArchive zip, Action<string> uiReporter, string zipPath = "") {
            string[] directories = Directory.GetDirectories(path);
            foreach (string subdirectory in directories)
                AppendFolderToZip(subdirectory, zip, uiReporter,
                    zipPath + subdirectory[(subdirectory.LastIndexOf('\\') + 1)..] + '\\');
            string[] allFiles = Directory.GetFiles(path);
            foreach (string asset in allFiles) {
                if (!asset.EndsWith(".mkv") && !asset.EndsWith(".zip")) {
                    string entryName = Path.GetFileName(asset);
                    ZipArchiveEntry entry = zip.CreateEntry(zipPath + entryName);
                    entry.LastWriteTime = DateTime.Now;
                    using Stream inputStream = File.OpenRead(asset);
                    using Stream outputStream = entry.Open();
                    using Stream progressStream = new StreamWithProgress(inputStream,
                        new FileProgressDisplay(entryName, inputStream.Length, uiReporter), null);
                    progressStream.CopyTo(outputStream);
                }
            }
        }

        /// <summary>
        /// Zips all assets from a composition.
        /// </summary>
        public static void ZipAssets(string path, string zipPath, Action<string> uiReporter) {
            if (File.Exists(zipPath))
                return;
            if (!path.EndsWith("\\"))
                path += '\\';
            using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            AppendFolderToZip(path, zip, uiReporter);
        }

        /// <summary>
        /// Get all MXF files in a folder (=assets in a composition). Might be useful when an asset map is missing.
        /// </summary>
        public static List<string> ForceGetAssets(string path) {
            string[] allFiles = Directory.GetFiles(path);
            List<string> assets = new();
            foreach (string asset in allFiles)
                if (asset.ToLower().EndsWith(".mxf"))
                    assets.Add(asset);
            return assets;
        }
    }
}