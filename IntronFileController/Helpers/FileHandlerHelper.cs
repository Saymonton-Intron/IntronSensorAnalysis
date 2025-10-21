using IntronFileController.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntronFileController.Helpers
{
    public interface IFileHandlerHelper
    {
        ObservableCollection<ImportedFile> ImportedFiles { get; }
        void AddFile(ImportedFile file);
        void AddFiles(IEnumerable<ImportedFile> files);
    }

    public class FileHandlerHelper : IFileHandlerHelper
    {
        public ObservableCollection<ImportedFile> ImportedFiles { get; } = [];

        public FileHandlerHelper()
        {

        }
        public void AddFile(ImportedFile file)
        {
            if (!ImportedFiles.Any(x => x.FilePath.Equals(file.FilePath, System.StringComparison.OrdinalIgnoreCase)))
                ImportedFiles.Add(file);
        }

        public void AddFiles(IEnumerable<ImportedFile> files)
        {
            // Evitar arquivos duplicados
            foreach (var file in files)
            {
                if (!ImportedFiles.Any(x => x.FilePath.Equals(file.FilePath, System.StringComparison.OrdinalIgnoreCase)))
                    AddFile(file);
            }
        }
    }
}
