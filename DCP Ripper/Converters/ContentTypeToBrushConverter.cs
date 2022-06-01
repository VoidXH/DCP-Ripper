using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DCP_Ripper.Converters {
    /// <summary>
    /// Determines a data grid row background color by content type.
    /// </summary>
    public class ContentTypeToBrushConverter : IValueConverter {
        static readonly Dictionary<ContentType, Brush> typeBackgrounds = new();

        /// <summary>
        /// Returns a data grid row background color by content type.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not ContentType)
                return null;
            ContentType type = (ContentType)value;
            if (typeBackgrounds.ContainsKey(type))
                return typeBackgrounds[type];
            string contentType = type.ToString();
            int mul = 255 / ('Z' - 'A');
            Color tint = Color.FromArgb(63,
                (byte)((contentType[0] - 'A') * mul),
                (byte)((contentType[1] - 'A') * mul),
                (byte)((contentType[2] - 'A') * mul));
            return typeBackgrounds[type] = new SolidColorBrush(tint);
        }

        /// <summary>
        /// Determines a content type by data grid row background color.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}