using IntronFileController.ViewModels;
using System.IO;
using System.Windows;
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

        private void AddButton_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // Permite apenas arquivos e já sinaliza "Copy"
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                // aceita se houver pelo menos um .txt
                bool ok = paths.Any(p => Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase));
                e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void AddButton_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = ((string[])e.Data.GetData(DataFormats.FileDrop))
                       .Where(p => Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                       .Distinct()
                       .ToArray();

            if (paths.Length == 0) return;

            // Chama seu VM/serviço. Exemplo direto ao VM:
            if (DataContext is HomeViewModel vm)
                await vm.AddFilesFromPathsAsync(paths); // método que você já tem/mostro abaixo
        }
    }
}
