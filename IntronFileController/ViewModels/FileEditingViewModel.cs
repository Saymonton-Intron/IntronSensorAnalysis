using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Common;
using IntronFileController.Common.Extensions;
using IntronFileController.Helpers;
using IntronFileController.Models;
using IntronFileController.Services;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using static System.Net.Mime.MediaTypeNames;

namespace IntronFileController.ViewModels
{
    public partial class FileEditingViewModel : ObservableObject
    {
        public event EventHandler NavigateInvoked;

        [ObservableProperty]
        private ObservableCollection<ImportedFileViewModel> importedFiles = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(ProcessAllCommand))]
        [NotifyPropertyChangedFor(nameof(LabelsVisibility))]
        [NotifyPropertyChangedFor(nameof(FirstLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(LastLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(TextInitLabelVisibility))]
        [NotifyPropertyChangedFor(nameof(TextEndLabelVisibility))]
        private ImportedFileViewModel selectedFile;
        partial void OnSelectedFileChanged(ImportedFileViewModel value)
        {
            if (value == null) return;
            if (value.ZMeasurements.Count == value.YMeasurements.Count && value.YMeasurements.Count == value.XMeasurements.Count)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Limpa coleções antigas
                    PlotPointsZ.Clear();
                    PlotPointsX.Clear();
                    PlotPointsY.Clear();

                    // Downsample antes de popular a lista que alimenta a série
                    var decZ = DownsampleMinMax(value.ZMeasurements, _maxDisplayPoints);
                    var decX = DownsampleMinMax(value.XMeasurements, _maxDisplayPoints);
                    var decY = DownsampleMinMax(value.YMeasurements, _maxDisplayPoints);

                    PlotPointsZ.AddRange(decZ);
                    PlotPointsX.AddRange(decX);
                    PlotPointsY.AddRange(decY);

                    PlotModel.InvalidatePlot(true);
                });
            }
        }

        [ObservableProperty] private int previewLinesCount = 20;

        private readonly IFileHandlerHelper fileHandlerHelper;
        private readonly IFileImportService fileImportService;
        private readonly IFileExportService fileExportService;
        private readonly IOxyThemeHelper oxyThemeHelper;
        private readonly IThemeService themeService;

        [ObservableProperty] private Visibility addCardVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility restOfContentVisibility = Visibility.Hidden;
        public Visibility TextInitLabelVisibility => SelectedFile?.TopContextCount >= SelectedFile?.TopCutLine ? Visibility.Visible : Visibility.Hidden;
        public Visibility TextEndLabelVisibility => SelectedFile?.BottomContextCount >= SelectedFile?.Model.Preview.Lines().Length - SelectedFile?.BottomCutLine ? Visibility.Visible : Visibility.Hidden;
        public Visibility LabelsVisibility => SelectedFile != null ? Visibility.Visible : Visibility.Collapsed;

        private string firstLinesTextBox = "0";
        public string FirstLinesTextBox
        {
            get
            {
                if (SelectedFile is null) return firstLinesTextBox;
                return SelectedFile.TopCutLine.ToString();
            }
            set
            {
                if (!int.TryParse(value, out var cut) || cut < 0) return; // aceita 0+; 0 = não corta nada no topo

                if (SelectedFile is not null)
                {
                    SelectedFile.TopCutLine = cut; // 1-based

                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }

            }
        }


        private string lastLinesTextBox = "0";
        public string LastLinesTextBox
        {
            get
            {
                if (SelectedFile is null) return lastLinesTextBox;

                var total = SelectedFile.Model.Preview.Lines().Length;
                // offset a partir do fim (o que aparece no TextBox)
                // regra: bottomCutLine = total - offset
                var offset = total - SelectedFile.BottomCutLine;
                return offset.ToString();
            }
            set
            {
                if (!int.TryParse(value, out var offset) || offset < 0)
                    return;

                if (SelectedFile is not null)
                {
                    var total = SelectedFile.Model.Preview.Lines().Length;

                    // Corte no rodapé = (última linha) - offset  (1-based)
                    // Ex.: total=200, offset=3  -> corte = 197
                    // clamp para não sair do arquivo
                    int bottomCutLine = Math.Max(1, total - offset);

                    SelectedFile.BottomCutLine = bottomCutLine;
                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                    // NÃO mexe no BottomContextCount: ele já dita quantas linhas mostrar.
                }
            }
        }
        //private string topContextTextBox = "5";
        public string TopContextTextBox
        {
            get => SelectedFile.TopContextCount.ToString();
            set
            {
                if (!int.TryParse(value, out var ctx) || ctx < 0) return;

                if (SelectedFile is not null)
                    SelectedFile.TopContextCount = ctx;
                OnPropertyChanged(nameof(TopContextTextBox));

                OnPropertyChanged(nameof(TextInitLabelVisibility));
                OnPropertyChanged(nameof(TextEndLabelVisibility));
                
            }
        }

        private string bottomContextTextBox = "5";
        public string BottomContextTextBox
        {
            get => bottomContextTextBox;
            set
            {
                if (!int.TryParse(value, out var ctx) || ctx < 0) return;
                if (SetProperty(ref bottomContextTextBox, value))
                {
                    if (SelectedFile is not null)
                        SelectedFile.BottomContextCount = ctx;

                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }
            }
        }


        public PlotModel PlotModel { get; private set; } = new() { Title = "Acelerômetro triaxial" };
        public PlotController PlotController { get; private set; } = new();
        public LineSeries _lineSeries { get; private set; } = new();
        public List<DataPoint> PlotPointsZ { get; private set; } = [];
        public List<DataPoint> PlotPointsX { get; private set; } = [];
        public List<DataPoint> PlotPointsY { get; private set; } = [];
        private int _maxSecsPlotPoint = 10;
        private double _zeroOffset = 0;

        // máximo de pontos que vamos exibir por série (downsampling)
        private int _maxDisplayPoints = 2000;

        [ObservableProperty] private double plotSelectionMin = 0;
        [ObservableProperty] private double plotSelectionMax = 0;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsShowingMarkersText))]
        private bool _isShowingMarkers = false;
        public string IsShowingMarkersText
        {
            get => !IsShowingMarkers ? "Mostrar pontos" : "Esconder pontos";
        }
        public FileEditingViewModel(IFileHandlerHelper _fileHandlerHelper, IFileImportService _fileImportService, IFileExportService _fileExportService, IOxyThemeHelper _oxyThemeHelper, IThemeService _themeService)
        {
            fileHandlerHelper = _fileHandlerHelper;
            fileImportService = _fileImportService;
            fileExportService = _fileExportService;
            oxyThemeHelper = _oxyThemeHelper;
            themeService = _themeService;
            ImportedFiles = fileHandlerHelper.ImportedFiles;
            SelectedFile = ImportedFiles?.FirstOrDefault()!;

            ImportedFiles!.CollectionChanged += (a, b) =>
            {
                RemoveSelectedCommand.NotifyCanExecuteChanged();
            };

            SetupPlotModel();
            oxyThemeHelper.Apply(PlotModel, themeService.Current);
            themeService.ThemeChanged += (sender, baseTheme) => oxyThemeHelper.Apply(PlotModel, baseTheme);
        }

        private void SetupPlotModel()
        {
            PlotController.UnbindAll();
            PlotController.BindMouseEnter(PlotCommands.HoverSnapTrack);

            var controller = PlotController;

            // --- Scroll normal = Pan horizontal (X) ---
            controller.BindMouseWheel(
                OxyModifierKeys.None,
                new DelegatePlotCommand<OxyMouseWheelEventArgs>(
                    (view, ctl, args) =>
                    {
                        double delta = args.Delta > 0 ? -1 : 1; // up = direita, down = esquerda
                        const double step = 50;
                        foreach (var axis in view.ActualModel.Axes.Where(a => a.Position == AxisPosition.Bottom))
                        {
                            axis.Pan(delta * step);
                        }
                        view.InvalidatePlot(false);
                        args.Handled = true;
                    }));

            // --- Ctrl + Scroll = Zoom no eixo X ---
            controller.BindMouseWheel(
                OxyModifierKeys.Control,
                new DelegatePlotCommand<OxyMouseWheelEventArgs>(
                    (view, ctl, args) =>
                    {
                        double zoomFactor = args.Delta > 0 ? 1.2 : 0.8;
                        foreach (var axis in view.ActualModel.Axes.Where(a => a.Position == AxisPosition.Bottom))
                        {
                            double x = axis.InverseTransform(args.Position.X);
                            axis.ZoomAt(zoomFactor, x);
                        }
                        if (zoomFactor >= 1)
                        {
                            _lineSeries.MarkerType = MarkerType.Circle;
                            IsShowingMarkers = true;
                            ShowHideMarkersCommand.NotifyCanExecuteChanged();
                        }
                        else
                        {
                            _lineSeries.MarkerType = MarkerType.None;
                            IsShowingMarkers = false;
                            ShowHideMarkersCommand.NotifyCanExecuteChanged();
                        }

                        view.InvalidatePlot(false);
                        args.Handled = true;
                    }));

            // --- Shift + Scroll = Pan vertical (Y) ---
            controller.BindMouseWheel(
                OxyModifierKeys.Shift,
                new DelegatePlotCommand<OxyMouseWheelEventArgs>(
                    (view, ctl, args) =>
                    {
                        double delta = args.Delta > 0 ? 1 : -1; // up = cima, down = baixo
                        const double step = 50;
                        foreach (var axis in view.ActualModel.Axes.Where(a => a.Position == AxisPosition.Left))
                        {
                            axis.Pan(delta * step);
                        }
                        view.InvalidatePlot(false);
                        args.Handled = true;
                    }));

            // 🖱 Botão do meio = Pan livre (X e Y ao mesmo tempo)
            controller.BindMouseDown(OxyMouseButton.Middle, PlotCommands.PanAt);

            //// Clique esquerdo/direito → resetar todos os eixos
            //var resetCommand = new DelegatePlotCommand<OxyMouseDownEventArgs>(
            //    (view, ctl, args) =>
            //    {
            //        view.ActualModel.ResetAllAxes();
            //        view.InvalidatePlot(false);
            //        args.Handled = true;
            //    });

            controller.Bind(
                new OxyMouseDownGesture(OxyMouseButton.Left, clickCount: 2),
                PlotCommands.ResetAt);

            //controller.BindMouseDown(OxyMouseButton.Left, resetCommand);
            //controller.BindMouseDown(OxyMouseButton.Right, resetCommand);
            //controller.BindMouseDown(OxyMouseButton.Middle, resetCommand);

            var now = DateTime.Now;

            //PlotModel.Axes.Add(new DateTimeAxis
            //{
            //    Position = AxisPosition.Bottom,
            //    Title = "Amostras",
            //    //StringFormat = "HH:mm:ss.fff",
            //    MajorGridlineStyle = LineStyle.Solid,
            //    MinorGridlineStyle = LineStyle.Dot,
            //    Minimum = 0,
            //    Maximum = DateTimeAxis.ToDouble(now.AddSeconds(_maxSecsPlotPoint))
            //    //Minimum = DateTimeAxis.ToDouble(now),
            //    //Maximum = DateTimeAxis.ToDouble(now.AddSeconds(_maxSecsPlotPoint))
            //});

            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Amostras",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Gravidade (g)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            _lineSeries = new LineSeries
            {
                Title = "Eixo Vertical (g)",
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
                MarkerSize = 5,
                MarkerStroke = OxyColors.Green,
                MarkerFill = OxyColors.White,
                MarkerStrokeThickness = 1.2,
                ItemsSource = PlotPointsZ,
                TrackerFormatString = "Amostra {0}\nValor {4:000.0}g",
                CanTrackerInterpolatePoints = false
            };

            PlotModel.Series.Add(_lineSeries);
        }

        // Downsampling simples: para cada bucket calcula min e max (preserva envelope)
        private List<DataPoint> DownsampleMinMax(IList<double> values, int maxPoints)
        {
            int n = values?.Count ?? 0;
            var result = new List<DataPoint>();
            if (n == 0) return result;
            if (n <= maxPoints)
            {
                result.Capacity = n;
                for (int i = 0; i < n; i++)
                    result.Add(new DataPoint(i, values[i]));
                return result;
            }

            // usa min/max por bucket; estimativa de buckets para ficar perto de maxPoints
            int targetBuckets = Math.Max(1, maxPoints / 2);
            double bucketSize = (double)n / targetBuckets;

            // inclui primeiro ponto
            result.Add(new DataPoint(0, values[0]));

            for (int b = 0; b < targetBuckets; b++)
            {
                int start = (int)Math.Floor(b * bucketSize);
                int end = (int)Math.Floor((b + 1) * bucketSize);
                if (b == targetBuckets - 1) end = n;
                start = Math.Max(0, Math.Min(start, n - 1));
                end = Math.Max(start + 1, Math.Min(end, n));

                double minVal = double.MaxValue; int minIdx = start;
                double maxVal = double.MinValue; int maxIdx = start;

                for (int i = start; i < end; i++)
                {
                    var v = values[i];
                    if (v < minVal) { minVal = v; minIdx = i; }
                    if (v > maxVal) { maxVal = v; maxIdx = i; }
                }

                if (minIdx == maxIdx)
                {
                    result.Add(new DataPoint(minIdx, minVal));
                }
                else if (minIdx < maxIdx)
                {
                    result.Add(new DataPoint(minIdx, minVal));
                    result.Add(new DataPoint(maxIdx, maxVal));
                }
                else
                {
                    result.Add(new DataPoint(maxIdx, maxVal));
                    result.Add(new DataPoint(minIdx, minVal));
                }
            }

            // inclui último ponto
            result.Add(new DataPoint(n - 1, values[n - 1]));

            // ordena por X para garantir sequência
            result = result.OrderBy(p => p.X).ToList();
            return result;
        }

        [RelayCommand]
        private void RangeSelected((double Min, double Max) range)
        {
            // Exemplo: só loga / aplica zoom / filtra / abre diálogo...
            var (min, max) = range;
            // Aplicar zoom no eixo X:
            var xAxis = PlotModel.DefaultXAxis ?? PlotModel.Axes.First(a => a.IsHorizontal());
            xAxis.Zoom(min, max);
            PlotModel.InvalidatePlot(false);
        }
        [RelayCommand(CanExecute = nameof(CanShowHideMarkers))]
        private void ShowHideMarkers()
        {
            if (_lineSeries == null) return;

            if (IsShowingMarkers)
            {

                _lineSeries.MarkerType = MarkerType.None;
                IsShowingMarkers = false;
            }
            else
            {
                _lineSeries.MarkerType = MarkerType.Circle;
                IsShowingMarkers = true;
            }
            PlotModel?.InvalidatePlot(true);
        }
        private bool CanShowHideMarkers() => PlotPointsZ.Count() > 0;


        [RelayCommand]
        private void NavigateBack()
        {
            ImportedFiles.Clear();
            //fileHandlerHelper.ImportedFiles.Clear();
            NavigateInvoked?.Invoke(this, new());
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            // Abre dialogo para selecionar os arquivos para importar
            var textPaths = fileImportService.OpenDialogSelectTextFiles();

            // chama serviço para importar (faz leitura assíncrona)
            var imported = await fileImportService.ImportTextFilesAsync(textPaths);

            // Adiciona os arquivos no fileHandler
            fileHandlerHelper.AddFiles(imported);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRemoveSelectedCommand))]
        private void RemoveSelected()
        {
            ImportedFiles.Remove(SelectedFile);
            SelectedFile = ImportedFiles!.FirstOrDefault()!;

        }
        private bool CanExecuteRemoveSelectedCommand() => SelectedFile != null && ImportedFiles.Count > 0;

        [RelayCommand(CanExecute = nameof(CanExecuteProcessAllCommand))]
        private async Task ProcessAll()
        {
            var readOnlyList = new ReadOnlyObservableCollection<ImportedFileViewModel>(ImportedFiles);
            await fileExportService.ExportAsync(readOnlyList, owner: System.Windows.Application.Current.MainWindow);
        }
        private bool CanExecuteProcessAllCommand() => SelectedFile != null && ImportedFiles.Count > 0; // Alterar depois

        [RelayCommand]
        private void ApplyToAll()
        {
            if (ImportedFiles.Count <= 1 || SelectedFile is null)
                return;

            // 1) Descobre N = quantas últimas linhas o SelectedFile está mantendo
            int selTotal = SelectedFile.Model.Preview.Lines().Length;

            // Se BottomCutLine é 1-based e inclusivo (ex.: 1 = primeira linha), então:
            // N_keep = total - BottomCutLine + 1
            int nKeepFromEnd = Math.Max(0, selTotal - SelectedFile.BottomCutLine + 1);

            foreach (var file in ImportedFiles)
            {
                if (ReferenceEquals(file, SelectedFile)) continue;

                // copia o TopCutLine "como está" (se você também quiser preservar “quantas primeiras linhas” ao invés do índice,
                // faça um cálculo similar ao do bottom)
                file.TopCutLine = SelectedFile.TopCutLine;

                // 2) Recalcula o índice de corte para ESTE arquivo,
                //     de modo que ele também mantenha as mesmas N últimas linhas
                int total = file.Model.Preview.Lines().Length;

                // índice 1-based do começo do “rabo” (onde começam as últimas N)
                int cutIndexFromTop = Math.Max(1, total - nKeepFromEnd + 1);

                file.BottomCutLine = cutIndexFromTop; // <<< agora atribui no alvo
            }
        }
    }
}
