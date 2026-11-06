using System;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Защищает расчёт от смены документа и любых последующих транзакций, включая Undo/Redo.</summary>
    internal sealed class CalculationSnapshot : IDisposable
    {
        private readonly Application _application;
        private bool _disposed;
        public Document Document { get; }
        public bool IsStale { get; private set; }
        public event Action? Invalidated;

        public CalculationSnapshot(Application application, Document document)
        {
            _application = application;
            Document = document;
            _application.DocumentChanged += OnDocumentChanged;
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            if (e.GetDocument().Equals(Document))
            {
                IsStale = true;
                Invalidated?.Invoke();
            }
        }

        public void EnsureCurrent(Document activeDocument)
        {
            if (_disposed || !Document.IsValidObject || !Document.Equals(activeDocument))
                throw new InvalidOperationException("Активен другой документ или исходная модель закрыта. Выполните расчёт заново.");
            if (IsStale)
                throw new InvalidOperationException("Модель изменилась после расчёта. Выполните расчёт заново.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _application.DocumentChanged -= OnDocumentChanged;
            _disposed = true;
        }
    }
}
