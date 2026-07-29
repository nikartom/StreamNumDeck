using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StreamNumDeck.Wpf.Presentation;

public sealed record EmojiOption(string Emoji, string Name, string AssetDirectory)
{
    public string Id => Emoji;

    public string Glyph => Emoji;

    public string FontFamily => "Segoe UI Emoji";

    public string ImagePath => EmojiCatalog.GetImagePath(Emoji);
}

public static class EmojiCatalog
{
    public static IReadOnlyList<EmojiOption> Options { get; } =
    [
        // Faces
        new("😀", "Grinning face", "Grinning face"),
        new("😃", "Grinning face with big eyes", "Grinning face with big eyes"),
        new("😄", "Grinning face with smiling eyes", "Grinning face with smiling eyes"),
        new("😁", "Beaming face", "Beaming face with smiling eyes"),
        new("😆", "Laughing face", "Grinning squinting face"),
        new("😅", "Grinning face with sweat", "Grinning face with sweat"),
        new("😂", "Tears of joy", "Face with tears of joy"),
        new("🤣", "Rolling on the floor laughing", "Rolling on the floor laughing"),
        new("😊", "Smiling face", "Smiling face with smiling eyes"),
        new("🙂", "Slightly smiling face", "Slightly smiling face"),
        new("😉", "Winking face", "Winking face"),
        new("😍", "Heart eyes", "Smiling face with heart-eyes"),
        new("🥰", "Smiling face with hearts", "Smiling face with hearts"),
        new("😘", "Blowing a kiss", "Face blowing a kiss"),
        new("😋", "Yummy", "Face savoring food"),
        new("😜", "Winking face with tongue", "Winking face with tongue"),
        new("🤪", "Zany face", "Zany face"),
        new("🤓", "Nerd face", "Nerd face"),
        new("😎", "Cool", "Smiling face with sunglasses"),
        new("🤩", "Star-struck", "Star-struck"),
        new("🥳", "Party", "Partying face"),
        new("🤔", "Thinking", "Thinking face"),
        new("🤨", "Raised eyebrow", "Face with raised eyebrow"),
        new("🙄", "Rolling eyes", "Face with rolling eyes"),
        new("😬", "Grimacing", "Grimacing face"),
        new("😴", "Sleeping", "Sleeping face"),
        new("🥺", "Pleading face", "Pleading face"),
        new("😢", "Crying face", "Crying face"),
        new("😭", "Loudly crying", "Loudly crying face"),
        new("😤", "Steam from nose", "Face with steam from nose"),
        new("😠", "Angry face", "Angry face"),
        new("😡", "Enraged face", "Pouting face"),
        new("🤬", "Swearing face", "Face with symbols on mouth"),
        new("🤯", "Mind blown", "Exploding head"),
        new("😳", "Flushed face", "Flushed face"),
        new("😱", "Screaming in fear", "Face screaming in fear"),
        new("🤭", "Hand over mouth", "Face with hand over mouth"),
        new("🤫", "Shushing face", "Shushing face"),
        new("🤥", "Lying face", "Lying face"),
        new("😶", "Face without mouth", "Face without mouth"),
        new("😐", "Neutral face", "Neutral face"),
        new("🤠", "Cowboy", "Cowboy hat face"),
        new("😈", "Smiling devil", "Smiling face with horns"),
        new("🤡", "Clown", "Clown face"),
        new("👻", "Ghost", "Ghost"),
        new("💀", "Skull", "Skull"),
        new("👽", "Alien", "Alien"),
        new("🤖", "Robot", "Robot"),

        // Gestures and people
        new("👍", "Thumbs up", "Thumbs up"),
        new("👎", "Thumbs down", "Thumbs down"),
        new("👏", "Applause", "Clapping hands"),
        new("🙌", "Celebration", "Raising hands"),
        new("🙏", "Folded hands", "Folded hands"),
        new("💪", "Strong", "Flexed biceps"),
        new("👋", "Hello", "Waving hand"),
        new("🤝", "Handshake", "Handshake"),
        new("👌", "OK", "Ok hand"),
        new("✌️", "Victory", "Victory hand"),
        new("🤞", "Fingers crossed", "Crossed fingers"),
        new("🤘", "Rock on", "Sign of the horns"),
        new("🤙", "Call me", "Call me hand"),
        new("👀", "Eyes", "Eyes"),
        new("🧠", "Brain", "Brain"),
        new("👑", "Crown", "Crown"),

        // Reactions and symbols
        new("❤️", "Red heart", "Red heart"),
        new("🧡", "Orange heart", "Orange heart"),
        new("💛", "Yellow heart", "Yellow heart"),
        new("💚", "Green heart", "Green heart"),
        new("💙", "Blue heart", "Blue heart"),
        new("💜", "Purple heart", "Purple heart"),
        new("🖤", "Black heart", "Black heart"),
        new("💔", "Broken heart", "Broken heart"),
        new("💕", "Two hearts", "Two hearts"),
        new("💖", "Sparkling heart", "Sparkling heart"),
        new("🔥", "Fire", "Fire"),
        new("✨", "Sparkles", "Sparkles"),
        new("⭐", "Star", "Star"),
        new("🌟", "Glowing star", "Glowing star"),
        new("⚡", "Lightning", "High voltage"),
        new("💥", "Boom", "Collision"),
        new("💯", "One hundred", "Hundred points"),
        new("🎉", "Confetti", "Party popper"),
        new("🎊", "Confetti ball", "Confetti ball"),
        new("🎁", "Gift", "Wrapped gift"),
        new("🏆", "Trophy", "Trophy"),
        new("🥇", "First place", "1st place medal"),
        new("✅", "Done", "Check mark button"),
        new("❌", "Cancel", "Cross mark"),
        new("⚠️", "Warning", "Warning"),
        new("🛑", "Stop", "Stop sign"),
        new("❓", "Question", "Red question mark"),
        new("❗", "Important", "Red exclamation mark"),

        // Media, games and streaming
        new("▶️", "Play", "Play button"),
        new("⏸️", "Pause", "Pause button"),
        new("⏹️", "Stop media", "Stop button"),
        new("⏺️", "Record", "Record button"),
        new("⏭️", "Next", "Next track button"),
        new("⏮️", "Previous", "Last track button"),
        new("🎵", "Music", "Musical note"),
        new("🎶", "Music notes", "Musical notes"),
        new("🎤", "Microphone", "Microphone"),
        new("🎧", "Headphones", "Headphone"),
        new("🔊", "Volume", "Speaker high volume"),
        new("🔇", "Muted", "Muted speaker"),
        new("📺", "Television", "Television"),
        new("📹", "Video", "Video camera"),
        new("📷", "Camera", "Camera"),
        new("🎬", "Clapper board", "Clapper board"),
        new("🎮", "Game", "Video game"),
        new("🕹️", "Joystick", "Joystick"),
        new("🎲", "Dice", "Game die"),
        new("🎯", "Target", "Bullseye"),
        new("🚀", "Rocket", "Rocket"),
        new("🎸", "Guitar", "Guitar"),
        new("🎹", "Keyboard", "Musical keyboard"),
        new("🥁", "Drum", "Drum"),

        // Communication, objects and places
        new("💬", "Chat", "Speech balloon"),
        new("💭", "Thought", "Thought balloon"),
        new("📢", "Announcement", "Loudspeaker"),
        new("📣", "Megaphone", "Megaphone"),
        new("🔔", "Notification", "Bell"),
        new("🔕", "Notifications off", "Bell with slash"),
        new("📞", "Phone", "Telephone receiver"),
        new("✉️", "Mail", "Envelope"),
        new("📧", "E-mail", "E-mail"),
        new("🌐", "Internet", "Globe with meridians"),
        new("🏠", "Home", "House"),
        new("🏢", "Office", "Office building"),
        new("⚙️", "Settings", "Gear"),
        new("💡", "Idea", "Light bulb"),
        new("🔧", "Tool", "Wrench"),
        new("🔨", "Hammer", "Hammer"),
        new("🗑️", "Delete", "Wastebasket"),
        new("🔒", "Locked", "Locked"),
        new("🔓", "Unlocked", "Unlocked"),
        new("🔑", "Key", "Key"),
        new("📌", "Pin", "Pushpin"),
        new("📁", "Folder", "File folder"),
        new("📄", "Document", "Page facing up"),
        new("📅", "Calendar", "Calendar"),
        new("⏰", "Alarm", "Alarm clock"),
        new("⏱️", "Stopwatch", "Stopwatch"),
        new("☕", "Coffee", "Hot beverage"),
        new("🍕", "Pizza", "Pizza"),
        new("🍔", "Burger", "Hamburger"),
        new("🍿", "Popcorn", "Popcorn"),
        new("🐱", "Cat", "Cat face"),
        new("🐶", "Dog", "Dog face"),
        new("🦊", "Fox", "Fox"),
        new("🦄", "Unicorn", "Unicorn"),
        new("🌈", "Rainbow", "Rainbow"),
        new("☀️", "Sun", "Sun"),
        new("🌙", "Moon", "Crescent moon"),
    ];

