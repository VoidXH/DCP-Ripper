using System.Collections.Generic;
using System.IO;
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
            string[] files = Directory.GetFiles(path), dirs = Directory.GetDirectories(path);
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