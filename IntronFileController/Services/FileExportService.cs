using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using IntronFileController.ViewModels;
using Ookii.Dialogs.Wpf;

namespace IntronFileController.Services;
public interface IFileExportService
{
    Task<bool> ExportAsync(
        ReadOnlyObservableCollection<ImportedFileViewModel> files,
        Window? owner = null,
        CancellationToken ct = default);
}
public sealed class FileExportService : IFileExportService
{
    public async Task<bool> ExportAsync(
        ReadOnlyObservableCollection<ImportedFileViewModel> files,
        Window? owner = null,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0) return false;

        var targetDir = PickFolder(owner);
        if (string.IsNullOrWhiteSpace(targetDir)) return false;

        // roda em background sem travar a UI
        await Task.Run(() =>
        {
            foreach (var vm in files)
            {
                ct.ThrowIfCancellationRequested();
                ExportOne(vm, targetDir!, ct);
            }
        }, ct).ConfigureAwait(false);

        return true;
    }

    private static string? PickFolder(Window? owner)
    {
        var dlg = new VistaFolderBrowserDialog
        {
            Description = "Escolha a pasta de destino para salvar os arquivos recortados",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            Multiselect = false
        };

        bool? result = (owner is null)
            ? dlg.ShowDialog()
            : dlg.ShowDialog(owner);

        return result == true ? dlg.SelectedPath : null;
    }


    // Classe auxiliar pra passar o handle do WPF pro dialog
    private sealed class WindowWrapper : System.Windows.Forms.IWin32Window
    {
        public WindowWrapper(nint handle) { Handle = (IntPtr)handle; }
        public IntPtr Handle { get; }
    }

    private static void ExportOne(ImportedFileViewModel vm, string targetDir, CancellationToken ct)
    {
        // Caminho de saída (mesmo nome do arquivo original, saneado)
        var outName = SanitizeFileName(vm.Model.FileName);
        var outPath = Path.Combine(targetDir, string.IsNullOrWhiteSpace(outName) ? "arquivo.txt" : outName);

        // STREAMING: copia o header + working preview inteiro (o WorkingPreview já reflete os cortes aplicados)
        using var reader = new StringReader(string.Concat(vm.Model.FileHeader ?? string.Empty, vm.WorkingPreview ?? string.Empty));
        using var writer = new StreamWriter(outPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            writer.Write(line);
            writer.Write(Environment.NewLine);
        }
    }

    private static string SanitizeFileName(string? name)
    {
        name ??= string.Empty;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
