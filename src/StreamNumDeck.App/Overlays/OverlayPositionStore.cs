using Windows.Graphics;
using Windows.Storage;

namespace StreamNumDeck.App.Overlays;

public sealed class OverlayPositionStore
{
    private const string XKey = "MicrophoneOverlay.Position.X";
    private const string YKey = "MicrophoneOverlay.Position.Y";
    private readonly ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

    public PointInt32? Load()
    {
        return settings.Values.TryGetValue(XKey, out var xValue)
            && settings.Values.TryGetValue(YKey, out var yValue)
            && xValue is int x
            && yValue is int y
                ? new PointInt32(x, y)
                : null;
    }

    public void Save(PointInt32 position)
    {
        settings.Values[XKey] = position.X;
        settings.Values[YKey] = position.Y;
    }
}
