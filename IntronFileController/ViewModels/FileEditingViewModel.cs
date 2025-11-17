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
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System;

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
        [NotifyCanExecuteChangedFor(nameof(ExportSelectionCommand))]
        [NotifyPropertyChangedFor(nameof(LabelsVisibility))]
        [NotifyPropertyChangedFor(nameof(FirstLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(LastLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(TextInitLabelVisibility))]
        [NotifyPropertyChangedFor(nameof(TextEndLabelVisibility))]
        private ImportedFileViewModel selectedFile;

        // keep reference to previously subscribed selected file to unsubscribe
        private ImportedFileViewModel? _previousSelectedFileForSubscription;

        // suppression flags to avoid feedback loops between plot <-> file updates
        private bool _suppressPlotToFile = false;
        private bool _suppressFileToPlot = false;

        partial void OnSelectedFileChanged(ImportedFileViewModel value)
        {
            // unsubscribe previous
            if (_previousSelectedFileForSubscription is not null)
            {
                _previousSelectedFileForSubscription.PropertyChanged -= SelectedFile_PropertyChanged;
                _previousSelectedFileForSubscription = null;
            }

            if (value == null) return;

            // subscribe new
            value.PropertyChanged += SelectedFile_PropertyChanged;
            _previousSelectedFileForSubscription = value;

            if (value.ZMeasurements.Count == value.YMeasurements.Count && value.YMeasurements.Count == value.XMeasurements.Count)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Recalcula a plotagem para toda a faixa (inicial)
                    // For time axis, compute overall visible range from earliest to latest among selected file
                    RebuildPlotForRange(PlotModel.DefaultXAxis?.ActualMinimum ?? 0, PlotModel.DefaultXAxis?.ActualMaximum ?? 0);

                    // initialize selection texts to full range (keep numeric indices for now)
                    PlotSelectionMinText = "0";
                    PlotSelectionMaxText = (Math.Max(0, value.ZMeasurements.Count - 1)).ToString(CultureInfo.InvariantCulture);

                    // initialize rectangle selection to match current file cuts
                    try
                    {
                        var min = value.TopCutLine;
                        var max = Math.Max(0, value.BottomCutLine - 2);

                        _suppressPlotToFile = true;
                        ApplyPlotSelectionToAxis(min, max);
                        _suppressPlotToFile = false;
                    }
                    catch
                    {
                        // ignore
                        _suppressPlotToFile = false;
                    }
                });
            }
        }

        private void SelectedFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ImportedFileViewModel vm) return;

            if (_suppressFileToPlot) return; // avoid reacting to changes we initiated from plot

            // react only to top/bottom cut changes: map to selection but selection now is timestamp based
            if (e.PropertyName == nameof(ImportedFileViewModel.TopCutLine) || e.PropertyName == nameof(ImportedFileViewModel.BottomCutLine))
            {
                try
                {
                    // map top/bottom sample indices to timestamps using selected file
                    var topIdx = vm.TopCutLine;
                    var bottomIdx = Math.Max(0, vm.BottomCutLine - 2);

                    if (vm.Timestamps.Count > 0)
                    {
                        double min = DateTimeAxis.ToDouble(vm.Timestamps[Math.Max(0, Math.Min(vm.Timestamps.Count - 1, topIdx))]);
                        double max = DateTimeAxis.ToDouble(vm.Timestamps[Math.Max(0, Math.Min(vm.Timestamps.Count - 1, bottomIdx))]);

                        _suppressPlotToFile = true;
                        ApplyPlotSelectionToAxis(min, max);
                        _suppressPlotToFile = false;
                    }
                }
                catch
                {
                    _suppressPlotToFile = false;
                }
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

                    // also update plot selection rectangle (map using timestamps)
                    try
                    {
                        if (SelectedFile.Timestamps.Count > 0)
                        {
                            var min = DateTimeAxis.ToDouble(SelectedFile.Timestamps[Math.Max(0, Math.Min(SelectedFile.Timestamps.Count - 1, SelectedFile.TopCutLine))]);
                            var max = DateTimeAxis.ToDouble(SelectedFile.Timestamps[Math.Max(0, Math.Min(SelectedFile.Timestamps.Count - 1, SelectedFile.BottomCutLine - 2))]);

                            _suppressPlotToFile = true;
                            ApplyPlotSelectionToAxis(min, max);
                            _suppressPlotToFile = false;
                        }
                    }
                    catch { _suppressPlotToFile = false; }
                }

            }
        }

        [ObservableProperty]
        private bool isCutMode = false; // false = Keep mode (default)

        [ObservableProperty]
        private bool isPointsShown = false; // false = Keep mode (default)
        partial void OnIsPointsShownChanged(bool value)
        {
            ShowHideMarkers(value);
        }

        private string lastLinesTextBox = "0";
        public string LastLinesTextBox
        {
            get
            {
                if (SelectedFile is null) return lastLinesTextBox;

                var total = SelectedFile.Model.Preview.Lines().Length;
                // offset a partir do fim (o que aparece no TextBox)
                // regra: bottomCutLine = total - offset + 1  (1-based first removed)
                var offset = Math.Max(0, total - SelectedFile.BottomCutLine + 1);
                return offset.ToString();
            }
            set
            {
                if (!int.TryParse(value, out var offset) || offset < 0)
                    return;

                if (SelectedFile is not null)
                {
                    var total = SelectedFile.Model.Preview.Lines().Length;

                    // Corte no rodapé = (última linha) - offset + 1 (1-based first removed)
                    // Ex.: total=200, offset=3  -> bottomCutLine = 198
                    // clamp para não sair do arquivo
                    int bottomCutLine = Math.Max(1, total - offset + 1);

                    SelectedFile.BottomCutLine = bottomCutLine;
                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));

                    // update plot selection rectangle too
                    try
                    {
                        if (SelectedFile.Timestamps.Count > 0)
                        {
                            var min = DateTimeAxis.ToDouble(SelectedFile.Timestamps[Math.Max(0, Math.Min(SelectedFile.Timestamps.Count - 1, SelectedFile.TopCutLine))]);
                            var max = DateTimeAxis.ToDouble(SelectedFile.Timestamps[Math.Max(0, Math.Min(SelectedFile.Timestamps.Count - 1, SelectedFile.BottomCutLine - 2))]);

                            _suppressPlotToFile = true;
                            ApplyPlotSelectionToAxis(min, max);
                            _suppressPlotToFile = false;
                        }
                    }
                    catch { _suppressPlotToFile = false; }

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
                    OnPropertyChanged(nameof(BottomContextTextBox));

                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }
            }
        }


        public PlotModel PlotModel { get; private set; } = new() { Title = "Acelerômetro triaxial" };
        public PlotController PlotController { get; private set; } = new();
        // per-file series bundles
        private class SeriesBundle
        {
            public LineSeries ZSeries { get; set; } = new();
            public LineSeries XSeries { get; set; } = new();
            public LineSeries YSeries { get; set; } = new();
            public List<DataPoint> ZPoints { get; set; } = new();
            public List<DataPoint> XPoints { get; set; } = new();
            public List<DataPoint> YPoints { get; set; } = new();
        }
        private readonly Dictionary<ImportedFileViewModel, SeriesBundle> _seriesMap = new();

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

        // suppression to avoid recursion when coercing plot values
        private bool _suppressPlotCoercion = false;

        [ObservableProperty]
        private double plotSelectionMin = 0;
        partial void OnPlotSelectionMinChanged(double value)
        {
            // update text representation when numeric selection changes (e.g. from graph)
            var text = value.ToString(CultureInfo.InvariantCulture);
            if (PlotSelectionMinText != text)
                PlotSelectionMinText = text;

            // Coerce to integer sample index when we have a selected file
            if (!_suppressPlotCoercion && SelectedFile is not null)
            {
                var total = Math.Max(1, SelectedFile.ZMeasurements.Count);
                var intVal = Math.Max(0, Math.Min(total - 1, (int)Math.Floor(value)));
                if (Math.Abs(value - intVal) > 1e-9)
                {
                    try
                    {
                        _suppressPlotCoercion = true;
                        PlotSelectionMin = intVal; // will re-enter this method but _suppressPlotCoercion prevents loop
                    }
                    finally
                    {
                        _suppressPlotCoercion = false;
                    }
                    return;
                }
            }

            // sync to SelectedFile (unless this change was initiated from the file side)
            if (!_suppressPlotToFile && SelectedFile is not null)
            {
                try
                {
                    _suppressFileToPlot = true;
                    SelectedFile.TopCutLine = Math.Max(0, (int)Math.Floor(value));
                    _suppressFileToPlot = false;
                }
                catch { _suppressFileToPlot = false; }
            }

            // at end, notify can execute
            CutSelectionCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private double plotSelectionMax = 0;
        partial void OnPlotSelectionMaxChanged(double value)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);
            if (PlotSelectionMaxText != text)
                PlotSelectionMaxText = text;

            // Coerce to integer sample index when we have a selected file
            if (!_suppressPlotCoercion && SelectedFile is not null)
            {
                var total = Math.Max(1, SelectedFile.ZMeasurements.Count);
                var intVal = Math.Max(0, Math.Min(total - 1, (int)Math.Floor(value)));
                if (Math.Abs(value - intVal) > 1e-9)
                {
                    try
                    {
                        _suppressPlotCoercion = true;
                        PlotSelectionMax = intVal; // re-enter but suppressed
                    }
                    finally
                    {
                        _suppressPlotCoercion = false;
                    }
                    return;
                }
            }

            if (!_suppressPlotToFile && SelectedFile is not null)
            {
                try
                {
                    _suppressFileToPlot = true;
                    // BottomCutLine is 1-based index of first line removed after the kept block
                    // Mapping: BottomCutLine = floor(value) + 2  (so when value==total-1 -> BottomCutLine = total+1 -> no cut)
                    var total = Math.Max(1, SelectedFile.Model.Preview.Lines().Length);
                    int bc = (int)Math.Floor(value) + 2;
                    bc = Math.Max(1, Math.Min(total + 1, bc));
                    SelectedFile.BottomCutLine = bc;
                    _suppressFileToPlot = false;
                }
                catch { _suppressFileToPlot = false; }
            }

            // at end, notify can execute
            CutSelectionCommand.NotifyCanExecuteChanged();
        }

        // string-backed inputs for UI so we can validate/parse immediately
        [ObservableProperty]
        private string plotSelectionMinText = "0";

        [ObservableProperty]
        private string plotSelectionMaxText = "100";

        private void ApplyPlotSelectionToAxis(double min, double max)
        {
            // Previously this method performed axis.Zoom to apply the selection to the X axis.
            // Change: do NOT change axis zoom here. Only update internal selection state and invalidate plot.

            // avoid NaN
            if (double.IsNaN(min) || double.IsNaN(max)) return;
            // ensure min <= max
            if (min > max) (min, max) = (max, min);

            // If we have a selected file, coerce to integer indices
            if (SelectedFile is not null)
            {
                var total = Math.Max(1, SelectedFile.ZMeasurements.Count);
                min = Math.Max(0, Math.Min(total - 1, Math.Floor(min)));
                max = Math.Max(0, Math.Min(total - 1, Math.Floor(max)));
            }

            // Update internal selection values (this will also update the text properties via property changed handlers)
            PlotSelectionMin = min;
            PlotSelectionMax = max;

            // Repaint so UI (e.g. any overlays) can reflect selection change without changing axis
            PlotModel.InvalidatePlot(false);

            // Note: callers typically call RebuildPlotForRange(...) themselves when they want to recalc points.
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
                        // Do not rebuild points or change axis — keep visible graph as-is
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
                        // Do not rebuild points or change axis — keep visible graph as-is
                    }
                }
            };

            oxyThemeHelper.Apply(PlotModel, themeService.Current);
            themeService.ThemeChanged += (sender, baseTheme) => oxyThemeHelper.Apply(PlotModel, baseTheme);

            // respond to collection changes to add/remove series bundles
            ImportedFiles.CollectionChanged += ImportedFiles_CollectionChanged;
            // add initial bundles
            foreach (var f in ImportedFiles) AddSeriesForFile(f);
        }

        private void ImportedFiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ImportedFileViewModel f in e.NewItems) AddSeriesForFile(f);
            }
            if (e.OldItems != null)
            {
                foreach (ImportedFileViewModel f in e.OldItems) RemoveSeriesForFile(f);
            }

            RebuildPlotForCurrentRange();
        }

        private void AddSeriesForFile(ImportedFileViewModel file)
        {
            if (_seriesMap.ContainsKey(file)) return;
            var bundle = new SeriesBundle();

            bundle.ZSeries = new LineSeries
            {
                Title = file.Model.FileName + " - Z",
                StrokeThickness = 1.5,
                MarkerType = MarkerType.None,
                ItemsSource = bundle.ZPoints,
                TrackerFormatString = "{0}\n{2:0.000}"
            };
            bundle.XSeries = new LineSeries
            {
                Title = file.Model.FileName + " - X",
                StrokeThickness = 1.0,
                Color = OxyColors.Red,
                MarkerType = MarkerType.None,
                ItemsSource = bundle.XPoints
            };
            bundle.YSeries = new LineSeries
            {
                Title = file.Model.FileName + " - Y",
                StrokeThickness = 1.0,
                Color = OxyColors.DodgerBlue,
                MarkerType = MarkerType.None,
                ItemsSource = bundle.YPoints
            };

            _seriesMap[file] = bundle;

            PlotModel.Series.Add(bundle.ZSeries);
            PlotModel.Series.Add(bundle.XSeries);
            PlotModel.Series.Add(bundle.YSeries);

            // subscribe to file changes to update series
            file.PropertyChanged += File_PropertyChanged;
        }

        private void RemoveSeriesForFile(ImportedFileViewModel file)
        {
            if (!_seriesMap.TryGetValue(file, out var bundle)) return;
            PlotModel.Series.Remove(bundle.ZSeries);
            PlotModel.Series.Remove(bundle.XSeries);
            PlotModel.Series.Remove(bundle.YSeries);
            _seriesMap.Remove(file);
            file.PropertyChanged -= File_PropertyChanged;
        }

        private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ImportedFileViewModel f)
            {
                // when measurements or timestamps change, refresh series for current visible range
                if (e.PropertyName == nameof(ImportedFileViewModel.ZMeasurements) || e.PropertyName == nameof(ImportedFileViewModel.WorkingPreview))
                {
                    RebuildPlotForCurrentRange();
                }
            }
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

            // Replace bottom axis with DateTimeAxis
            PlotModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy-MM-dd HH:mm:ss",
                Title = "Tempo",
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

            // don't add static series here; series are added per-file in AddSeriesForFile
        }

        // Reconstroi as List<DataPoint> (PlotPointsZ/X/Y) para a faixa [minX,maxX]
        private void RebuildPlotForRange(double minX, double maxX)
        {
            // minX / maxX are axis coordinates (DateTime) values. Convert to DateTime
            var start = DateTimeAxis.ToDateTime(minX);
            var end = DateTimeAxis.ToDateTime(maxX);

            // for each file, build points within range
            foreach (var kv in _seriesMap)
            {
                var file = kv.Key;
                var bundle = kv.Value;

                bundle.ZPoints.Clear();
                bundle.XPoints.Clear();
                bundle.YPoints.Clear();

                var timestamps = file.Timestamps;
                var zs = file.ZMeasurements.ToList();
                var xs = file.XMeasurements.ToList();
                var ys = file.YMeasurements.ToList();

                if (timestamps == null || timestamps.Count == 0) continue;

                // build parallel arrays of X (axis double) and Y values within index window
                var xsD = new List<double>();
                var zsSel = new List<double>();
                var xsSel = new List<double>();
                var ysSel = new List<double>();

                for (int i = 0; i < timestamps.Count; i++)
                {
                    var t = timestamps[i];
                    if (t < start || t > end) continue;
                    xsD.Add(DateTimeAxis.ToDouble(t));
                    zsSel.Add(i < zs.Count ? zs[i] : double.NaN);
                    xsSel.Add(i < xs.Count ? xs[i] : double.NaN);
                    ysSel.Add(i < ys.Count ? ys[i] : double.NaN);
                }

                if (xsD.Count == 0) continue;

                int desired = Math.Clamp(_maxDisplayPoints, MinPointsOnScreen, MaxPointsOnScreen);
                desired = Math.Min(desired, xsD.Count);

                var decZ = DownsampleMinMaxWithX(zsSel, xsD, desired);
                var decX = DownsampleMinMaxWithX(xsSel, xsD, desired);
                var decY = DownsampleMinMaxWithX(ysSel, xsD, desired);

                bundle.ZPoints.AddRange(decZ);
                bundle.XPoints.AddRange(decX);
                bundle.YPoints.AddRange(decY);
            }

            PlotModel.InvalidatePlot(true);
        }

        // Rebuild usando o eixo X atual
        private void RebuildPlotForCurrentRange()
        {
            var xAxis = PlotModel.DefaultXAxis ?? PlotModel.Axes.First(a => a.IsHorizontal());
            double min = xAxis.ActualMinimum;
            double max = xAxis.ActualMaximum;
            RebuildPlotForRange(min, max);
        }

        // Downsampling that preserves X positions
        private List<DataPoint> DownsampleMinMaxWithX(IList<double> values, IList<double> xs, int maxPoints)
        {
            int n = values?.Count ?? 0;
            var result = new List<DataPoint>();
            if (n == 0) return result;
            if (n <= maxPoints)
            {
                result.Capacity = n;
                for (int i = 0; i < n; i++)
                    result.Add(new DataPoint(xs[i], values[i]));
                return result;
            }

            int targetBuckets = Math.Max(1, maxPoints / 2);
            double bucketSize = (double)n / targetBuckets;

            // include first
            result.Add(new DataPoint(xs[0], values[0]));

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
                    result.Add(new DataPoint(xs[minIdx], minVal));
                }
                else if (minIdx < maxIdx)
                {
                    result.Add(new DataPoint(xs[minIdx], minVal));
                    result.Add(new DataPoint(xs[maxIdx], maxVal));
                }
                else
                {
                    result.Add(new DataPoint(xs[maxIdx], maxVal));
                    result.Add(new DataPoint(xs[minIdx], minVal));
                }
            }

            // include last
            result.Add(new DataPoint(xs[n - 1], values[n - 1]));

            result = result.OrderBy(p => p.X).ToList();
            return result;
        }

        [RelayCommand]
        private void RangeSelected((double Min, double Max) range)
        {
            var (min, max) = range;

            // If selection given, coerce to nearest axis domain values
            ApplyPlotSelectionToAxis(min, max);
            OnPropertyChanged(nameof(TextInitLabelVisibility));
        }
        // Esse método só inverte (toggle) enquanto a sobrecarga o controla pelo value passado
        [RelayCommand(CanExecute = nameof(CanShowHideMarkers))]
        private void ShowHideMarkers() => ShowHideMarkers(!IsShowingMarkers);
        private void ShowHideMarkers(bool value)
        {
            foreach (var b in _seriesMap.Values)
            {
                if (value)
                {
                    b.ZSeries.MarkerType = MarkerType.Circle;
                    b.XSeries.MarkerType = MarkerType.Circle;
                    b.YSeries.MarkerType = MarkerType.Circle;
                }
                else
                {
                    b.ZSeries.MarkerType = MarkerType.None;
                    b.XSeries.MarkerType = MarkerType.None;
                    b.YSeries.MarkerType = MarkerType.None;
                }
            }
            IsShowingMarkers = value;
            PlotModel?.InvalidatePlot(true);
        }
        private bool CanShowHideMarkers() => _seriesMap.Values.Sum(b => b.ZPoints.Count + b.XPoints.Count + b.YPoints.Count) > 0;

        [RelayCommand]
        private void NavigateBack()
        {
            bool hasChanges = false;
            try
            {
                if (ImportedFiles != null && ImportedFiles.Count > 0)
                {
                    hasChanges = ImportedFiles.Any(f =>
                        (f.RemovedRanges != null && f.RemovedRanges.Count > 0) ||
                        (f.WorkingPreview != null && f.Model?.Preview != null && !ReferenceEquals(f.WorkingPreview, f.Model.Preview) && !string.Equals(f.WorkingPreview, f.Model.Preview, StringComparison.Ordinal))
                        );
                }
            }
            catch
            {
                hasChanges = true;
            }

            if (hasChanges)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(System.Windows.Application.Current.MainWindow,
                        "Existem alterações não salvas. Deseja descartar as alterações e voltar?",
                        "Confirmar retorno",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        ImportedFiles?.Clear();
                        NavigateInvoked?.Invoke(this, new());
                    }
                    // se escolher No, não faz nada (permanece na tela)
                });
            }
            else
            {
                ImportedFiles?.Clear();
                NavigateInvoked?.Invoke(this, new());
            }
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            var textPaths = fileImportService.OpenDialogSelectTextFiles();
            var imported = await fileImportService.ImportTextFilesAsync(textPaths);
            fileHandlerHelper.AddFiles(imported);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRemoveSelectedCommand))]
        private void RemoveSelected()
        {
            if (SelectedFile != null)
            {
                RemoveSeriesForFile(SelectedFile);
                ImportedFiles.Remove(SelectedFile);
                SelectedFile = ImportedFiles!.FirstOrDefault()!;
            }
        }
        private bool CanExecuteRemoveSelectedCommand() => SelectedFile != null && ImportedFiles.Count > 0;

        [RelayCommand(CanExecute = nameof(CanExecuteProcessAllCommand))]
        private async Task ProcessAll()
        {
            try
            {
                var readOnlyList = new ReadOnlyObservableCollection<ImportedFileViewModel>(ImportedFiles);
                var ok = await fileExportService.ExportAsync(readOnlyList, owner: System.Windows.Application.Current.MainWindow);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (ok)
                    {
                        MessageBox.Show(System.Windows.Application.Current.MainWindow,
                            "Arquivos processados e salvos com sucesso.\nVerifique a pasta selecionada para os arquivos gerados.",
                            "Processamento concluído",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(System.Windows.Application.Current.MainWindow,
                            "Operação cancelada ou nenhum arquivo para processar.",
                            "Processamento cancelado",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(System.Windows.Application.Current.MainWindow,
                        "Operação cancelada pelo usuário.",
                        "Cancelado",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(System.Windows.Application.Current.MainWindow,
                        $"Erro ao processar arquivos:\n{ex.Message}",
                        "Erro",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        private bool CanExecuteProcessAllCommand() => SelectedFile != null && ImportedFiles.Count > 0; // Alterar depois

        [RelayCommand(CanExecute = nameof(CanExecuteExportSelectionCommand))]
        private async Task ExportSelection()
        {
            if (SelectedFile is null) return;

            try
            {
                int start = Math.Max(0, (int)Math.Floor(PlotSelectionMin));
                int end = Math.Max(0, (int)Math.Floor(PlotSelectionMax));

                var ok = await fileExportService.ExportSelectionAsync(SelectedFile, start, end, owner: System.Windows.Application.Current.MainWindow);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (ok)
                    {
                        MessageBox.Show(System.Windows.Application.Current.MainWindow,
                            $"Seleção exportada com sucesso para o arquivo referente a '{SelectedFile.Model.FileName}'.\nLinhas exportadas: {start} a {end}.",
                            "Exportação concluída",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(System.Windows.Application.Current.MainWindow,
                            "Operação cancelada ou nada a exportar.",
                            "Exportação cancelada",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(System.Windows.Application.Current.MainWindow,
                        "Exportação cancelada pelo usuário.",
                        "Cancelado",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(System.Windows.Application.Current.MainWindow,
                        $"Erro ao exportar seleção:\n{ex.Message}",
                        "Erro",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        private bool CanExecuteExportSelectionCommand() => SelectedFile != null; 

        [RelayCommand]
        private void ApplyToAll()
        {
            if (ImportedFiles.Count <= 1 || SelectedFile is null)
                return;

            int selTotal = SelectedFile.Model.Preview.Lines().Length;
            int nKeepFromEnd = Math.Max(0, selTotal - SelectedFile.BottomCutLine + 1);

            foreach (var file in ImportedFiles)
            {
                if (ReferenceEquals(file, SelectedFile)) continue;
                file.TopCutLine = SelectedFile.TopCutLine;
                int total = file.Model.Preview.Lines().Length;
                int cutIndexFromTop = Math.Max(1, total - nKeepFromEnd + 1);
                file.BottomCutLine = cutIndexFromTop;
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
            // Do not rebuild or change visible plotted points
        }
        [RelayCommand]
        private void DecreasePlotMin(int step = 1)
        {
            PlotSelectionMin = Math.Max(0, PlotSelectionMin - step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            // Do not rebuild or change visible plotted points
        }

        [RelayCommand]
        private void IncreasePlotMax(int step = 1)
        {
            if (SelectedFile == null) return;
            double maxIndex = Math.Max(0, SelectedFile.ZMeasurements.Count - 1);
            PlotSelectionMax = Math.Min(maxIndex, PlotSelectionMax + step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            // Do not rebuild or change visible plotted points
        }
        [RelayCommand]
        private void DecreasePlotMax(int step = 1)
        {
            PlotSelectionMax = Math.Max(0, PlotSelectionMax - step);
            ApplyPlotSelectionToAxis(PlotSelectionMin, PlotSelectionMax);
            // Do not rebuild or change visible plotted points
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCutSelection))]
        private void CutSelection()
        {
            if (SelectedFile is null) return;

            // interpret PlotSelectionMin/Max as axis (datetime) values
            var start = DateTimeAxis.ToDateTime(PlotSelectionMin);
            var end = DateTimeAxis.ToDateTime(PlotSelectionMax);

            // apply cut to all imported files using their timestamp indices
            foreach (var f in ImportedFiles)
            {
                var (sIdx, eIdx) = f.GetIndexRangeForTimestampRange(start, end);
                var total = f.WorkingPreview.Lines().Length;

                if (IsCutMode)
                {
                    if (sIdx <= eIdx)
                        f.CutRange(sIdx, eIdx);
                }
                else
                {
                    // Keep mode: remove everything outside [sIdx,eIdx]
                    if (eIdx < total - 1)
                    {
                        f.CutRange(eIdx + 1, total - 1);
                    }
                    if (sIdx > 0)
                    {
                        f.CutRange(0, sIdx - 1);
                    }
                }
            }

            RebuildPlotForCurrentRange();
            ShowHideMarkersCommand.NotifyCanExecuteChanged();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            ProcessAllCommand.NotifyCanExecuteChanged();
        }

        private bool CanExecuteCutSelection()
        {
            if (SelectedFile is null) return false;
            return PlotSelectionMax >= PlotSelectionMin;
        }
    }
}
