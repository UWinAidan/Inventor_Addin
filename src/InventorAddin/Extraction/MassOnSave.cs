using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Keeps the custom "Mass" property of a part or assembly current by writing it just before the document is saved, so it is
    /// saved with the file (task 222). The print templates' WEIGHT field prints it.
    /// </summary>
    /// <remarks>
    /// Nothing here throws into Inventor and a save is never stopped: every failure is logged once per document and message. The write
    /// is made outside a transaction, inside the save, so it adds no undo step of its own.
    /// </remarks>
    public sealed class MassOnSave : IDisposable
    {
        private readonly Application _app;
        private readonly ILog _log;
        private readonly HashSet<string> _failuresLogged = new HashSet<string>(StringComparer.Ordinal);

        // Kept referenced, or the event stops firing.
        private ApplicationEvents? _events;
        private bool _running;

        public MassOnSave(Application app, ILog log)
        {
            _app = app;
            _log = log;
        }

        /// <summary>Call once, from <c>Activate</c>.</summary>
        public void Start()
        {
            try
            {
                _events = _app.ApplicationEvents;
                _events.OnSaveDocument += OnSaveDocument;
            }
            catch (Exception ex)
            {
                _log.Error("Mass property: watching for saves failed; Mass is not refreshed on save.", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_events != null)
                {
                    _events.OnSaveDocument -= OnSaveDocument;
                    _events = null;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Mass property: stopping failed.", ex);
            }
        }

        private void OnSaveDocument(_Document documentObject, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kBefore || _running)
                return;

            string name = "(unknown)";
            _running = true;
            try
            {
                var doc = (Document)documentObject;
                name = ComSafe.Get(() => doc.DisplayName) ?? name;
                DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));
                if (!kind.IsPart() && !kind.IsAssembly())
                    return;

                PropertyWrite? write = MassPropertyReader.WriteFor(doc);
                if (write != null)
                    PropertyWriter.Apply(doc, new[] { write });
            }
            catch (Exception ex)
            {
                if (_failuresLogged.Add(name + "|" + ex.Message))
                    _log.Warn($"Mass property: could not write Mass on save of '{name}'. The save went ahead. {ex.Message}");
            }
            finally
            {
                _running = false;
            }
        }
    }
}
