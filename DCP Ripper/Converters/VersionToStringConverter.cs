using System;
using System.Globalization;
using System.Windows.Data;

namespace DCP_Ripper.Converters {
    /// <summary>
    /// Converts between <see cref="Version"/> and string.
    /// </summary>
    class VersionToStringConverter : IValueConverter {
        static readonly string[] versions = { "Original version", "Version file" };

        /// <summary>
        /// Converts a <see cref="Version"/> to string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Version ? versions[(int)value] : null;

        /// <summary>
        /// Converts a string to <see cref="Version"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}