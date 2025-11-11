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

    // Exporta apenas a seleção atual (header + linhas entre startIndex e endIndex inclusive)
    Task<bool> ExportSelectionAsync(
        ImportedFileViewModel file,
        int startIndex,
        int endIndex,
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

    public async Task<bool> ExportSelectionAsync(ImportedFileViewModel file, int startIndex, int endIndex, Window? owner = null, CancellationToken ct = default)
    {
        if (file is null) return false;

        var targetDir = PickFolder(owner);
        if (string.IsNullOrWhiteSpace(targetDir)) return false;

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            ExportOneSelection(file, startIndex, endIndex, targetDir!, ct);
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

    private static void ExportOneSelection(ImportedFileViewModel vm, int startIndex, int endIndex, string targetDir, CancellationToken ct)
    {
        // Sanitiza e monta nome de saída, adicionando sufixo _selection
        var outName = SanitizeFileName(vm.Model.FileName);
        var nameWithoutExt = string.IsNullOrEmpty(outName) ? "arquivo" : Path.GetFileNameWithoutExtension(outName);
        var ext = Path.GetExtension(outName);
        var outFileName = string.Concat(nameWithoutExt, "_selection", string.IsNullOrEmpty(ext) ? ".txt" : ext);
        var outPath = Path.Combine(targetDir, outFileName);

        // prepara linhas da working preview
        var allLines = (vm.WorkingPreview ?? string.Empty).Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (allLines.Length == 0)
        {
            // nada a exportar, mas ainda escrevemos apenas o header
            using var writerEmpty = new StreamWriter(outPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!string.IsNullOrEmpty(vm.Model.FileHeader))
            {
                using var readerHeader = new StringReader(vm.Model.FileHeader);
                string? l;
                while ((l = readerHeader.ReadLine()) is not null)
                {
                    ct.ThrowIfCancellationRequested();
                    writerEmpty.WriteLine(l);
                }
            }
            return;
        }

        // clamp indices
        if (startIndex < 0) startIndex = 0;
        if (endIndex < 0) endIndex = 0;
        if (startIndex >= allLines.Length) startIndex = allLines.Length - 1;
        if (endIndex >= allLines.Length) endIndex = allLines.Length - 1;
        if (endIndex < startIndex) (startIndex, endIndex) = (endIndex, startIndex);

        using var writer = new StreamWriter(outPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // escreve header
        if (!string.IsNullOrEmpty(vm.Model.FileHeader))
        {
            using var readerHeader = new StringReader(vm.Model.FileHeader);
            string? l;
            while ((l = readerHeader.ReadLine()) is not null)
            {
                ct.ThrowIfCancellationRequested();
                writer.WriteLine(l);
            }
        }

        // escreve seleção
        for (int i = startIndex; i <= endIndex && i < allLines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            writer.WriteLine(allLines[i]);
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
