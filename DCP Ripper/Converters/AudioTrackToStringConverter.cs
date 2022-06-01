using System;
using System.Globalization;
using System.Windows.Data;

namespace DCP_Ripper.Converters {
    /// <summary>
    /// Converts between <see cref="AudioTrack"/> and string.
    /// </summary>
    class AudioTrackToStringConverter : IValueConverter {
        static readonly string[] tracks = {
            "N/A", "Stereo", "5.1 Surround", "7.1 Surround", "SDDS", "Dolby Atmos",
            "Barco Auro", "Barco AuroMax", "DTS:X", "Cavern", "Cavern XL",
            "IMAX 5-track", "IMAX 6-track", "IMAX 12-track"
        };

        /// <summary>
        /// Converts a <see cref="AudioTrack"/> to string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is AudioTrack ? tracks[(int)value] : null;

        /// <summary>
        /// Converts a string to <see cref="AudioTrack"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}