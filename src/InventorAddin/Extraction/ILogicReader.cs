using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Access to iLogic rules through the iLogic add-in's automation object.
    /// Late-bound (dynamic) so we don't need to reference Autodesk.iLogic.Interfaces.
    /// </summary>
    public static class ILogicReader
    {
        public const string ILogicAddInId = "{3BDD8D79-2179-4B11-8A5A-257B1C0263AC}";

        public static dynamic? GetAutomation(Application app)
        {
            ApplicationAddIn? addIn = ComSafe.Get(() => app.ApplicationAddIns.ItemById[ILogicAddInId]);
            if (addIn == null)
                return null;
            if (!addIn.Activated)
                addIn.Activate();
            return addIn.Automation;
        }

        public static List<ILogicRuleData> Read(Application app, Document doc, List<string> warnings)
        {
            var list = new List<ILogicRuleData>();
            ComSafe.Run(() =>
            {
                dynamic? auto = GetAutomation(app);
                if (auto == null)
                    return;
                dynamic? rules = auto.Rules(doc);
                if (rules == null)
                    return;
                foreach (dynamic rule in rules)
                {
                    list.Add(new ILogicRuleData
                    {
                        Name = (string)rule.Name,
                        IsActive = (bool)rule.IsActive,
                        Text = (string)rule.Text,
                    });
                }
            }, warnings, "iLogic rules");
            return list;
        }

        /// <summary>Adds or replaces a rule in the document. Used by the iLogic injection tool.</summary>
        public static void AddOrReplaceRule(Application app, Document doc, string ruleName, string ruleText)
        {
            dynamic auto = GetAutomation(app) ?? throw new System.InvalidOperationException("iLogic add-in not available.");
            dynamic? existing = null;
            try { existing = auto.GetRule(doc, ruleName); } catch { }

            if (existing != null)
                existing.Text = ruleText;
            else
                auto.AddRule(doc, ruleName, ruleText);
        }
    }
}
