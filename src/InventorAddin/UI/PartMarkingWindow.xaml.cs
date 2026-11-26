using System;
using System.Collections.Generic;
using System.Windows;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// The Part Marking window (spec 05, "Added for M5", item 2; task 199). All behaviour is in <see cref="PartMarkingViewModel"/>; this
    /// fills the two lists and closes on OK. <see cref="Ask"/> shows it over Inventor and returns the options, or null on Cancel.
    /// </summary>
    public partial class PartMarkingWindow : Window
    {
        /// <summary>One entry of a dropdown: the words shown, and the enum value they stand for.</summary>
        public sealed class Choice
        {
            public Choice(string label, object value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public object Value { get; }
        }

        private readonly PartMarkingViewModel _viewModel;

        public PartMarkingWindow(PartMarkingViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = "Part Marking";

            var methods = new List<Choice>();
            foreach (PartMarkingMethod method in Enum.GetValues(typeof(PartMarkingMethod)))
                methods.Add(new Choice(PartMarking.MethodName(method), method));
            MethodBox.ItemsSource = methods;
            TargetBox.ItemsSource = new[]
            {
                new Choice("Leader to an edge", PartMarkingTarget.Leader),
                new Choice("Marking zone on a view", PartMarkingTarget.Zone),
            };

            DataContext = _viewModel;
        }

        /// <summary>Shows the window and returns the options the user confirmed, or null when they cancelled or closed it.</summary>
        public static PartMarkingOptions? Ask(PartMarkingViewModel viewModel)
        {
            var window = new PartMarkingWindow(viewModel);
            bool? result = WindowHost.ShowDialog(window, IconNames.PartMarking, stateKey: "PartMarking");
            return result == true ? viewModel.Options : null;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CanApply)
                DialogResult = true;
        }
    }
}
