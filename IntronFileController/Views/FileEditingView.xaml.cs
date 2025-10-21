using IntronFileController.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IntronFileController.Views
{
    /// <summary>
    /// Interação lógica para FileEditingView.xam
    /// </summary>
    public partial class FileEditingView : UserControl
    {
        private readonly FileEditingViewModel VM;
        public event EventHandler NavigateInvoked;
        public FileEditingView(FileEditingViewModel viewModel)
        {
            InitializeComponent();

            VM = viewModel;
            DataContext = viewModel;

            VM.NavigateInvoked += (sender, args) => NavigateInvoked?.Invoke(sender, args);
        }
    }
}
