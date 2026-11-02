using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// A small square of a colour that is data, not part of the look: the fit colour of a dowel hole, from the hole
    /// workbook (AWB Hole, task 090). <see cref="Colour"/> is <c>#RRGGBB</c>; anything else, or null, draws no fill.
    /// Its size, border and gap are the implicit <c>ColourSwatch</c> style in <c>UI/Theme/Styles.xaml</c>; windows set
    /// no colour of their own, which is why the fill is set here and not in XAML.
    /// </summary>
    public sealed class ColourSwatch : Border
    {
        public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(
            nameof(Colour), typeof(string), typeof(ColourSwatch),
            new FrameworkPropertyMetadata(null, (d, _) => ((ColourSwatch)d).ApplyColour()));

        static ColourSwatch()
        {
            FocusableProperty.OverrideMetadata(typeof(ColourSwatch), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The colour as <c>#RRGGBB</c>, or null for none.</summary>
        public string? Colour
        {
            get => (string?)GetValue(ColourProperty);
            set => SetValue(ColourProperty, value);
        }

        private void ApplyColour()
        {
            Brush? fill = null;
            string text = (Colour ?? string.Empty).Trim();
            if (text.Length == 7 && text[0] == '#')
            {
                try
                {
                    var colour = (Color)ColorConverter.ConvertFromString(text);
                    var brush = new SolidColorBrush(colour);
                    brush.Freeze();
                    fill = brush;
                }
                catch (FormatException)
                {
                    // Not a colour: no fill.
                }
            }

            Background = fill;
        }
    }
}
