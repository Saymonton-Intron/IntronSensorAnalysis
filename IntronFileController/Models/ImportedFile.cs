using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntronFileController.Common;

namespace IntronFileController.Models
{
    public class ImportedFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; } = default;
        public string Preview { get; set; } = string.Empty; // conteúdo carregado sem o header
        public string FileHeader { get; set; } = string.Empty; // Apenas o header
        public DateTime ImportedAt { get; private set; } = DateTime.UtcNow;
    }

}
