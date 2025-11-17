using CommunityToolkit.Mvvm.ComponentModel;
using IntronFileController.Common.Extensions;
using IntronFileController.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace IntronFileController.ViewModels;

public partial class ImportedFileViewModel : ObservableObject
{
    public ImportedFile Model { get; }
    [ObservableProperty] private ObservableCollection<double> zMeasurements = [];
    [ObservableProperty] private ObservableCollection<double> xMeasurements = [];
    [ObservableProperty] private ObservableCollection<double> yMeasurements = [];

    // Keep original preview (timestamped) and a working copy where temporary cuts are applied
    private string _originalPreview;
    private string _working_preview;

    // Raw data (original numeric lines without timestamps) for efficient recalculation
    private readonly string _rawData;

    // parsed timestamps for each line in WorkingPreview (keeps sync with measurements)
    public List<DateTime> Timestamps { get; private set; } = new();

    // list of removed ranges (start inclusive, end inclusive) for potential future use
    public List<(int Start, int End)> RemovedRanges { get; } = new();

    public ImportedFileViewModel(ImportedFile model)
    {
        Model = model;

        // Model.RawData contains original data lines (sample index;z;x;y)
        _rawData = model.RawData ?? string.Empty;

        // If Model.Preview already contains timestamped lines, keep it; otherwise build from raw
        if (string.IsNullOrWhiteSpace(model.Preview))
        {
            _originalPreview = BuildTimestampedPreviewFromRaw(_rawData, model.ParsedHeader);
            Model.Preview = _originalPreview;
        }
        else
        {
            _originalPreview = model.Preview;
        }

        _working_preview = _originalPreview;

        // defaults “de fábrica”
        TopCutLine = 0;          // linha alvo topo (1-based)
        TopContextCount = 20;       // qtas linhas pegar (antes e depois)
        // set BottomCutLine to total+1 so that by default nothing is cut at bottom
        BottomCutLine = WorkingPreview.Lines().Length + 1;       // linha alvo fundo (1-based)
        BottomContextCount = 20;

        // Preencher lista de leituras a partir do working preview
        RebuildMeasurementsFromPreview();
    }

    public int SamplingRate
    {
        get => Model.ParsedHeader?.SamplingRate ?? 0;
        set
        {
            if (Model.ParsedHeader == null) return;
            if (Model.ParsedHeader.SamplingRate == value) return;
            Model.ParsedHeader.SamplingRate = value;
            // Recalculate timestamped preview from raw data
            RecalculateTimestamps();
            OnPropertyChanged();
        }
    }

    private void RebuildMeasurementsFromPreview()
    {
        ZMeasurements.Clear();
        XMeasurements.Clear();
        YMeasurements.Clear();
        Timestamps.Clear();

        var header = Model.ParsedHeader;

        foreach (var line in WorkingPreview.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split(';'); // 0 é o datetime agora
            if (p.Length < 4) continue;

            // parse timestamp
            DateTime ts = default;
            var tsRaw = p[0].Trim();
            if (!string.IsNullOrEmpty(header?.DateFormat))
            {
                if (!DateTime.TryParseExact(tsRaw, header.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out ts))
                {
                    DateTime.TryParse(tsRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out ts);
                }
            }
            else
            {
                DateTime.TryParse(tsRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out ts);
            }

            Timestamps.Add(ts);

            // columns: Datetime;Measure Ch_Z(g);Ch_X(g);Ch_Y(g)
            if (double.TryParse(p[1].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var z)) ZMeasurements.Add(z);
            if (double.TryParse(p[2].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var x)) XMeasurements.Add(x);
            if (double.TryParse(p[3].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var y)) YMeasurements.Add(y);
        }

        // notify plot/viewmodel that counts changed if needed
        OnPropertyChanged(nameof(ZMeasurements));
        OnPropertyChanged(nameof(XMeasurements));
        OnPropertyChanged(nameof(YMeasurements));
        OnPropertyChanged(nameof(Timestamps));
    }

    // Builds timestamped preview (Datetime;z;x;y) from raw data lines (index;z;x;y) using header info
    private static string BuildTimestampedPreviewFromRaw(string rawData, SensorFileHeader header)
    {
        if (string.IsNullOrWhiteSpace(rawData)) return string.Empty;
        if (header == null) return rawData;

        var lines = rawData.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();

        // if header.Date is default or sampling rate invalid, return raw joined
        if (header.Date == default || header.SamplingRate <= 0)
        {
            return string.Join("\r\n", lines);
        }

        double intervalSeconds = 1.0 / header.SamplingRate;

        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            var parts = l.Split(';');
            if (parts.Length < 4)
                continue;

            // parse sample index (first column) but fallback to i
            int index = i;
            if (int.TryParse(parts[0], out var idx)) index = idx;

            var timestamp = header.Date.AddSeconds(index * intervalSeconds);
            // format using header.DateFormat if present or ISO otherwise
            string ts = !string.IsNullOrEmpty(header.DateFormat) ? timestamp.ToString(header.DateFormat, CultureInfo.InvariantCulture) : timestamp.ToString("o", CultureInfo.InvariantCulture);

            // preserve numeric columns as found but normalize decimal separator to '.' using InvariantCulture
            var zStr = parts.Length > 1 ? parts[1].Trim().Replace(',', '.') : "";
            var xStr = parts.Length > 2 ? parts[2].Trim().Replace(',', '.') : "";
            var yStr = parts.Length > 3 ? parts[3].Trim().Replace(',', '.') : "";

            sb.Append(ts);
            sb.Append(';');
            sb.Append(zStr);
            sb.Append(';');
            sb.Append(xStr);
            sb.Append(';');
            sb.Append(yStr);
            if (i < lines.Length - 1) sb.Append("\r\n");
        }

