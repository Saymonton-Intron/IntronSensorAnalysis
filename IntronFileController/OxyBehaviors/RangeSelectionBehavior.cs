using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.SkiaSharp.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace IntronFileController.OxyBehaviors
{ 
    public static class RangeSelectionBehavior
    {
        // === Attached DPs já existentes (resumo) ===
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(RangeSelectionBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));
        public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);
        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty RangeSelectedCommandProperty =
            DependencyProperty.RegisterAttached(
                "RangeSelectedCommand", typeof(ICommand), typeof(RangeSelectionBehavior),
                new PropertyMetadata(null));
        public static void SetRangeSelectedCommand(DependencyObject d, ICommand v) => d.SetValue(RangeSelectedCommandProperty, v);
        public static ICommand GetRangeSelectedCommand(DependencyObject d) => (ICommand)d.GetValue(RangeSelectedCommandProperty);

        public static readonly DependencyProperty FillColorProperty =
            DependencyProperty.RegisterAttached(
                "FillColor", typeof(Color), typeof(RangeSelectionBehavior),
                new PropertyMetadata(Color.FromArgb(0x4D, 0xFF, 0xFF, 0x00)));
        public static void SetFillColor(DependencyObject d, Color v) => d.SetValue(FillColorProperty, v);
        public static Color GetFillColor(DependencyObject d) => (Color)d.GetValue(FillColorProperty);

        public static readonly DependencyProperty XAxisKeyProperty =
            DependencyProperty.RegisterAttached(
                "XAxisKey", typeof(string), typeof(RangeSelectionBehavior), new PropertyMetadata(null));
        public static void SetXAxisKey(DependencyObject d, string v) => d.SetValue(XAxisKeyProperty, v);
        public static string GetXAxisKey(DependencyObject d) => (string)d.GetValue(XAxisKeyProperty);

        // === NOVO: Selected range exposto como DP ===
        public static readonly DependencyProperty SelectedMinXProperty =
            DependencyProperty.RegisterAttached(
                "SelectedMinX", typeof(double), typeof(RangeSelectionBehavior),
                new PropertyMetadata(double.NaN, OnSelectedRangeDpChanged));
        public static void SetSelectedMinX(DependencyObject d, double v) => d.SetValue(SelectedMinXProperty, v);
        public static double GetSelectedMinX(DependencyObject d) => (double)d.GetValue(SelectedMinXProperty);

        public static readonly DependencyProperty SelectedMaxXProperty =
            DependencyProperty.RegisterAttached(
                "SelectedMaxX", typeof(double), typeof(RangeSelectionBehavior),
                new PropertyMetadata(double.NaN, OnSelectedRangeDpChanged));
        public static void SetSelectedMaxX(DependencyObject d, double v) => d.SetValue(SelectedMaxXProperty, v);
        public static double GetSelectedMaxX(DependencyObject d) => (double)d.GetValue(SelectedMaxXProperty);

        // Quando true, alterações via DP também disparam o Command
        public static readonly DependencyProperty RaiseCommandOnProgrammaticChangeProperty =
            DependencyProperty.RegisterAttached(
                "RaiseCommandOnProgrammaticChange", typeof(bool), typeof(RangeSelectionBehavior),
                new PropertyMetadata(false));
        public static void SetRaiseCommandOnProgrammaticChange(DependencyObject d, bool v) => d.SetValue(RaiseCommandOnProgrammaticChangeProperty, v);
        public static bool GetRaiseCommandOnProgrammaticChange(DependencyObject d) => (bool)d.GetValue(RaiseCommandOnProgrammaticChangeProperty);

        // Mostrar/ocultar retângulo quando range inválido
        public static readonly DependencyProperty KeepRectangleWhenInvalidProperty =
            DependencyProperty.RegisterAttached(
                "KeepRectangleWhenInvalid", typeof(bool), typeof(RangeSelectionBehavior),
                new PropertyMetadata(false));
        public static void SetKeepRectangleWhenInvalid(DependencyObject d, bool v) => d.SetValue(KeepRectangleWhenInvalidProperty, v);
        public static bool GetKeepRectangleWhenInvalid(DependencyObject d) => (bool)d.GetValue(KeepRectangleWhenInvalidProperty);

        // === Estado interno ===
        private class State
        {
            public bool Dragging;
            public double X0;
            public RectangleAnnotation Rect;
            public bool SuppressFeedback; // evita loop entre DP <-> mouse
        }
        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached("State", typeof(State), typeof(RangeSelectionBehavior), new PropertyMetadata(null));
        private static void SetState(DependencyObject d, State v) => d.SetValue(StateProperty, v);
        private static State GetState(DependencyObject d) => (State)d.GetValue(StateProperty);

        // === Helpers básicos ===
        static OxyColor ToOxy(Color c) => OxyColor.FromArgb(c.A, c.R, c.G, c.B);

        private static Axis GetXAxis(PlotModel model, string xKey)
        {
            if (model == null) return null;
            if (!string.IsNullOrWhiteSpace(xKey))
                return model.Axes.FirstOrDefault(a => (a.Position is AxisPosition.Bottom or AxisPosition.Top) && a.Key == xKey)
                    ?? model.DefaultXAxis;
            return model.DefaultXAxis
                ?? model.Axes.FirstOrDefault(a => a.Position is AxisPosition.Bottom or AxisPosition.Top);
        }
        private static Axis GetYAxis(PlotModel model)
        {
            if (model == null) return null;
            return model.DefaultYAxis
                ?? model.Axes.FirstOrDefault(a => a.Position is AxisPosition.Left or AxisPosition.Right);
        }

        private static void EnsureRect(PlotView pv, State st)
        {
            var model = pv.ActualModel; if (model == null) return;
            if (st.Rect == null)
            {
                st.Rect = new RectangleAnnotation
                {
                    Layer = AnnotationLayer.AboveSeries,
                    Fill = ToOxy(GetFillColor(pv)),
                    Stroke = OxyColors.Transparent
                };
                model.Annotations.Add(st.Rect);
            }
            var y = GetYAxis(model);
            if (y != null)
            {
                st.Rect.MinimumY = y.ActualMinimum;
                st.Rect.MaximumY = y.ActualMaximum;
            }
        }

        private static double? ToDataX(PlotView pv, Point mouse)
        {
            var model = pv?.ActualModel;
            var xAxis = GetXAxis(model, GetXAxisKey(pv));
            if (model == null || xAxis == null) return null;

            var pa = model.PlotArea;
            var p = mouse;
            if (p.X < pa.Left || p.X > pa.Right || p.Y < pa.Top || p.Y > pa.Bottom) return null;

            return xAxis.InverseTransform(p.X);
        }

        // === Wire/unwire ===
        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PlotView pv) return;

            if ((bool)e.NewValue)
            {
                pv.PreviewMouseLeftButtonDown += OnMouseDown;
                pv.PreviewMouseMove += OnMouseMove;
                pv.PreviewMouseLeftButtonUp += OnMouseUp;
                pv.Unloaded += OnUnloaded;
                SetState(pv, new State());
            }
            else
            {
                pv.PreviewMouseLeftButtonDown -= OnMouseDown;
                pv.PreviewMouseMove -= OnMouseMove;
                pv.PreviewMouseLeftButtonUp -= OnMouseUp;
                pv.Unloaded -= OnUnloaded;
                SetState(pv, null);
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView pv) SetIsEnabled(pv, false);
        }

        // === Mouse → atualiza DP e retângulo ===
        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Shift) return;
            if (sender is not PlotView pv) return;
            var st = GetState(pv) ?? new State();
            var model = pv.ActualModel; if (model == null) return;

            var x0 = ToDataX(pv, e.GetPosition(pv)); if (x0 == null) return;

            st.Dragging = true; st.X0 = x0.Value;
            EnsureRect(pv, st);

            st.Rect.MinimumX = st.X0;
            st.Rect.MaximumX = st.X0;

            model.InvalidatePlot(false);
            pv.CaptureMouse();
            SetState(pv, st);
            e.Handled = true;
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not PlotView pv) return;
            var st = GetState(pv);
            if (st == null || !st.Dragging) return;

            var x = ToDataX(pv, e.GetPosition(pv)); if (x == null) return;

            EnsureRect(pv, st);
            st.Rect.MinimumX = Math.Min(st.X0, x.Value);
            st.Rect.MaximumX = Math.Max(st.X0, x.Value);

            var model = pv.ActualModel;
            var y = GetYAxis(model);
            if (y != null)
            {
                st.Rect.MinimumY = y.ActualMinimum;
                st.Rect.MaximumY = y.ActualMaximum;
            }

            model.InvalidatePlot(false);
        }

        private static void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not PlotView pv) return;
            var st = GetState(pv);
            var model = pv.ActualModel;
            if (st == null || model == null) return;

            pv.ReleaseMouseCapture();
            if (!st.Dragging) return;
            st.Dragging = false;

            var x1 = ToDataX(pv, e.GetPosition(pv)); if (x1 == null) return;

            var min = Math.Min(st.X0, x1.Value);
            var max = Math.Max(st.X0, x1.Value);
            if (Math.Abs(max - min) < 1e-9)
            {
                if (!GetKeepRectangleWhenInvalid(pv) && st.Rect != null)
                {
                    model.Annotations.Remove(st.Rect);
                    st.Rect = null;
                    model.InvalidatePlot(false);
                }
                return;
            }

            // Atualiza DPs (dispara bindings)
            st.SuppressFeedback = true;
            SetSelectedMinX(pv, min);
            SetSelectedMaxX(pv, max);
            st.SuppressFeedback = false;

            // Dispara comando
            var cmd = GetRangeSelectedCommand(pv);
            if (cmd != null && cmd.CanExecute((min, max))) cmd.Execute((min, max));
        }

        // === DP → atualiza retângulo (programático) e, se quiser, dispara comando ===
        private static void OnSelectedRangeDpChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PlotView pv) return;
            var st = GetState(pv) ?? new State();
            var model = pv.ActualModel;
            if (model == null) return;

            var min = GetSelectedMinX(pv);
            var max = GetSelectedMaxX(pv);

            // range válido?
            bool valid = !(double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max) || max <= min);

            if (!valid)
            {
                if (!GetKeepRectangleWhenInvalid(pv) && st.Rect != null)
                {
                    model.Annotations.Remove(st.Rect);
                    st.Rect = null;
                    model.InvalidatePlot(false);
                }
                return;
            }

            EnsureRect(pv, st);
            st.Rect.MinimumX = min;
            st.Rect.MaximumX = max;

            var y = GetYAxis(model);
            if (y != null)
            {
                st.Rect.MinimumY = y.ActualMinimum;
                st.Rect.MaximumY = y.ActualMaximum;
            }

            model.InvalidatePlot(false);

            // Evita loop: só dispara command quando mudança veio do código (e se habilitado)
            if (!st.SuppressFeedback && GetRaiseCommandOnProgrammaticChange(pv))
            {
                var cmd = GetRangeSelectedCommand(pv);
                if (cmd != null && cmd.CanExecute((min, max))) cmd.Execute((min, max));
            }

            SetState(pv, st);
        }
    }

}
