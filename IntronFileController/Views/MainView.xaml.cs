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
using System.Windows.Shapes;

namespace IntronFileController.Views
{
    /// <summary>
    /// Lógica interna para MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private readonly MainViewModel VM;
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();

            VM = viewModel;
            DataContext = viewModel;
        }

        private void PackIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VM.ChangeThemeCommand.Execute(this);
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VM.AddFileButtonCommand.Execute(this);
        }
    }
}
