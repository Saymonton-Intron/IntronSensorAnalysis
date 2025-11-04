using CommunityToolkit.Mvvm.ComponentModel;
using IntronFileController.Common.Extensions;
using IntronFileController.Models;

namespace IntronFileController.ViewModels;

public partial class ImportedFileViewModel : ObservableObject
{
    public ImportedFile Model { get; }

    public ImportedFileViewModel(ImportedFile model)
    {
        Model = model;

        // defaults “de fábrica”
        TopCutLine = 0;          // linha alvo topo (1-based)
        TopContextCount = 20;       // qtas linhas pegar (antes e depois)
        BottomCutLine = Model.Preview.Lines().Length;       // linha alvo fundo (1-based)
        BottomContextCount = 20;
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