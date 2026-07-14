using System.Windows;
using System.Windows.Input;

namespace StreamNumDeck.Wpf.Overlays;

public partial class MicrophoneMuteOverlayWindow : Window
{
    private readonly OverlayPositionStore positionStore;

    public MicrophoneMuteOverlayWindow(OverlayPositionStore positionStore)
    {
        this.positionStore = positionStore;
        InitializeComponent();
        var position = positionStore.Load();
        if (position.HasValue)
        {
            Left = position.Value.Left;
            Top = position.Value.Top;
        }
        else
        {
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Top + 24;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
            positionStore.Save(Left, Top);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
