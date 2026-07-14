using System.Globalization;

namespace StreamNumDeck.Wpf.Overlays;

public sealed class OverlayPositionStore
{
    private readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StreamNumDeck",
        "overlay-position.txt");

    public (double Left, double Top)? Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var parts = File.ReadAllText(filePath).Split('|');
            return parts.Length == 2
                   && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left)
                   && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top)
                ? (left, top)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public void Save(double left, double top)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            $"{left.ToString(CultureInfo.InvariantCulture)}|{top.ToString(CultureInfo.InvariantCulture)}");
    }
}
