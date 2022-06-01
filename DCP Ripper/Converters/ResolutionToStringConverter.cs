using System;
using System.Globalization;
using System.Windows.Data;

namespace DCP_Ripper.Converters {
    /// <summary>
    /// Converts between <see cref="Resolution"/> and string.
    /// </summary>
    class ResolutionToStringConverter : IValueConverter {
        static readonly string[] resolutions = { "N/A", "2K", "4K" };

        /// <summary>
        /// Converts a <see cref="Resolution"/> to string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Resolution ? resolutions[(int)value] : null;

        /// <summary>
        /// Converts a string to <see cref="Resolution"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}