using System;
using System.Windows.Media;

namespace DCP_Ripper {
    public class CompositionInfo {
        public string Title { get; set; }
        public ContentType Type { get; set; }
        public string Modifiers { get; set; }
        public Framing AspectRatio { get; set; }
        public string Language { get; set; }
        public string Territory { get; set; }
        public AudioTrack Audio { get; set; }
        public Resolution Resolution { get; set; }
        public string Studio { get; set; }
        public DateTime Creation { get; set; }
        public string Facility { get; set; }
        public string Standard { get; set; }
        public Version PackageType { get; set; }

        // Enum cache
        static readonly string[] contentTypes = Enum.GetNames(typeof(ContentType));
        static readonly string[] aspects = Enum.GetNames(typeof(Framing));
        static readonly string[] versions = Enum.GetNames(typeof(Version));

        public CompositionInfo(string title) {
            Title = title;
            string[] modifiers = title.Split('_');
            bool wasDate = false;
            for (int i = 1, c = modifiers.Length; i < c; ++i) {
                if (modifiers[i].Length == 8 && int.TryParse(modifiers[i], out int date)) {
                    Creation = new DateTime(date / 10000, date % 10000 / 100, date % 100);
                    wasDate = true;
                } else if (Enum.TryParse('_' + modifiers[i], out Resolution detectedRes)) Resolution = detectedRes;
                else if (modifiers[i].StartsWith("iop", StringComparison.CurrentCultureIgnoreCase) ||
                    modifiers[i].StartsWith("smpte", StringComparison.CurrentCultureIgnoreCase)) Standard = modifiers[i].ToUpper();
                else if (TryParseEnum(modifiers[i], versions, out Version packageType)) PackageType = packageType;
                else if (TryParseEnum(modifiers[i], aspects, out Framing aspectRatio)) AspectRatio = aspectRatio;
                else if (TryParseAudio(modifiers[i], out AudioTrack format)) Audio = format;
                else if (TryParseContentType(modifiers[i], out ContentType type, out string contentMods)) {
                    Type = type;
                    Modifiers = contentMods;
                } else if (IsLanguage(modifiers[i])) {
                    if (string.IsNullOrEmpty(Language)) Language = modifiers[i];
                    else Territory = modifiers[i];
                } else if (wasDate) Facility = modifiers[i];
                else Studio = modifiers[i];
            }
            if (string.IsNullOrEmpty(Language))
                Language = "XX";
            if (string.IsNullOrEmpty(Territory))
                Territory = "XX";
        }

        static bool TryParseEnum<T>(string modifier, string[] modifiers, out T output) where T : struct {
            int splitPos = modifier.IndexOf('-');
            if (splitPos != -1)
                modifier = modifier.Substring(splitPos + 1);
            modifier = '_' + modifier;
            foreach (string mod in modifiers) {
                int secondUnderscore = mod.LastIndexOf('_');
                string compMod = secondUnderscore == 0 ? mod : mod.Substring(0, secondUnderscore);
                if (compMod.Equals(modifier)) {
                    output = (T)Enum.Parse(typeof(T), mod);
                    return true;
                }
            }
            output = (T)(object)0;
            return false;
        }

        static bool TryParseAudio(string modifier, out AudioTrack track) {
            track = AudioTrack.Unknown;
            modifier = modifier.ToLower();
            if (modifier.StartsWith("20")) track = AudioTrack.Stereo;
            else if (modifier.StartsWith("51")) track = AudioTrack.Surround__5_1;
            else if (modifier.StartsWith("71")) track = AudioTrack.Surround__7_1;
            else if (modifier.StartsWith("sdds", StringComparison.CurrentCultureIgnoreCase)) track = AudioTrack.SDDS;
            else if (modifier.StartsWith("cavern"))
                track = modifier.Contains("xl") ? AudioTrack.CavernXL : AudioTrack.Cavern;
            // These formats might upgrade a present standard layout, like "71-Atmos"
            if (modifier.Contains("atmos")) track = AudioTrack.Atmos;
            if (modifier.Contains("auro")) track = AudioTrack.Auro;
            if (modifier.Contains("auromax")) track = AudioTrack.AuroMax;
            if (modifier.Contains("dtsx")) track = AudioTrack.DTS___X;
            return track != AudioTrack.Unknown;
        }

        static bool TryParseContentType(string modifier, out ContentType contentType, out string modifiers) {
            modifiers = string.Empty;
            if (modifier.Length >= 3) {
                string stamp = modifier.Substring(0, 3);
                foreach (string type in contentTypes) {
                    if (type.StartsWith(stamp)) {
                        contentType = (ContentType)Enum.Parse(typeof(ContentType), type);
                        string[] contentMods = modifier.Substring(3).Split('-');
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

        static bool IsLanguage(string modifier) {
            int firstSplit = modifier.IndexOf('-');
            if (firstSplit == -1) return false;
            int secondSplit = modifier.IndexOf('-', firstSplit + 1);
            return firstSplit <= 3 && (secondSplit != -1 ? secondSplit : modifier.Length) - firstSplit <= 4;
        }

        public Brush GetBrush() {
            string contentType = Type.ToString();
            int mul = 255 / ('Z' - 'A');
            Color tint = Color.FromArgb(63,
                (byte)((contentType[0] - 'A') * mul),
                (byte)((contentType[1] - 'A') * mul),
                (byte)((contentType[2] - 'A') * mul));
            return new SolidColorBrush(tint);
        }
    }
}
