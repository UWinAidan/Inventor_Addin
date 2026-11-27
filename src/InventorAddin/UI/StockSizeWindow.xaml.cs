using System;
using System.Collections.Generic;
using System.Windows;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// The Stock Size window (spec 05, "Added for M5", item 5; task 206). All behaviour is in <see cref="StockSizeViewModel"/>; this fills the
    /// shape list and closes on OK. <see cref="Ask"/> shows it over Inventor and returns true on OK.
    /// </summary>
    public partial class StockSizeWindow : Window
    {
        /// <summary>One entry of the shape dropdown: the words shown, and the <see cref="StockShape"/> they stand for.</summary>
        public sealed class Choice
        {
            public Choice(string label, StockShape value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public StockShape Value { get; }
        }

        private readonly StockSizeViewModel _viewModel;

        public StockSizeWindow(StockSizeViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = "Stock Size";
            ShapeBox.ItemsSource = new List<Choice>
            {
                new Choice("Plate", StockShape.Plate),
                new Choice("Round bar", StockShape.Round),
            };
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        // Focus the text so it can be edited straight away.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            SizeBox.Focus();
            SizeBox.SelectAll();
        }

        /// <summary>Shows the window and returns true when the user pressed OK.</summary>
        public static bool? Ask(StockSizeViewModel viewModel)
        {
            var window = new StockSizeWindow(viewModel);
            return WindowHost.ShowDialog(window, IconNames.StockSize, stateKey: "StockSize");
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CanWrite)
                DialogResult = true;
        }
    }
}
