using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace DCP_Ripper {
    /// <summary>
    /// Get all available information about a composition from DCNC-compatible file names.
    /// </summary>
    [SuppressMessage("Design", "CA1067", Justification = "No.")]
    public class CompositionInfo : IEquatable<CompositionInfo> {
        /// <summary>
        /// The standardized title that is shown on the playout server.
        /// </summary>
        public string StandardTitle { get; private set; }
        /// <summary>
        /// Location of the composition playlist file.
        /// </summary>
        public string Path { get; private set; }
        /// <summary>
        /// Complete title.
        /// </summary>
        public string Title { get; private set; }
        /// <summary>
        /// Content type.
        /// </summary>
        public ContentType Type { get; private set; }
        /// <summary>
        /// Unconventional content modifiers.
        /// </summary>
        public string Modifiers { get; private set; }
        /// <summary>
        /// Framing name and aspect ratio.
        /// </summary>
        public Framing AspectRatio { get; private set; }
        /// <summary>
        /// Content language.
        /// </summary>
        public string Language { get; private set; }
        /// <summary>
        /// Distribution area.
        /// </summary>
        public string Territory { get; private set; }
        /// <summary>
        /// Main audio track format.
        /// </summary>
        public AudioTrack Audio { get; private set; }
        /// <summary>
        /// Video width.
        /// </summary>
        public Resolution Resolution { get; private set; }
        /// <summary>
        /// Content creator.
        /// </summary>
        public string Studio { get; private set; }
        /// <summary>
        /// Creation date.
        /// </summary>
        public DateTime Creation { get; private set; }
        /// <summary>
        /// DCP creator.
        /// </summary>
        public string Facility { get; private set; }
        /// <summary>
        /// Used DCP standard.
        /// </summary>
        public string Standard { get; private set; }
        /// <summary>
        /// Original version or version file.
        /// </summary>
        public Version PackageType { get; private set; }

        /// <summary>
        /// The contained material for the title.
        /// </summary>
        public string Material => $"{Type.ToString()[4..]} {Modifiers}";

        // Enum cache
        static readonly string[] contentTypes = Enum.GetNames(typeof(ContentType));
        static readonly string[] aspects = Enum.GetNames(typeof(Framing));
        static readonly string[] versions = Enum.GetNames(typeof(Version));

        /// <summary>
        /// Parse a DCNC file name.
        /// </summary>
        public CompositionInfo(string path) {
            StandardTitle = GetContentTitle(Path = path);
            string[] modifiers = StandardTitle.Split('_');
            bool wasDate = false,
                wasLanguage = false;
            for (int i = 0, c = modifiers.Length; i < c; ++i) {
                if (modifiers[i].Length == 8 && int.TryParse(modifiers[i], out int date)) {
                    try {
                        Creation = new DateTime(date / 10000, date % 10000 / 100, date % 100);
                    } catch {
                        Creation = new DateTime(date / 10000, date % 10000 / 100, date % 100 - 1); // Fixes things like sept. 31
                    }
                    wasDate = true;
                } else if (Enum.TryParse('_' + modifiers[i], out Resolution detectedRes))
                    Resolution = detectedRes;
                else if (modifiers[i].StartsWith("iop", StringComparison.CurrentCultureIgnoreCase) ||
                    modifiers[i].StartsWith("smpte", StringComparison.CurrentCultureIgnoreCase))
                    Standard = modifiers[i].ToUpper();
                else if (TryParseEnum(modifiers[i], versions, out Version packageType))
                    PackageType = packageType;
                else if (TryParseAudio(modifiers[i], out AudioTrack format))
                    Audio = format;
                else if (TryParseContentType(modifiers[i], out ContentType type, out string contentMods)) {
                    Type = type;
                    Modifiers = contentMods;
                } else if (TryParseEnum(modifiers[i], aspects, out Framing aspectRatio))
                    AspectRatio = aspectRatio;
                else if (wasLanguage || (wasLanguage = IsLanguage(modifiers[i]))) {
                    if (string.IsNullOrEmpty(Language))
                        Language = modifiers[i];
                    else {
                        Territory = modifiers[i];
                        wasLanguage = false;
                    }
                } else if (wasDate && string.IsNullOrEmpty(Facility))
                    Facility = modifiers[i];
                else if (Type != ContentType.UNK_Unknown)
                    Studio = modifiers[i];
                else if (string.IsNullOrEmpty(Title))
                    Title = modifiers[i];
                else
                    Title += "_" + modifiers[i];
            }
            if (string.IsNullOrEmpty(Title))
                Title = modifiers[0];
            if (string.IsNullOrEmpty(Language))
                Language = "XX";
            if (string.IsNullOrEmpty(Territory))
                Territory = "XX";
        }

        /// <summary>
        /// Get the content title from the playlist file.
        /// </summary>
        static string GetContentTitle(string path) {
            using XmlReader reader = XmlReader.Create(path);
            while (!reader.Name.Equals("ContentTitleText"))
                if (!reader.Read())
                    return path[(path.LastIndexOf("\\") + 1)..];
            reader.Read();
            return reader.Value;
        }

        /// <summary>
        /// Parse a modifier into an enumeration if it contains a matching value.
        /// </summary>
        static bool TryParseEnum<T>(string modifier, string[] modifiers, out T output) where T : struct {
            int splitPos = modifier.IndexOf('-');
            if (splitPos != -1)
                modifier = modifier[(splitPos + 1)..];
            modifier = '_' + modifier;
            foreach (string mod in modifiers) {
                int secondUnderscore = mod.LastIndexOf('_');
                string compMod = secondUnderscore == 0 ? mod : mod[..secondUnderscore];
                if (compMod.Equals(modifier)) {
                    output = (T)Enum.Parse(typeof(T), mod);
                    return true;
                }
            }
            output = (T)(object)0;
            return false;
        }

        /// <summary>
        /// Parse a modifier as an audio format.
        /// </summary>
        static bool TryParseAudio(string modifier, out AudioTrack track) {
            track = AudioTrack.Unknown;
            modifier = modifier.ToLower();
            if (modifier.StartsWith("20")) track = AudioTrack.Stereo;
            else if (modifier.StartsWith("51")) track = AudioTrack.Surround__5_1;
            else if (modifier.StartsWith("71")) track = AudioTrack.Surround__7_1;
            else if (modifier.StartsWith("sdds", StringComparison.CurrentCultureIgnoreCase)) track = AudioTrack.SDDS;
            else if (modifier.StartsWith("cavern"))
                track = modifier.Contains("xl") ? AudioTrack.CavernXL : AudioTrack.Cavern;
            else if (modifier.StartsWith("imax5")) track = AudioTrack.IMAX5;
            else if (modifier.StartsWith("imax6")) track = AudioTrack.IMAX6;
            else if (modifier.StartsWith("imax12")) track = AudioTrack.IMAX12;
            // These formats might upgrade a present standard layout, like "71-Atmos"
            if (modifier.Contains("atmos")) track = AudioTrack.Atmos;
            if (modifier.Contains("auro")) track = AudioTrack.Auro;
            if (modifier.Contains("auromax")) track = AudioTrack.AuroMax;
            if (modifier.Contains("dtsx")) track = AudioTrack.DTS___X;
            return track != AudioTrack.Unknown;
        }

        /// <summary>
        /// Parse a modifier as content type and separate unconventional modifiers.
        /// </summary>
        static bool TryParseContentType(string modifier, out ContentType contentType, out string modifiers) {
            modifiers = string.Empty;
            if (modifier.Length >= 3) {
                string stamp = modifier[..3];
                foreach (string type in contentTypes) {
                    if (type.StartsWith(stamp)) {
                        contentType = (ContentType)Enum.Parse(typeof(ContentType), type);
                        string[] contentMods = modifier[3..].Split('-');
                        foreach (string mod in contentMods)
                            if (modifiers.Length == 0) modifiers = mod;
                            else modifiers += ", " + mod;
                        return true;
                    }
                }
            }
            contentType = ContentType.UNK_Unknown;
            return false;
        }

        /// <summary>
        /// Check if a modifier represents language information.
        /// </summary>
        static bool IsLanguage(string modifier) {
            int firstSplit = modifier.IndexOf('-');
            if (firstSplit == -1) return false;
            int secondSplit = modifier.IndexOf('-', firstSplit + 1);
            return firstSplit <= 3 && (secondSplit != -1 ? secondSplit : modifier.Length) - firstSplit <= 4;
        }

        /// <summary>
        /// Checks if two compositions are the same, except for the language and creator.
        /// </summary>
        public bool Equals(CompositionInfo other) =>
            Title.Equals(other.Title) && Type.Equals(other.Type) && Modifiers.Equals(other.Modifiers) &&
            AspectRatio.Equals(other.AspectRatio) && Audio.Equals(other.Audio) && Resolution.Equals(other.Resolution);

        /// <summary>
        /// Returns a more readable short title.
        /// </summary>
        public override string ToString() => $"{Title} {Type.ToString()[4..]} {Modifiers}";
    }
}