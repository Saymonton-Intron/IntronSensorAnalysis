using IntronFileController.Common;
using IntronFileController.Common.Extensions;
using IntronFileController.Models;
using IntronFileController.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
            var pathList = paths?.ToArray() ?? Array.Empty<string>();

            // show small loading window on UI thread
            IntronFileController.Views.LoadingWindow? loader = null;
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        loader = new IntronFileController.Views.LoadingWindow();
                        loader.Owner = app.MainWindow;
                        loader.Show();
                    });
                }

                for (int i = 0; i < pathList.Length; i++)
                {
                    var p = pathList[i];
                    try
                    {
                        if (loader != null)
                        {
                            var idx = i + 1;
                            var total = pathList.Length;
                            Application.Current?.Dispatcher.Invoke(() => loader.UpdateMessage($"Carregando arquivo {idx} de {total}..."));
                        }

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

                        // split lines using helper (maintains behavior elsewhere)
                        var allLines = content.Lines();
                        if (allLines.Length == 0) continue;

                        // find the index of the data header (e.g. line starting with "TimeStamp")
                        int dataHeaderIndex = Array.FindIndex(allLines, l => !string.IsNullOrWhiteSpace(l) && l.TrimStart().StartsWith("TimeStamp", StringComparison.OrdinalIgnoreCase));

                        if (dataHeaderIndex < 0)
                        {
                            // not a supported format
                            string userMsg = $"Arquivo rejeitado (formato inválido / cabeçalho não encontrado):\n{p}";
                            Trace.TraceWarning(userMsg);
                            ShowMessageBox(userMsg, "Arquivo inválido", MessageBoxImage.Warning);
                            continue;
                        }

                        // include the data header line as part of the file header so exports keep column names
                        var headerLines = allLines.Take(dataHeaderIndex + 1).ToArray();
                        var dataLines = allLines.Skip(dataHeaderIndex + 1).ToArray();

                        // parse header metadata
                        var parsed = ParseSensorHeader(headerLines);

                        // require minimum header fields
                        if (parsed == null || !parsed.IsValid)
                        {
                            string userMsg = $"Arquivo rejeitado (cabeçalho incompleto ou inválido):\n{p}";
                            Trace.TraceWarning(userMsg);
                            ShowMessageBox(userMsg, "Cabeçalho inválido", MessageBoxImage.Warning);
                            continue;
                        }

                        // raw data joined
                        var rawData = string.Join("\r\n", dataLines).TrimStart('\r', '\n');

                        // build timestamped preview from raw data using header info
                        var timestampedPreview = BuildTimestampedPreviewFromRaw(rawData, parsed);

                        // create model
                        list.Add(new(new ImportedFile
                        {
                            FileName = fi.Name,
                            FilePath = fi.FullName,
                            SizeBytes = fi.Length,
                            FileHeader = string.Join(Environment.NewLine, headerLines),
                            RawData = rawData,
                            Preview = timestampedPreview,  // agora Preview contém os dados com timestamps
                            ParsedHeader = parsed,
                            SensorType = parsed.DeviceName?.IndexOf("AX 3D", StringComparison.OrdinalIgnoreCase) >= 0 ? SensorType.AX3D : SensorType.Unknown
                        }));
                    }
                    catch (Exception ex)
                    {
                        // 1) Construir mensagem amigável para o usuário
                        string userMsg = $"Erro ao importar o arquivo:\n{p}\n\nMensagem: {ex.Message}";

                        // 2) Registrar para diagnóstico (stack trace etc.)
                        Trace.TraceError("Falha ao importar arquivo '{0}': {1}\n{2}", p, ex.Message, ex.StackTrace);

                        // 3) Exibir MessageBox na thread da UI, se possível
                        try
                        {
                            if (app?.Dispatcher != null)
                            {
                                app.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    MessageBox.Show(userMsg, "Erro ao importar arquivo", MessageBoxButton.OK, MessageBoxImage.Error);
                                }));
                            }
                            else
                            {
                                // Fallback: tentar exibir diretamente (pode falhar se chamado fora da UI thread)
                                MessageBox.Show(userMsg, "Erro ao importar arquivo", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch
                        {
                            // Se nem a MessageBox for possível, apenas garantir que não parem o fluxo.
                            // Já registramos o erro acima.
                        }

                        // continuar com o próximo arquivo
                    }
                }
            }
            finally
            {
                if (loader != null)
                {
                    Application.Current?.Dispatcher.Invoke(() => loader.Close());
                }
            }

            return list;
        }

        private static string BuildTimestampedPreviewFromRaw(string rawData, SensorFileHeader header)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return string.Empty;
            if (header == null) return rawData;

            var lines = rawData.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

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
                string ts = !string.IsNullOrEmpty(header.DateFormat) ? timestamp.ToString(header.DateFormat, System.Globalization.CultureInfo.InvariantCulture) : timestamp.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

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

        private static SensorFileHeader? ParseSensorHeader(string[] headerLines)
        {
            if (headerLines == null || headerLines.Length == 0) return null;

            var hdr = new SensorFileHeader();
            foreach (var raw in headerLines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var line = raw.Trim();

                // Some lines are separators (dashes) or column header; skip separators
                if (line.All(c => c == '-' || c == ' ' || c == '\t')) continue;

                // If this is the column names line (starts with TimeStamp), skip parsing but keep as part of header text
                if (line.StartsWith("TimeStamp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // try split by ':' first
                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    if (string.Equals(key, "BeanDevice", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Device", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr.DeviceName = val;
                    }
                    else if (key.IndexOf("Range", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // try to parse two numbers from range like "-2g / +2g" or "-2 / 2"
                        var matches = Regex.Matches(val, "[-+]?[0-9]*\\,?[0-9]+");
                        if (matches.Count >= 2)
                        {
                            if (double.TryParse(matches[0].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rmin)) hdr.RangeMin = rmin;
                            if (double.TryParse(matches[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rmax)) hdr.RangeMax = rmax;
                        }
                        else if (matches.Count == 1)
                        {
                            if (double.TryParse(matches[0].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r)) { hdr.RangeMin = -Math.Abs(r); hdr.RangeMax = Math.Abs(r); }
                        }
                    }
                    else if (string.Equals(key, "Mac Id", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "MacId", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Mac Id ", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr.MacId = val;
                    }
                    else if (string.Equals(key, "Network Id", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "NetworkId", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr.NetworkId = val;
                    }
                    else if (string.Equals(key, "Pan Id", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "PanId", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr.PanId = val;
                    }
                    else if (key.IndexOf("Measure mode", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("Measure mode", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hdr.MeasureMode = val;
                    }
                    else if (key.IndexOf("Streaming Options", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hdr.StreamingOptions = val;
                    }
                    else if (key.IndexOf("Unit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hdr.Unit = val;
                    }
                    else if (string.Equals(key, "DATE_FORMAT", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr.DateFormat = val;
                    }
                    else if (string.Equals(key, "Date", StringComparison.OrdinalIgnoreCase))
                    {
                        // try parse using provided format if present
                        if (!string.IsNullOrEmpty(hdr.DateFormat))
                        {
                            try
                            {
                                hdr.Date = DateTime.ParseExact(val, hdr.DateFormat, System.Globalization.CultureInfo.InvariantCulture);
                            }
                            catch
                            {
                                DateTime.TryParse(val, out var dt);
                                hdr.Date = dt;
                            }
                        }
                        else
                        {
                            DateTime.TryParse(val, out var dt);
                            hdr.Date = dt;
                        }
                    }
                    else if (string.Equals(key, "Sampling rate", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Sampling Rate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out var sr)) hdr.SamplingRate = sr;
                    }
                    else if (string.Equals(key, "Sensor Ids", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Sensor Id", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = val.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
                        var ids = new List<int>();
                        foreach (var p in parts)
                        {
                            if (int.TryParse(p, out var id)) ids.Add(id);
                        }
                        hdr.SensorIds = ids.ToArray();
                    }
                    else if (string.Equals(key, "Sensor Labels", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "SensorLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = val.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
                        hdr.SensorLabels = parts;
                    }
                }
                else
                {
                    // Some lines may use different separators or present key/value with no colon; try simple heuristics
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();
                        // fallback to assign to DeviceName if looks like a device line
                        if (key.IndexOf("BeanDevice", StringComparison.OrdinalIgnoreCase) >= 0)
                            hdr.DeviceName = val;
                    }
                }
            }

            return hdr;
        }

        private static void ShowMessageBox(string text, string title, MessageBoxImage icon)
        {
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(text, title, MessageBoxButton.OK, icon);
                    }));
                }
                else
                {
                    MessageBox.Show(text, title, MessageBoxButton.OK, icon);
                }
            }
            catch
            {
                // swallow
            }
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
            if (result != true) return Array.Empty<string>();

            var selectedPaths = dlg.FileNames;
            if (selectedPaths == null || selectedPaths.Length == 0) return Array.Empty<string>();

            // opcional: filtrar por tamanho, extensão, etc.
            return selectedPaths.Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }
}
