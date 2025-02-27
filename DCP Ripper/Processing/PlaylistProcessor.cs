﻿using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace DCP_Ripper.Processing {
    /// <summary>
    /// Parses a composition playlist.
    /// </summary>
    class PlaylistProcessor {
        /// <summary>
        /// Composition title.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// List of reel data in this composition.
        /// </summary>
        public List<Reel> Contents { get; } = new List<Reel>();

        /// <summary>
        /// Get the file names for UUIDs in a given composition.
        /// </summary>
        static Dictionary<string, string> ParseAssetMap(string directory) {
            Dictionary<string, string> map = new();
            string fileName = directory + "ASSETMAP";
            if (!File.Exists(fileName)) {
                fileName += ".xml";
                if (!File.Exists(fileName))
                    return map;
            }
            string nextId = string.Empty;
            using (XmlReader reader = XmlReader.Create(fileName)) {
                while (reader.Read()) {
                    if (reader.NodeType != XmlNodeType.Element)
                        continue;
                    switch (reader.Name) {
                        case "Id":
                            reader.Read();
                            nextId = reader.Value;
                            break;
                        case "Path":
                            reader.Read();
                            if (!map.ContainsKey(nextId))
                                map.Add(nextId, reader.Value);
                            break;
                        default:
                            break;
                    }
                }
            }
            return map;
        }

        public PlaylistProcessor(string cplPath) {
            string directory = cplPath[..(cplPath.LastIndexOf('\\') + 1)];
            Dictionary<string, string> assets = ParseAssetMap(directory);
            Reel reel = new();
            using XmlReader reader = XmlReader.Create(cplPath);
            bool video = false,
                audio = false;
            while (reader.Read()) {
                if (reader.NodeType != XmlNodeType.Element) {
                    if (reader.NodeType == XmlNodeType.EndElement) {
                        switch (reader.Name) {
                            case "Reel":
                                Contents.Add(reel);
                                break;
                            case "MainPicture":
                            case "MainStereoscopicPicture":
                                video = false;
                                break;
                            case "MainSound":
                                audio = false;
                                break;
                        }
                    }
                    continue;
                } else if (reader.Name.EndsWith("MainStereoscopicPicture")) {
                    video = true;
                    reel.is3D = true;
                    continue;
                }
                switch (reader.Name) {
                    case "Id":
                        reader.Read();
                        if (assets.ContainsKey(reader.Value)) {
                            if (video) {
                                reel.videoFile = directory + assets[reader.Value];
                            } else if (audio) {
                                reel.audioFile = directory + assets[reader.Value];
                            }
                        } else { // Try to parse a single reel content with a missing asset map
                            List<string> bulkAssets = Finder.ForceGetAssets(directory);
                            if (bulkAssets.Count == 2) {
                                long size0 = new FileInfo(bulkAssets[0]).Length, size1 = new FileInfo(bulkAssets[1]).Length;
                                if (video) {
                                    reel.videoFile = bulkAssets[size0 < size1 ? 1 : 0];
                                } else if (audio) {
                                    reel.audioFile = bulkAssets[size1 < size0 ? 1 : 0];
                                }
                            }
                        }
                        break;
                    case "EntryPoint":
                        reader.Read();
                        if (video) {
                            reel.videoStartFrame = int.Parse(reader.Value);
                        } else if (audio) {
                            reel.audioStartFrame = int.Parse(reader.Value);
                        }
                        break;
                    case "Duration":
                        reader.Read();
                        reel.duration = int.Parse(reader.Value);
                        break;
                    case "FrameRate":
                        reader.Read();
                        if (string.IsNullOrWhiteSpace(reader.Value)) // Handles <FrameRate />
                            break;
                        int split = reader.Value.IndexOf(' ');
                        reel.framerate = int.Parse(reader.Value[..split]) / float.Parse(reader.Value[split..]);
                        if (reel.is3D)
                            reel.framerate *= .5f;
                        break;
                    case "Reel":
                        reel = new Reel();
                        break;
                    case "MainPicture":
                        video = true;
                        break;
                    case "MainSound":
                        audio = true;
                        break;
                    case "MainSubtitle":
                    case "ns1:AuxData":
                    case "axd:AuxData":
                    case "axd-cpl:AuxData":
                        string name = reader.Name;
                        while (reader.Read() && (reader.NodeType != XmlNodeType.EndElement || !reader.Name.Equals(name))) {
                            // Skip these blocks, they can't be parsed
                        }
                        break;
                    case "KeyId":
                        reel.needsKey = true;
                        break;
                    case "ContentTitleText":
                        reader.Read();
                        Title = reader.Value;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}