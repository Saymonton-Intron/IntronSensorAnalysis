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
using System.Windows.Input;

namespace IntronFileController.OxyBehaviors
{
    public static class RangeSelectionBehavior
    {
        // Liga/desliga o recurso
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(RangeSelectionBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        // Comando na VM que recebe o range (MinX, MaxX)
        public static readonly DependencyProperty RangeSelectedCommandProperty =
            DependencyProperty.RegisterAttached(
                "RangeSelectedCommand",
                typeof(ICommand),
                typeof(RangeSelectionBehavior),
                new PropertyMetadata(null));

        public static void SetRangeSelectedCommand(DependencyObject d, ICommand value) => d.SetValue(RangeSelectedCommandProperty, value);
        public static ICommand GetRangeSelectedCommand(DependencyObject d) => (ICommand)d.GetValue(RangeSelectedCommandProperty);

        // Opção: chave do eixo X (se você usa múltiplos eixos)
        public static readonly DependencyProperty XAxisKeyProperty =
            DependencyProperty.RegisterAttached(
                "XAxisKey",
                typeof(string),
                typeof(RangeSelectionBehavior),
                new PropertyMetadata(null));

        public static void SetXAxisKey(DependencyObject d, string value) => d.SetValue(XAxisKeyProperty, value);
        public static string GetXAxisKey(DependencyObject d) => (string)d.GetValue(XAxisKeyProperty);

        // Cor/alpha do retângulo
        public static readonly DependencyProperty FillColorProperty =
            DependencyProperty.RegisterAttached(
                "FillColor",
                typeof(OxyColor),
                typeof(RangeSelectionBehavior),
                new PropertyMetadata(OxyColor.FromAColor(80, OxyColors.Yellow)));

        public static void SetFillColor(DependencyObject d, OxyColor value) => d.SetValue(FillColorProperty, value);
        public static OxyColor GetFillColor(DependencyObject d) => (OxyColor)d.GetValue(FillColorProperty);

        // Estado interno por PlotView
        private class State
        {
            public bool Dragging;
            public double X0;
            public RectangleAnnotation Rect;
        }

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached("State", typeof(State), typeof(RangeSelectionBehavior), new PropertyMetadata(null));

        private static void SetState(DependencyObject d, State value) => d.SetValue(StateProperty, value);
        private static State GetState(DependencyObject d) => (State)d.GetValue(StateProperty);

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
            if (sender is PlotView pv)
            {
                SetIsEnabled(pv, false);
            }
        }

        private static Axis GetXAxis(PlotModel model, string xKey)
        {
            if (model == null) return null;

            if (!string.IsNullOrWhiteSpace(xKey))
                return model.Axes.FirstOrDefault(a => a.Position is AxisPosition.Bottom or AxisPosition.Top
                                                    && a.Key == xKey)
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


        // Converte posição do mouse (WPF) para X no eixo (dados)
        private static double? ToDataX(PlotView pv, Point mouse)
        {
            var model = pv?.ActualModel;
            var xAxis = GetXAxis(model, GetXAxisKey(pv));
            if (model == null || xAxis == null) return null;

            // Coordenada X relativa à PlotArea
            var pa = model.PlotArea;
            var sx = mouse.X; // posição no controle
            var sy = mouse.Y;

            // Só considerar se dentro da PlotArea
            if (sx < pa.Left || sx > pa.Right || sy < pa.Top || sy > pa.Bottom)
                return null;

            // Eixo trabalha em coordenadas de "screen" relativas à PlotArea
            var screenX = sx;
            // Em OxyPlot, Axis.InverseTransform aceita a coordenada X de tela
            return xAxis.InverseTransform(screenX);
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Ativa seleção somente se Shift estiver pressionado
            if (Keyboard.Modifiers != ModifierKeys.Shift) return;

            if (sender is not PlotView pv) return;
            var st = GetState(pv) ?? new State();
            var model = pv.ActualModel;
            if (model == null) return;

            var x0 = ToDataX(pv, e.GetPosition(pv));
            if (x0 == null) return;

            st.Dragging = true;
            st.X0 = x0.Value;

            // Cria (ou reutiliza) a annotation
            if (st.Rect == null)
            {
                st.Rect = new RectangleAnnotation
                {
                    Layer = AnnotationLayer.AboveSeries,
                    Fill = GetFillColor(pv),
                    Stroke = OxyColors.Transparent,
                    MinimumY = GetYAxis(model)?.ActualMinimum ?? double.NaN,
                    MaximumY = GetYAxis(model)?.ActualMaximum ?? double.NaN
                };
                model.Annotations.Add(st.Rect);
            }

            // Inicializa retângulo
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
            if (st == null || !st.Dragging || st.Rect == null) return;

            var x = ToDataX(pv, e.GetPosition(pv));
            if (x == null) return;

            st.Rect.MinimumX = Math.Min(st.X0, x.Value);
            st.Rect.MaximumX = Math.Max(st.X0, x.Value);

            // Atualiza Y para acompanhar zooms durante o drag
            var model = pv.ActualModel;
            var yAxis = GetYAxis(model);
            if (yAxis != null)
            {
                st.Rect.MinimumY = yAxis.ActualMinimum;
                st.Rect.MaximumY = yAxis.ActualMaximum;
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

            if (!st.Dragging)
                return;

            st.Dragging = false;

            var x1 = ToDataX(pv, e.GetPosition(pv));
            if (x1 == null) return;

            var min = Math.Min(st.X0, x1.Value);
            var max = Math.Max(st.X0, x1.Value);

            // Evita ranges minúsculos por clique sem arrastar
            if (Math.Abs(max - min) < 1e-9)
            {
                // limpa a seleção visual
                if (st.Rect != null)
                {
                    model.Annotations.Remove(st.Rect);
                    st.Rect = null;
                    model.InvalidatePlot(false);
                }
                return;
            }

            // Mantém o retângulo visível (ou remova se não quiser)
            model.InvalidatePlot(false);

            // Dispara o comando
            var cmd = GetRangeSelectedCommand(pv);
            if (cmd != null && cmd.CanExecute((min, max)))
                cmd.Execute((min, max));
        }
    }
}
