using MaterialDesignThemes.Wpf;
using OxyPlot;

namespace IntronFileController.Helpers
{
    public interface IOxyThemeHelper
    {
        void Apply(PlotModel model, BaseTheme theme);
    }

    public sealed class OxyThemeHelper : IOxyThemeHelper
    {
        private static readonly OxyColor Gray50 = OxyColor.FromRgb(50, 50, 50);
        private static readonly OxyColor White241 = OxyColor.FromRgb(241, 241, 241);

        public void Apply(PlotModel model, BaseTheme theme)
        {
            if (model is null) return;

            if (theme == BaseTheme.Dark)
            {
                model.Background = Gray50;
                model.TextColor = OxyColors.White;
                model.PlotAreaBorderColor = OxyColors.White;
                foreach (var axis in model.Axes)
                {
                    axis.TextColor = OxyColors.White;
                    axis.TicklineColor = OxyColors.White;
                    axis.MajorGridlineColor = OxyColor.FromAColor(60, OxyColors.Gray);
                    axis.MinorGridlineColor = OxyColor.FromAColor(20, OxyColors.Gray);
                    axis.MajorGridlineStyle = LineStyle.Solid;
                    axis.MinorGridlineStyle = LineStyle.Dot;
                }
                foreach (var legend in model.Legends)
                {
                    legend.LegendTextColor = OxyColors.White;
                }
            }
            else // Light
            {
                model.Background = White241;
                model.TextColor = OxyColors.Black;
                model.PlotAreaBorderColor = OxyColors.Gray;
                foreach (var axis in model.Axes)
                {
                    axis.TextColor = OxyColors.Black;
                    axis.TicklineColor = OxyColors.Gray;
                    axis.MajorGridlineColor = OxyColor.FromAColor(60, OxyColors.Gray);
                    axis.MinorGridlineColor = OxyColor.FromAColor(20, OxyColors.Gray);
                    axis.MajorGridlineStyle = LineStyle.Solid;
                    axis.MinorGridlineStyle = LineStyle.Dot;
                }
                foreach (var legend in model.Legends)
                {
                    legend.LegendTextColor = OxyColors.Black;
                }
            }

            model.InvalidatePlot(false);
        }
    }
}
