using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// The dropdown our windows use (task 169). It is a <see cref="ComboBox"/> whose automation peer says it has no
    /// Scroll pattern. While the add-in waits on an Inventor COM call, COM pumps window messages on the UI thread and
    /// a UI Automation client can ask the dropdown for its Scroll pattern. WPF's <see cref="ComboBoxAutomationPeer"/>
    /// answers with itself while the dropdown is closed, which is not an <c>IScrollProvider</c>, so WPF's own cast in
    /// <c>ScrollProviderWrapper.Wrap</c> fails, and the exception unwinds through the COM call into whatever Inventor
    /// read was running. Answering "no Scroll pattern" (null) while closed is valid and removes that path; the look is the shared ComboBox style, applied to this type in <c>Styles.xaml</c>.
    /// </summary>
    public class AwbComboBox : ComboBox
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new AwbComboBoxAutomationPeer(this);

        private sealed class AwbComboBoxAutomationPeer : ComboBoxAutomationPeer
        {
            public AwbComboBoxAutomationPeer(AwbComboBox owner) : base(owner) { }

            // Closed, WPF answers Scroll with the peer itself, which is not an IScrollProvider; open, it gives the
            // popup's real ScrollViewer provider, which is kept.
            public override object? GetPattern(PatternInterface patternInterface)
                => patternInterface == PatternInterface.Scroll && !((ComboBox)Owner).IsDropDownOpen
                    ? null
                    : base.GetPattern(patternInterface);
        }
    }
}
