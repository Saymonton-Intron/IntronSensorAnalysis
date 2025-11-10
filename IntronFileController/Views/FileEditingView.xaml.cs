using IntronFileController.ViewModels;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.SkiaSharp.Wpf;
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
        public FileEditingViewModel VM { get; private set; }
        public event EventHandler NavigateInvoked;
        public FileEditingView(FileEditingViewModel viewModel)
        {
            InitializeComponent();

            VM = viewModel;
            DataContext = viewModel;

            // Attach the PlotModel to the PlotView at runtime to avoid model reuse errors in designer
            try
            {
                PlotViewControl.Model = viewModel.PlotModel;
            }
            catch
            {
                // ignore
            }

            // ensure we detach the model when the view is unloaded so other views can reuse it safely
            this.Unloaded += (s, e) =>
            {
                try { PlotViewControl.Model = null; } catch { }
            };

            this.Loaded += (s, e) =>
            {
                try { PlotViewControl.Model = viewModel.PlotModel; } catch { }
            };

            VM.NavigateInvoked += (sender, args) => NavigateInvoked?.Invoke(sender, args);
        }
    }
}
