using System;
using System.Globalization;
using System.Windows.Data;

namespace DCP_Ripper.Converters {
    /// <summary>
    /// Converts between <see cref="Framing"/> and string.
    /// </summary>
    class FramingToStringConverter : IValueConverter {
        static readonly string[] framings = {
            "N/A", "1.19:1", "1.33:1", "1.37:1 (Academy)", "1.66:1", "1.78:1", "1.85:1 (Flat)",
            "2.35 (Scope)", "2.39 (Scope)", "Flat", "Scope", "Full container"
        };

        /// <summary>
        /// Converts a <see cref="Framing"/> to string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Framing ? framings[(int)value] : null;

        /// <summary>
        /// Converts a string to <see cref="Framing"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}