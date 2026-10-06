using System;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>About dialog. Shows the values of an <see cref="AboutViewModel"/>; the Close button is wired in XAML.</summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(AboutViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
