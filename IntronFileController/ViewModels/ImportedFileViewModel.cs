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
    public ImportedFileViewModel(ImportedFile model)
    {
        Model = model;

        // defaults “de fábrica”
        TopCutLine = 0;          // linha alvo topo (1-based)
        TopContextCount = 20;       // qtas linhas pegar (antes e depois)
        BottomCutLine = Model.Preview.Lines().Length;       // linha alvo fundo (1-based)
        BottomContextCount = 20;

        // Preencher lista de leituras
        foreach (var line in Model.Preview.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split(';'); // 0 é o index

            ZMeasurements.Add(double.Parse(p[1]));
            XMeasurements.Add(double.Parse(p[2]));
            YMeasurements.Add(double.Parse(p[3]));
        }
    }

    //----------- TOPO -----------

    /// <summary>
    /// linha de corte do topo (1-based)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstLines))]
    [NotifyPropertyChangedFor(nameof(KeepFirstLines))]
    private int topCutLine;

    /// <summary>
    /// qtas linhas exibir antes ou depois
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstLines))]
    [NotifyPropertyChangedFor(nameof(KeepFirstLines))]
    private int topContextCount;

    /// <summary>
    /// bloco que será removido (antes do separador)
    /// </summary>
    public string FirstLines => Model.Preview.TopContextBlock(TopCutLine, TopContextCount);

    /// <summary>
    /// bloco que será mantido (após o separador), recortado
    /// </summary>
    public string KeepFirstLines => Model.Preview.TopKeepWindow(TopCutLine, TopContextCount);


    //----------- FUNDO -----------

    /// <summary>
    /// linha de corte do rodapé (1-based)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLines))]
    [NotifyPropertyChangedFor(nameof(KeepLastLines))]
    private int bottomCutLine;

    /// <summary>
    /// qtas linhas exibir antes ou depois
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLines))]
    [NotifyPropertyChangedFor(nameof(KeepLastLines))]
    private int bottomContextCount;

    /// <summary>
    /// bloco que será mantido (antes do separador), recortado
    /// </summary>
    public string KeepLastLines => Model.Preview.BottomKeepWindow(BottomCutLine, BottomContextCount);

    /// <summary>
    /// bloco que será removido (após o separador)
    /// </summary>
    public string LastLines => Model.Preview.BottomContextBlock(BottomCutLine, BottomContextCount);


    // se recarregar Model.Preview externamente
    public void RaiseAll()
    {
        OnPropertyChanged(nameof(FirstLines));
        OnPropertyChanged(nameof(KeepFirstLines));
        OnPropertyChanged(nameof(KeepLastLines));
        OnPropertyChanged(nameof(LastLines));
    }
}