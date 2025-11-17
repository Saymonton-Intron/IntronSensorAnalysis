using System.Windows;

namespace IntronFileController.Views
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public void UpdateMessage(string text)
        {
            MessageText.Text = text;
        }
    }
}