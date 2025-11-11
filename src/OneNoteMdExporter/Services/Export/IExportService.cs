using alxnbl.OneNoteMdExporter.Models;
using System;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public interface IExportService
    {
        string ExportFormatCode { get; }
        NotebookExportResult ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "", DateTime? modifiedSince = null, string exportRoot = null, bool preserveExisting = false);
    }
}
