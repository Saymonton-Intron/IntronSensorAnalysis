using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntronFileController.Models
{
    public class ImportedFile
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long SizeBytes { get; set; }
        public string Preview { get; set; } = string.Empty; // conteúdo carregado (ou parcial)
        public DateTime ImportedAt { get; private set; } = DateTime.UtcNow;
    }

}
