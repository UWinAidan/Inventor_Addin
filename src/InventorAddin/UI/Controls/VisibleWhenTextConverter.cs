using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// Visible when the bound text is not blank, else collapsed, so a message line that has nothing to say takes no room
    /// (AWB Hole's problems and notes, task 090). A list counts as text when it has any entry.
    /// </summary>
    public sealed class VisibleWhenTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool shown = value switch
            {
                null => false,
                string text => !string.IsNullOrWhiteSpace(text),
                System.Collections.ICollection list => list.Count > 0,
                _ => true,
            };
            return shown ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
