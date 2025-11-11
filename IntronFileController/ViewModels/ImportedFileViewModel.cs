using CommunityToolkit.Mvvm.ComponentModel;
using IntronFileController.Common.Extensions;
using IntronFileController.Models;
using System.Collections.ObjectModel;

namespace IntronFileController.ViewModels;

public partial class ImportedFileViewModel : ObservableObject
{
    public ImportedFile Model { get; }
    [ObservableProperty] private ObservableCollection<double> zMeasurements = [];
    [ObservableProperty] private ObservableCollection<double> xMeasurements = [];
    [ObservableProperty] private ObservableCollection<double> yMeasurements = [];

    // Keep original preview and a working copy where temporary cuts are applied
    private readonly string _originalPreview;
    private string _workingPreview;

    // list of removed ranges (start inclusive, end inclusive) for potential future use
    public List<(int Start, int End)> RemovedRanges { get; } = new();

    public ImportedFileViewModel(ImportedFile model)
    {
        Model = model;

        _originalPreview = model.Preview ?? string.Empty;
        _workingPreview = _originalPreview;

        // defaults “de fábrica”
        TopCutLine = 0;          // linha alvo topo (1-based)
        TopContextCount = 20;       // qtas linhas pegar (antes e depois)
        // set BottomCutLine to total+1 so that by default nothing is cut at bottom
        BottomCutLine = WorkingPreview.Lines().Length + 1;       // linha alvo fundo (1-based)
        BottomContextCount = 20;

        // Preencher lista de leituras a partir do working preview
        RebuildMeasurementsFromPreview();
    }

    private void RebuildMeasurementsFromPreview()
    {
        ZMeasurements.Clear();
        XMeasurements.Clear();
        YMeasurements.Clear();

        foreach (var line in WorkingPreview.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split(';'); // 0 é o index
            if (p.Length < 4) continue;
            if (double.TryParse(p[1], out var z)) ZMeasurements.Add(z);
            if (double.TryParse(p[2], out var x)) XMeasurements.Add(x);
            if (double.TryParse(p[3], out var y)) YMeasurements.Add(y);
        }

        // notify plot/viewmodel that counts changed if needed
        OnPropertyChanged(nameof(ZMeasurements));
        OnPropertyChanged(nameof(XMeasurements));
        OnPropertyChanged(nameof(YMeasurements));
    }

    // Expose working preview for other components if necessary
    public string WorkingPreview
    {
        get => _workingPreview;
        private set
        {
            if (_workingPreview == value) return;
            _workingPreview = value;
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
}