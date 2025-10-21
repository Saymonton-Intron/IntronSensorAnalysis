using IntronFileController.Models;
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
        Task<IEnumerable<ImportedFile>> ImportTextFilesAsync(IEnumerable<string> paths, int previewMaxChars = 10000);
        string[] OpenDialogSelectTextFiles();
    }

    public class FileImportService : IFileImportService
    {
        // previewMaxChars evita carregar arquivos gigantes no preview (ajuste conforme necessário)
        public async Task<IEnumerable<ImportedFile>> ImportTextFilesAsync(IEnumerable<string> paths, int previewMaxChars = 10000)
        {
            var list = new List<ImportedFile>();

            foreach (var p in paths)
            {
                try
                {
                    var fi = new FileInfo(p);
                    if (!fi.Exists) continue;

                    // limitada leitura para preview (evita estourar memória)
                    using var stream = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                    // lê somente até previewMaxChars (mas podemos mudar para ReadToEndAsync se quiser carregar tudo)
                    var buffer = new char[4096];
                    var sb = new System.Text.StringBuilder();
                    int totalRead = 0;
                    while (!reader.EndOfStream && totalRead < previewMaxChars)
                    {
                        int toRead = Math.Min(buffer.Length, previewMaxChars - totalRead);
                        int read = await reader.ReadAsync(buffer, 0, toRead);
                        if (read <= 0) break;
                        sb.Append(buffer, 0, read);
                        totalRead += read;
                    }

                    var preview = sb.ToString();
                    if (!reader.EndOfStream)
                        preview += "\n\n--- PREVIEW TRUNCADO ---";

                    list.Add(new ImportedFile
                    {
                        FileName = fi.Name,
                        FilePath = fi.FullName,
                        SizeBytes = fi.Length,
                        Preview = preview
                    });
                }
                catch
                {
                    // se preferir, registre/logue a falha; aqui só ignoramos arquivos problemáticos
                }
            }

            return list;
        }

        public string[] OpenDialogSelectTextFiles()
        {
            var dlg = new OpenFileDialog
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
            return selectedPaths.Where(p => p.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }
}
