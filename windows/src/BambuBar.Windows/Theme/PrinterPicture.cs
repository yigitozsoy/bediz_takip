using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BambuBar.Core.Imaging;
using BambuBar.Core.Model;

namespace BambuBar.Theme;

/// <summary>
/// The printer on the job card: the model's badge under the Gamma theme, Core's drawing in the classic look.
/// beta dropped Bambu Bar's live 3D model (HelixToolkit over Direct3D 11): it cost about 30 MB of libraries and a
/// graphics device for a picture the Gamma theme doesn't show.
/// </summary>
public sealed class PrinterPicture : Grid
{
    private readonly Image _art;
    private PrinterArt.Scene? _state;

    public PrinterPicture(double width, double height)
    {
        Width = width;
        Height = height;
        _art = ArtImage.Element(width, height);
        Children.Add(_art);
        Focusable = false;
        AutomationProperties.SetName(this, Loc.T("Printer model"));
    }

    /// <summary>Shows <paramref name="scene"/>: the printer's shape or badge, its state, filaments and light.</summary>
    public void Show(PrinterArt.Scene scene)
    {
        _state = scene;
        ArtImage.Show(_art, scene);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PicturePeer(this);

    /// <summary>"Printer model", valued "Printing 64 percent" (the Mac's VoiceOver label and value).</summary>
    private sealed class PicturePeer(PrinterPicture view) : FrameworkElementAutomationPeer(view), IValueProvider
    {
        protected override string GetClassNameCore() => nameof(PrinterPicture);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
        protected override string GetNameCore() => Loc.T("Printer model");

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => true;

        public string Value => ((PrinterPicture)Owner)._state is { } s
            ? s.State.IsActive() ? Loc.F("%@ %lld percent", s.State.Title(), (long)Math.Round(s.Progress * 100)) : s.State.Title()
            : "";

        public void SetValue(string value) => throw new InvalidOperationException("The printer model is read-only.");
    }
}
