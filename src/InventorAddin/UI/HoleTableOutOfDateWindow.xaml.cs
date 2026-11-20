using System;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// The prompt the automatic hole table check shows (task 176). All behaviour is in
    /// <see cref="HoleTableOutOfDatePromptViewModel"/>; this closes the window when the view-model asks.
    /// </summary>
    public partial class HoleTableOutOfDateWindow : Window
    {
        public const string TitleText = "Hole Table";

        private bool _closing;

        public HoleTableOutOfDateWindow(HoleTableOutOfDatePromptViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            Title = TitleText;
            DataContext = viewModel;
            Closing += (_, _) => _closing = true;
            viewModel.CloseRequested += _ =>
            {
                if (!_closing)
                    Close();
            };
        }
    }
}
