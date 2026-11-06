// Только для теста жизненного цикла снимка. Не имитирует выполнение Revit API или транзакций.
using System;

namespace Autodesk.Revit.DB
{
    public sealed class Document { public bool IsValidObject { get; set; } = true; }
}
namespace Autodesk.Revit.DB.Events
{
    public sealed class DocumentChangedEventArgs : EventArgs
    {
        private readonly Document _document;
        public DocumentChangedEventArgs(Document document) => _document = document;
        public Document GetDocument() => _document;
    }
}
namespace Autodesk.Revit.ApplicationServices
{
    public sealed class Application
    {
        public event EventHandler<DB.Events.DocumentChangedEventArgs>? DocumentChanged;
        public void Change(DB.Document document) =>
            DocumentChanged?.Invoke(this, new DB.Events.DocumentChangedEventArgs(document));
    }
}
