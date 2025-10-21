using IntronFileController.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace IntronFileController.Views
{
    /// <summary>
    /// Interação lógica para HomeView.xam
    /// </summary>
    public partial class HomeView : UserControl
    {
        private readonly HomeViewModel VM;
        public event EventHandler NavigateInvoked;
        public HomeView(HomeViewModel viewModel)
        {
            InitializeComponent();

            VM = viewModel;
            DataContext = viewModel;

            VM.NavigateInvoked += (sender, args) => NavigateInvoked?.Invoke(sender, args);
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VM.AddFileButtonCommand.Execute(this);
        }
    }
}
