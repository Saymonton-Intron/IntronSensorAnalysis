using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntronFileController.Common;

namespace IntronFileController.Models
{
    public enum SensorType
    {
        Unknown = 0,
        AX3D = 1,
        // other types can be added later
    }

    public class SensorFileHeader
    {
        public string DeviceName { get; set; } = string.Empty;
        // Range parsed into numeric min/max for easier processing
        public double RangeMin { get; set; } = double.NaN;
        public double RangeMax { get; set; } = double.NaN;
        public string MacId { get; set; } = string.Empty;
        public string NetworkId { get; set; } = string.Empty;
        public string PanId { get; set; } = string.Empty;
        public string MeasureMode { get; set; } = string.Empty;
        public string StreamingOptions { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string DateFormat { get; set; } = string.Empty;
        public DateTime Date { get; set; } = default;
        public int SamplingRate { get; set; } = 0;
        public int[] SensorIds { get; set; } = Array.Empty<int>();
        public string[] SensorLabels { get; set; } = Array.Empty<string>();

        public bool IsValid => Date != default && SamplingRate > 0 && !string.IsNullOrEmpty(DeviceName);
    }

    public class ImportedFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; } = default;
        // Preview stores timestamped preview lines (first column = datetime)
        public string Preview { get; set; } = string.Empty; // conteúdo carregado sem o header (já com timestamps)
        // RawData stores original data lines (first column sample index)
        public string RawData { get; set; } = string.Empty;
        public string FileHeader { get; set; } = string.Empty; // Apenas o header
        public DateTime ImportedAt { get; private set; } = DateTime.UtcNow;

        // New: parsed header metadata and sensor type
        public SensorFileHeader ParsedHeader { get; set; } = new SensorFileHeader();
        public SensorType SensorType { get; set; } = SensorType.Unknown;

        // Convenience
        public bool HasValidHeader => ParsedHeader?.IsValid ?? false;
    }

}
