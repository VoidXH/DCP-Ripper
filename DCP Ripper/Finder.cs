using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

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
                using (XmlReader reader = XmlReader.Create(path)) {
                    if (!reader.Read())
                        return false;
                    while (reader.Name.Equals("xml") || string.IsNullOrEmpty(reader.Name.Trim()))
                        if (!reader.Read())
                            return false;
                    return reader.Name.Equals("CompositionPlaylist");
                }
            }
            return false;
        }

        /// <summary>
        /// Get the content title of a composition playlist.
        /// </summary>
        public static string GetCPLTitle(string path) {
            using (XmlReader reader = XmlReader.Create(path)) {
                while (!reader.Name.Equals("ContentTitleText"))
                    if (!reader.Read())
                        return path.Substring(path.LastIndexOf("\\") + 1);
                reader.Read();
                return reader.Value;
            }
        }

        /// <summary>
        /// Checks a folder and its subfolders for composition playlist files.
        /// </summary>
        static void ProcessFolder(string path, List<string> collection) {
            string[] files, dirs;
            try {
                files = Directory.GetFiles(path);
                dirs = Directory.GetDirectories(path);
            } catch {
                return;
            }
            foreach (string file in files)
                if (IsCPL(file))
                    collection.Add(file);
            foreach (string dir in dirs)
                ProcessFolder(dir, collection);
        }

        /// <summary>
        /// Starts checking a folder and its subfolders for composition playlist files.
        /// </summary>
        public static List<string> ProcessFolder(string path) {
            List<string> result = new List<string>();
            ProcessFolder(path, result);
            return result;
        }

        /// <summary>
        /// Delete all assets from a composition, and delete the folder if it's empty.
        /// </summary>
        public static void DeleteAssets(string path) {
            bool hasOutput = false;
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

        /// <summary>
        /// Zips all assets from a composition.
        /// </summary>
        public static void ZipAssets(string path, string zipPath) {
            if (File.Exists(zipPath))
                return;
            if (!path.EndsWith("\\"))
                path += '\\';
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                string[] allFiles = Directory.GetFiles(path);
                foreach (string asset in allFiles)
                    if (!asset.EndsWith(".mkv") && !asset.EndsWith(".zip"))
                        zip.CreateEntryFromFile(asset, asset.Substring(path.Length),
                            CompressionLevel.Optimal);
            }
        }

        /// <summary>
        /// Get all MXF files in a folder (=assets in a composition). Might be useful when an asset map is missing.
        /// </summary>
        public static List<string> ForceGetAssets(string path) {
            string[] allFiles = Directory.GetFiles(path);
            List<string> assets = new List<string>();
            foreach (string asset in allFiles)
                if (asset.ToLower().EndsWith(".mxf"))
                    assets.Add(asset);
            return assets;
        }
    }
}