    private static readonly IReadOnlyDictionary<string, EmojiOption> OptionsByEmoji =
        Options.ToDictionary(option => option.Emoji, StringComparer.Ordinal);

    public static EmojiOption? Find(string emoji) =>
        OptionsByEmoji.TryGetValue(emoji, out var option) ? option : null;

    public static string GetImagePath(string emoji) =>
        $"/StreamNumDeck;component/Assets/Emoji/{GetAssetFileName(emoji)}";

    public static ImageSource? TryLoadImage(string emoji)
    {
        if (!OptionsByEmoji.ContainsKey(emoji))
        {
            return null;
        }

        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri(GetImagePath(emoji), UriKind.Relative));
            if (resource is null)
            {
                return null;
            }

            using (resource.Stream)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = resource.Stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
        catch
        {
            return null;
        }
    }

    private static string GetAssetFileName(string emoji)
    {
        var result = new StringBuilder();
        for (var index = 0; index < emoji.Length; index++)
        {
            if (result.Length > 0)
            {
                result.Append('-');
            }

            var codePoint = char.ConvertToUtf32(emoji, index);
            result.Append(codePoint.ToString("x", CultureInfo.InvariantCulture));
            if (char.IsHighSurrogate(emoji[index]))
            {
                index++;
            }
        }

        result.Append(".png");
        return result.ToString();
    }
}
