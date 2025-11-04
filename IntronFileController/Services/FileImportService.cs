using IntronFileController.Models;
using IntronFileController.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntronFileController.Services
{
    public interface IFileImportService
    {
        Task<IEnumerable<ImportedFileViewModel>> ImportTextFilesAsync(IEnumerable<string> paths);
        string[] OpenDialogSelectTextFiles();
    }

    public class FileImportService : IFileImportService
    {
        // previewMaxChars evita carregar arquivos gigantes no preview (ajuste conforme necessário)
        public async Task<IEnumerable<ImportedFileViewModel>> ImportTextFilesAsync(IEnumerable<string> paths)
        {
            var list = new List<ImportedFileViewModel>();

            foreach (var p in paths)
            {
                try
                {
                    var fi = new FileInfo(p);
                    if (!fi.Exists) continue;

                    // Abrir de forma assíncrona e com hint de leitura sequencial
                    using var stream = new FileStream(
                        p, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 81920,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);

                    // Detecta BOM automaticamente
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                    // LÊ O ARQUIVO TODO
                    string content = await reader.ReadToEndAsync();

                    list.Add(new(new ImportedFile
                    {
                        FileName = fi.Name,
                        FilePath = fi.FullName,
                        SizeBytes = fi.Length,
                        Preview = content  // agora Preview contém o conteúdo completo
                    }));
                }
                catch
                {
                    // registre/logue se quiser; aqui ignoramos o arquivo problemático
                }
            }

            return list;
        }
        public static async Task<string> ReadLinesRangeAsync(
            string path,
            int startLine,
            int endLine,
            CancellationToken ct = default)
        {
            if (startLine < 1)
                throw new ArgumentOutOfRangeException(nameof(startLine), "startLine deve ser >= 1");
            if (endLine < startLine)
                throw new ArgumentOutOfRangeException(nameof(endLine), "endLine deve ser >= startLine");

            var sb = new StringBuilder();
            int currentLine = 0;

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var reader = new StreamReader(
                stream,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 81920);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                currentLine++;

                if (currentLine < startLine)
                    continue; // ainda não chegou na linha inicial

                if (currentLine > endLine)
                    break; // já passou da linha final

                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public string[] OpenDialogSelectTextFiles()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Selecione um ou mais arquivos .txt"
            };

            bool? result = dlg.ShowDialog();
            if (result != true) return [];

            var selectedPaths = dlg.FileNames;
            if (selectedPaths == null || selectedPaths.Length == 0) return [];

            // opcional: filtrar por tamanho, extensão, etc.
            return [.. selectedPaths.Where(p => p.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))];
        }
    }
}