        return sb.ToString();
    }

    // Recalculate timestamped preview from raw data and update working/original previews
    public void RecalculateTimestamps()
    {
        var newPreview = BuildTimestampedPreviewFromRaw(_rawData, Model.ParsedHeader);
        // update model and working preview
        Model.Preview = newPreview;
        WorkingPreview = newPreview;
        RebuildMeasurementsFromPreview();
        BottomCutLine = Math.Max(1, WorkingPreview.Lines().Length + 1);
        RaiseAll();
    }

    // Expose working preview for other components if necessary
    public string WorkingPreview
    {
        get => _working_preview;
        private set
        {
            if (_working_preview == value) return;
            _working_preview = value;
            OnPropertyChanged();
        }
    }

    //----------- TOPO -----------

    /// <summary>
    /// linha de corte do topo (1-based)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstLines))]
    [NotifyPropertyChangedFor(nameof(SelectedFirstLines))]
    private int topCutLine;

    /// <summary>
    /// qtas linhas exibir antes ou depois
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstLines))]
    [NotifyPropertyChangedFor(nameof(SelectedFirstLines))]
    private int topContextCount;

    /// <summary>
    /// bloco que será removido (antes do separador)
    /// </summary>
    public string FirstLines => WorkingPreview.TopContextBlock(TopCutLine, TopContextCount);

    /// <summary>
    /// bloco que será selecionado (após o separador), recortado
    /// </summary>
    public string SelectedFirstLines => WorkingPreview.TopKeepWindow(TopCutLine, TopContextCount);


    //----------- FUNDO -----------

    /// <summary>
    /// linha de corte do rodapé (1-based)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLines))]
    [NotifyPropertyChangedFor(nameof(SelectedLastLines))]
    private int bottomCutLine;

    /// <summary>
    /// qtas linhas exibir antes ou depois
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLines))]
    [NotifyPropertyChangedFor(nameof(SelectedLastLines))]
    private int bottomContextCount;

    /// <summary>
    /// bloco que será selecionado (antes do separador), recortado
    /// </summary>
    public string SelectedLastLines => WorkingPreview.BottomKeepWindow(BottomCutLine, BottomContextCount);

    /// <summary>
    /// bloco que será removido (após o separador)
    /// </summary>
    public string LastLines => WorkingPreview.BottomContextBlock(BottomCutLine, BottomContextCount);


    // se recarregar Model.Preview externamente
    public void RaiseAll()
    {
        OnPropertyChanged(nameof(FirstLines));
        OnPropertyChanged(nameof(SelectedFirstLines));
        OnPropertyChanged(nameof(SelectedLastLines));
        OnPropertyChanged(nameof(LastLines));
    }

    /// <summary>
    /// Temporarily remove a range of samples from the working preview.
    /// startIdx/endIdx are 0-based inclusive indices of samples to remove.
    /// </summary>
    public void CutRange(int startIdx, int endIdx)
    {
        if (startIdx < 0) startIdx = 0;
        var lines = WorkingPreview.Split(new[] { "\r\n" }, StringSplitOptions.None).ToList();
        if (lines.Count == 0) return;
        if (endIdx < startIdx) return;
        if (startIdx >= lines.Count) return;
        endIdx = Math.Min(endIdx, lines.Count - 1);

        // record removed range relative to current working preview
        RemovedRanges.Add((startIdx, endIdx));

        // remove range
        lines.RemoveRange(startIdx, endIdx - startIdx + 1);

        // update working preview
        WorkingPreview = string.Join("\r\n", lines);

        // rebuild measurements
        RebuildMeasurementsFromPreview();

        // update bottom cut default to new length if needed
        BottomCutLine = Math.Max(1, WorkingPreview.Lines().Length + 1);

        // raise previews
        RaiseAll();
    }

    /// <summary>
    /// Reset any cuts and restore original preview
    /// </summary>
    public void ResetCuts()
    {
        RemovedRanges.Clear();
        WorkingPreview = _originalPreview;
        RebuildMeasurementsFromPreview();
        BottomCutLine = Math.Max(1, WorkingPreview.Lines().Length + 1);
        RaiseAll();
    }

    // Return index range (inclusive) of lines whose timestamps lie within [start,end].
    // If no intersection, returns (0,-1).
    public (int Start, int End) GetIndexRangeForTimestampRange(DateTime start, DateTime end)
    {
        if (Timestamps == null || Timestamps.Count == 0) return (0, -1);
        int s = -1, e = -1;
        for (int i = 0; i < Timestamps.Count; i++)
        {
            if (s == -1 && Timestamps[i] >= start) s = i;
            if (Timestamps[i] <= end) e = i;
            if (Timestamps[i] > end) break;
        }
        if (s == -1 || e == -1 || e < s) return (0, -1);
        return (s, e);
    }
}