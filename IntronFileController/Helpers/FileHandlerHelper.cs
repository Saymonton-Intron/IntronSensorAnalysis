using IntronFileController.Models;
using IntronFileController.ViewModels;
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
        ObservableCollection<ImportedFileViewModel> ImportedFiles { get; }
        void AddFile(ImportedFileViewModel file);
        void AddFiles(IEnumerable<ImportedFileViewModel> files);
    }

    public class FileHandlerHelper : IFileHandlerHelper
    {
        public ObservableCollection<ImportedFileViewModel> ImportedFiles { get; } = [];

        public FileHandlerHelper()
        {

        }
        public void AddFile(ImportedFileViewModel file)
        {
            if (!ImportedFiles.Any(x => x.Model.FilePath.Equals(file.Model.FilePath, System.StringComparison.OrdinalIgnoreCase)))
                ImportedFiles.Add(file);
        }

        public void AddFiles(IEnumerable<ImportedFileViewModel> files)
        {
            // Evitar arquivos duplicados
            foreach (var file in files)
            {
                if (!ImportedFiles.Any(x => x.Model.FilePath.Equals(file.Model.FilePath, System.StringComparison.OrdinalIgnoreCase)))
                    AddFile(file);
            }
        }
    }
}
