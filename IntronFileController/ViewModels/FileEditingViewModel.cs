using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Common.Extensions;
using IntronFileController.Helpers;
using IntronFileController.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using OxyPlot.Legends;

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
                    // Recalcula a plotagem para toda a faixa (inicial)
                    RebuildPlotForRange(0, Math.Max(0, value.ZMeasurements.Count - 1));

                    // initialize selection texts to full range
                    PlotSelectionMinText = "0";
                    PlotSelectionMaxText = (Math.Max(0, value.ZMeasurements.Count - 1)).ToString(CultureInfo.InvariantCulture);
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
        public LineSeries _lineSeriesX { get; private set; } = new();
        public LineSeries _lineSeriesY { get; private set; } = new();
        public List<DataPoint> PlotPointsZ { get; private set; } = [];
        public List<DataPoint> PlotPointsX { get; private set; } = [];
        public List<DataPoint> PlotPointsY { get; private set; } = [];
        private int _maxSecsPlotPoint = 10;
        private double _zeroOffset = 0;

        // flag to skip reapplying previous vertical axis ranges once (used after Reset)
        private bool _skipReapplyVerticalOnce = false;

        // máximo de pontos que vamos exibir por série (downsampling para a faixa completa)
        private int _maxDisplayPoints = 2000;

        // limite superior de pontos renderizados na tela simultaneamente (para evitar travamento em janelas medianas)
        // ajuste este valor conforme performance/monitor alvo. Com 400k+ de amostras, 1500 costuma ser seguro.
        [ObservableProperty]
        private int maxPointsOnScreen = 1500;
        [ObservableProperty]
        private int minPointsOnScreen = 100;

        // reacted to changes via PropertyChanged subscription in constructor

        [ObservableProperty]
        private double plotSelectionMin = 0;
        partial void OnPlotSelectionMinChanged(double value)
        {
            // update text representation when numeric selection changes (e.g. from graph)
            var text = value.ToString(CultureInfo.InvariantCulture);
            if (PlotSelectionMinText != text)
                PlotSelectionMinText = text;
        }

        [ObservableProperty]
        private double plotSelectionMax = 0;
        partial void OnPlotSelectionMaxChanged(double value)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);
            if (PlotSelectionMaxText != text)
                PlotSelectionMaxText = text;
        }

        // string-backed inputs for UI so we can validate/parse immediately
        [ObservableProperty]
        private string plotSelectionMinText = "0";

        [ObservableProperty]
        private string plotSelectionMaxText = "100";

        private void ApplyPlotSelectionToAxis(double min, double max)
        {
            var xAxis = PlotModel.DefaultXAxis ?? PlotModel.Axes.First(a => a.IsHorizontal());
            if (xAxis != null)
            {
                // avoid NaN
                if (double.IsNaN(min) || double.IsNaN(max)) return;
                // ensure min <= max
                if (min > max) (min, max) = (max, min);
                xAxis.Zoom(min, max);
                PlotModel.InvalidatePlot(false);
                // start debounce, but rebuild already called by callers
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsShowingMarkersText))]
        private bool _isShowingMarkers = false;
        public string IsShowingMarkersText
        {
            get => !IsShowingMarkers ? "Mostrar pontos" : "Esconder pontos";
        }

        // timer para debouncing de eventos de eixo
        private readonly System.Timers.Timer _axisChangeTimer;

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

            // debounce de 150ms para evitar recalcular à exaustão enquanto o usuário move o scroll
            _axisChangeTimer = new System.Timers.Timer(150) { AutoReset = false };
            _axisChangeTimer.Elapsed += (_, __) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    RebuildPlotForCurrentRange();
                });
            };

            SetupPlotModel();

            // Subscribe to property changes for Max/Min points so UI updates immediately
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "MaxPointsOnScreen" || e.PropertyName == "MinPointsOnScreen")
                {
                    // rebuild for current visible range
                    RebuildPlotForCurrentRange();
                }

                if (e.PropertyName == "PlotSelectionMinText")
                {
                    if (double.TryParse(PlotSelectionMinText, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        if (SelectedFile != null)
                        {
                            var total = Math.Max(1, SelectedFile.ZMeasurements.Count);
                            v = Math.Max(0, Math.Min(v, total - 1));
                        }
                        PlotSelectionMin = v;
                        ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
                        RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
                    }
                }
                if (e.PropertyName == "PlotSelectionMaxText")
                {
                    if (double.TryParse(PlotSelectionMaxText, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        if (SelectedFile != null)
                        {
                            var total = Math.Max(1, SelectedFile.ZMeasurements.Count);
                            v = Math.Max(0, Math.Min(v, total - 1));
                        }
                        PlotSelectionMax = v;
                        ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
                        RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
                    }
                }
            };

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

                        // NÃO altera markers automaticamente mais aqui.
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

            // Replace default ResetAt binding with custom action that resets axes and skips vertical reapply
            controller.Bind(
                new OxyMouseDownGesture(OxyMouseButton.Left, clickCount: 2),
                new DelegatePlotCommand<OxyMouseDownEventArgs>((view, ctl, args) =>
                {
                    // If we have a selected file with data, explicitly set X axis to full data range
                    try
                    {
                        double min = 0;
                        double max = 0;
                        if (SelectedFile != null)
                        {
                            var total = Math.Max(0, SelectedFile.ZMeasurements.Count - 1);
                            min = 0;
                            max = Math.Max(0, total);
                        }

                        var model = view.ActualModel;
                        if (model != null)
                        {
                            // Set X axis to full range
                            var xAxis = model.Axes.FirstOrDefault(a => a.IsHorizontal());
                            if (xAxis != null)
                            {
                                xAxis.Zoom(min, max);
                            }

                            // Reset vertical axes to let OxyPlot autoscale them
                            foreach (var yAxis in model.Axes.Where(a => a.IsVertical()))
                            {
                                yAxis.Reset();
                            }

                            // ensure we do not reapply previous vertical ranges during the next rebuild
                            _skipReapplyVerticalOnce = true;

                            // Force rebuild for the full X range (so points are recalculated)
                            RebuildPlotForRange(min, max);

                            // Invalidate to redraw with new axis settings
                            model.InvalidatePlot(true);
                        }
                    }
                    catch
                    {
                        // fallback: call ResetAllAxes
                        view.ActualModel?.ResetAllAxes();
                        _skipReapplyVerticalOnce = true;
                        RebuildPlotForCurrentRange();
                        view.InvalidatePlot(true);
                    }

                    args.Handled = true;
                }));

            var now = DateTime.Now;
            PlotModel.Legends.Add(new OxyPlot.Legends.Legend()
            {
                LegendTitle = "Legenda",
                LegendItemAlignment = OxyPlot.HorizontalAlignment.Left,
                LegendPosition = LegendPosition.BottomCenter,
                LegendPlacement = LegendPlacement.Outside,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendMargin = 4,
                LegendTitleFontSize = 12,
                LegendBackground = OxyColor.FromAColor(30, OxyColors.LimeGreen),
                LegendTitleColor = OxyColors.Gray,
                LegendTextColor = OxyColors.WhiteSmoke,
                IsLegendVisible = true,
            });
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

            // Observa mudanças do eixo X para recalcular pontos visíveis
            var bottomAxis = PlotModel.Axes.First(a => a.IsHorizontal());
            bottomAxis.AxisChanged += (s, e) =>
            {
                // debounce
                _axisChangeTimer.Stop();
                _axisChangeTimer.Start();
            };

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
                TrackerFormatString = "Amostra {2:0}\nValor {4} g",
                CanTrackerInterpolatePoints = false
            };

            _lineSeriesX = new LineSeries
            {
                Title = "Eixo Lateral (g)",
                StrokeThickness = 2,
                Color = OxyColors.Red,
                MarkerType = MarkerType.None,
                MarkerSize = 5,
                MarkerStroke = OxyColors.Red,
                MarkerFill = OxyColors.White,
                MarkerStrokeThickness = 1.2,
                ItemsSource = PlotPointsX,
                TrackerFormatString = "Amostra {2:0}\nValor {4} g",
                CanTrackerInterpolatePoints = false
            };

            _lineSeriesY = new LineSeries
            {
                Title = "Eixo Frontal (g)",
                StrokeThickness = 2,
                Color = OxyColors.DodgerBlue,
                MarkerType = MarkerType.None,
                MarkerSize = 5,
                MarkerStroke = OxyColors.DodgerBlue,
                MarkerFill = OxyColors.White,
                MarkerStrokeThickness = 1.2,
                ItemsSource = PlotPointsY,
                TrackerFormatString = "Amostra {2:0}\nValor {4} g",
                CanTrackerInterpolatePoints = false
            };

            PlotModel.Series.Add(_lineSeries);
            PlotModel.Series.Add(_lineSeriesX);
            PlotModel.Series.Add(_lineSeriesY);
        }

        // Reconstroi as List<DataPoint> (PlotPointsZ/X/Y) para a faixa [minX,maxX]
        private void RebuildPlotForRange(double minX, double maxX)
        {
            if (SelectedFile is null) return;

            int total = SelectedFile.ZMeasurements.Count;
            if (total == 0) return;

            // clamp faixa aos índices de amostra
            int startIdx = Math.Max(0, (int)Math.Floor(minX));
            int endIdx = Math.Min(total - 1, (int)Math.Ceiling(maxX));
            if (endIdx < startIdx) endIdx = startIdx;

            int windowLength = Math.Max(1, endIdx - startIdx + 1);

            // guarda escalas Y atuais para evitar autoscale ao rebuild (Pan/Zoom)
            var verticalAxes = PlotModel.Axes.Where(a => a.IsVertical()).Select(a => (axis: a, min: a.ActualMinimum, max: a.ActualMaximum)).ToList();

            // cálculo adaptativo de quantos pontos mostrar:
            // quanto menor a janela visível (zoom in), maior o número de pontos (até o total)
            double fullRange = Math.Max(1, total - 1);
            double visibleWidth = Math.Max(1.0, maxX - minX);
            // fator: relação entre faixa completa e faixa visível
            double zoomFactor = fullRange / visibleWidth;

            // rawDesired: tenta escalar _maxDisplayPoints pela razão de zoom
            int rawDesired = Math.Max(1, (int)(_maxDisplayPoints * zoomFactor));

            // aplica limites: evita muitos pontos quando zoom ainda está "médio"
            // - força um mínimo razoável
            // - força um teto absoluto para evitar travamentos em janelas médias/grandes
            int desired = Math.Clamp(rawDesired, MinPointsOnScreen, MaxPointsOnScreen);

            // se a janela está muito pequena (windowLength < desired), permitimos mostrar todos os pontos da janela
            desired = Math.Min(desired, windowLength);

            // obtém a sublista de valores e aplica downsample
            var subZ = SelectedFile.ZMeasurements.Skip(startIdx).Take(windowLength).ToList();
            var subX = SelectedFile.XMeasurements.Skip(startIdx).Take(windowLength).ToList();
            var subY = SelectedFile.YMeasurements.Skip(startIdx).Take(windowLength).ToList();

            var decZ = DownsampleMinMax(subZ, desired)
                        .Select(dp => new DataPoint(dp.X + startIdx, dp.Y))
                        .ToList();
            var decX = DownsampleMinMax(subX, desired)
                        .Select(dp => new DataPoint(dp.X + startIdx, dp.Y))
                        .ToList();
            var decY = DownsampleMinMax(subY, desired)
                        .Select(dp => new DataPoint(dp.X + startIdx, dp.Y))
                        .ToList();

            PlotPointsZ.Clear();
            PlotPointsX.Clear();
            PlotPointsY.Clear();

            PlotPointsZ.AddRange(decZ);
            PlotPointsX.AddRange(decX);
            PlotPointsY.AddRange(decY);

            // atualiza o CanExecute do comando de mostrar markers
            ShowHideMarkersCommand.NotifyCanExecuteChanged();

            // reaplica escalas Y anteriores para evitar autoscale durante pan/zoom
            // unless we were asked to skip reapplying once (e.g. after Reset)
            if (!_skipReapplyVerticalOnce)
            {
                foreach (var (axis, min, max) in verticalAxes)
                {
                    if (!double.IsNaN(min) && !double.IsNaN(max) && min < max)
                    {
                        axis.Zoom(min, max);
                    }
                }
            }
            else
            {
                // consume the skip flag so next rebuild behaves normally
                _skipReapplyVerticalOnce = false;
            }

            PlotModel.InvalidatePlot(true);
        }

        // Rebuild usando o eixo X atual
        private void RebuildPlotForCurrentRange()
        {
            if (SelectedFile is null) return;
            var xAxis = PlotModel.DefaultXAxis ?? PlotModel.Axes.First(a => a.IsHorizontal());
            double min = xAxis.ActualMinimum;
            double max = xAxis.ActualMaximum;
            RebuildPlotForRange(min, max);
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

            // também força rebuild imediato da janela selecionada
            RebuildPlotForRange(min, max);
        }
        [RelayCommand(CanExecute = nameof(CanShowHideMarkers))]
        private void ShowHideMarkers()
        {
            if (_lineSeries == null) return;

            if (IsShowingMarkers)
            {

                _lineSeries.MarkerType = MarkerType.None;
                _lineSeriesX.MarkerType = MarkerType.None;
                _lineSeriesY.MarkerType = MarkerType.None;
                IsShowingMarkers = false;
            }
            else
            {
                _lineSeries.MarkerType = MarkerType.Circle;
                _lineSeriesX.MarkerType = MarkerType.Circle;
                _lineSeriesY.MarkerType = MarkerType.Circle;
                IsShowingMarkers = true;
            }
            PlotModel?.InvalidatePlot(true);
        }
        private bool CanShowHideMarkers() => (PlotPointsZ.Count() + PlotPointsX.Count() + PlotPointsY.Count()) > 0;


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

        // Commands to adjust Max/Min points on screen
        [RelayCommand]
        private void IncreaseMaxPoints(int step = 100)
        {
            MaxPointsOnScreen = Math.Min(100_000_000, MaxPointsOnScreen + step);
        }
        [RelayCommand]
        private void DecreaseMaxPoints(int step = 100)
        {
            MaxPointsOnScreen = Math.Max(10, MaxPointsOnScreen - step);
        }

        [RelayCommand]
        private void IncreaseMinPoints(int step = 10)
        {
            MinPointsOnScreen = Math.Min(MaxPointsOnScreen, MinPointsOnScreen + step);
        }
        [RelayCommand]
        private void DecreaseMinPoints(int step = 10)
        {
            MinPointsOnScreen = Math.Max(1, MinPointsOnScreen - step);
        }

        // Commands to nudge plot selection numeric values
        [RelayCommand]
        private void IncreasePlotMin(int step = 1)
        {
            if (SelectedFile == null) return;
            double maxIndex = Math.Max(0, SelectedFile.ZMeasurements.Count - 1);
            PlotSelectionMin = Math.Min(maxIndex, PlotSelectionMin + step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
        }
        [RelayCommand]
        private void DecreasePlotMin(int step = 1)
        {
            PlotSelectionMin = Math.Max(0, PlotSelectionMin - step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
        }

        [RelayCommand]
        private void IncreasePlotMax(int step = 1)
        {
            if (SelectedFile == null) return;
            double maxIndex = Math.Max(0, SelectedFile.ZMeasurements.Count - 1);
            PlotSelectionMax = Math.Min(maxIndex, PlotSelectionMax + step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
        }
        [RelayCommand]
        private void DecreasePlotMax(int step = 1)
        {
            PlotSelectionMax = Math.Max(0, PlotSelectionMax - step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            RebuildPlotForRange(PlotSelectionMin, PlotSelectionMax);
        }
    }
}
