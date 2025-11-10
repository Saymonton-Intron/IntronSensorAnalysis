using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace IntronFileController.Views.Controls
{
    public partial class NumericUpDown : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(NumericUpDown), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum), typeof(double), typeof(NumericUpDown), new PropertyMetadata(double.MinValue));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(NumericUpDown), new PropertyMetadata(double.MaxValue));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step), typeof(double), typeof(NumericUpDown), new PropertyMetadata(1.0));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, Math.Max(Minimum, Math.Min(Maximum, value)));
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        private DispatcherTimer _holdTimer;
        private bool _isHoldingUp = false;
        private bool _isHoldingDown = false;

        public NumericUpDown()
        {
            InitializeComponent();
            PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            PART_TextBox.LostFocus += TextBox_LostFocus;

            _holdTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _holdTimer.Interval = TimeSpan.FromMilliseconds(120);
            _holdTimer.Tick += HoldTimer_Tick;
        }

        private void HoldTimer_Tick(object? sender, EventArgs e)
        {
            if (_isHoldingUp)
            {
                // accelerate while holding
                double inc = Math.Max(1, Step * 5);
                Value = Math.Min(Maximum, Value + inc);
                PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            }
            else if (_isHoldingDown)
            {
                double dec = Math.Max(1, Step * 5);
                Value = Math.Max(Minimum, Value - dec);
                PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void ButtonUp_Click(object sender, RoutedEventArgs e)
        {
            Value = Math.Min(Maximum, Value + Step);
            PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
        }

        private void ButtonDown_Click(object sender, RoutedEventArgs e)
        {
            Value = Math.Max(Minimum, Value - Step);
            PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly Regex _regex = new("[^0-9.-]+");
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _regex.IsMatch(e.Text);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(PART_TextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            {
                Value = Math.Max(Minimum, Math.Min(Maximum, v));
                PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                PART_TextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender == PART_ButtonUp)
            {
                _isHoldingUp = true;
                _holdTimer.Start();
            }
            else if (sender == PART_ButtonDown)
            {
                _isHoldingDown = true;
                _holdTimer.Start();
            }
        }

        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isHoldingUp = false;
            _isHoldingDown = false;
            _holdTimer.Stop();
        }
    }
}