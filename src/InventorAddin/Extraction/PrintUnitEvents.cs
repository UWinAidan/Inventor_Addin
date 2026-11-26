using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Refreshes a print's FINISH and WEIGHT when the drawing is opened (after) and before it is saved (task 244, spec 05 question 33), so
    /// they follow the part's finish and mass without Create Print. The work is <see cref="PrintUnitRefresher"/>'s; it changes nothing on a
    /// print that is already right, and leaves every drawing without the AWB fields alone.
    /// </summary>
    /// <remarks>
    /// Nothing here throws into Inventor and an open or a save is never stopped: every failure is logged once per document and message. The
    /// writes are made outside a transaction, as <see cref="MassOnSave"/> does, so they add no undo step.
    /// </remarks>
    public sealed class PrintUnitEvents : IDisposable
    {
        private readonly Application _app;
        private readonly ILog _log;
        private readonly HashSet<string> _failuresLogged = new HashSet<string>(StringComparer.Ordinal);

        // Kept referenced, or the events stop firing.
        private ApplicationEvents? _events;
        private bool _running;

        public PrintUnitEvents(Application app, ILog log)
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
                _events.OnOpenDocument += OnOpenDocument;
                _events.OnSaveDocument += OnSaveDocument;
            }
            catch (Exception ex)
            {
                _log.Error("Print units: watching for opens and saves failed; FINISH and WEIGHT are not refreshed.", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_events != null)
                {
                    _events.OnOpenDocument -= OnOpenDocument;
                    _events.OnSaveDocument -= OnSaveDocument;
                    _events = null;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Print units: stopping failed.", ex);
            }
        }

        private void OnOpenDocument(_Document documentObject, string fullDocumentName, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter == EventTimingEnum.kAfter)
                Refresh(documentObject, "opening");
        }

        private void OnSaveDocument(_Document documentObject, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter == EventTimingEnum.kBefore)
                Refresh(documentObject, "saving");
        }

        private void Refresh(_Document documentObject, string when)
        {
            if (_running)
                return;

            string name = "(unknown)";
            _running = true;
            try
            {
                var doc = (Document)documentObject;
                if (ComSafe.Get(() => DocumentKinds.Of(doc)) != DocumentKind.Drawing)
                    return;

                // A document opened without a window (Mass Update's scan, Rename's opens) is never refreshed or edited, and nothing is
                // logged for it. A document has no Visible property; one with no views is not shown (probe, task 244).
                if (ComSafe.Get(() => doc.Views.Count) is not > 0)
                    return;

                name = ComSafe.Get(() => doc.DisplayName) ?? name;
                PrintUnitRefresh result = PrintUnitRefresher.Refresh((DrawingDocument)doc, null, null);
                if (!result.Applicable)
                    return;

                if (result.Changed)
                {
                    _log.Info(
                        $"Print units: on {when} '{name}' (print in {result.PrintUnits}, model in {result.ModelUnits}) wrote {result.Written.Count} drawing propert(ies) and repointed {result.FieldsChanged} title block field(s)." +
                        (result.Warnings.Count == 0 ? string.Empty : " Warnings: " + string.Join(" ", result.Warnings)));
                }
                else if (result.Warnings.Count > 0)
                {
                    LogOnce(name, $"Print units: refreshing FINISH and WEIGHT of '{name}' on {when} had problems. " + string.Join(" ", result.Warnings));
                }
            }
            catch (Exception ex)
            {
                LogOnce(name, $"Print units: could not refresh FINISH and WEIGHT of '{name}' on {when}. It went ahead. {ex.Message}");
            }
            finally
            {
                _running = false;
            }
        }

        private void LogOnce(string name, string message)
        {
            if (_failuresLogged.Add(name + "|" + message))
                _log.Warn(message);
        }
    }
}
