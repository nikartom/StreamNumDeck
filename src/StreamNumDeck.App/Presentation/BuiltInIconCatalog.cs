using StreamNumDeck.App.Localization;

namespace StreamNumDeck.App.Presentation;

public sealed record BuiltInIconOption(string Id, string DefaultName, string Glyph, string? ResourceId = null)
{
    public string Name => AppStrings.Get($"Icon_{(ResourceId ?? Id).Replace('-', '_')}", DefaultName);
}

public static class BuiltInIconCatalog
{
    public static BuiltInIconOption BlankOption { get; } = new("blank", "No icon", string.Empty, "square");

    public static IReadOnlyList<BuiltInIconOption> Options { get; } =
    [
        // Универсальные действия
        new("square", "Без иконки", "\uE710"),
        new("plus", "Плюс", "\uE710"),
        new("minus", "Минус", "\uE738"),
        new("check", "Готово", "\uE73E"),
        new("cancel", "Отмена", "\uE711"),
        new("edit", "Изменить", "\uE70F"),
        new("delete", "Удалить", "\uE74D"),
        new("save", "Сохранить", "\uE74E"),
        new("copy", "Копировать", "\uE8C8"),
        new("paste", "Вставить", "\uE77F"),
        new("refresh", "Обновить", "\uE72C"),
        new("search", "Поиск", "\uE721"),
        new("link", "Ссылка", "\uE71B"),
        new("share", "Поделиться", "\uE72D"),
        new("send", "Отправить", "\uE724"),
        new("pin", "Закрепить", "\uE718"),
        new("lock", "Заблокировать", "\uE72E"),
        new("unlock", "Разблокировать", "\uE785"),
        new("settings", "Настройки", "\uE713"),
        new("power", "Питание", "\uE7E8"),
        new("command", "Командная строка", "\uE756"),
        new("code", "Код", "\uE943"),
        new("calculator", "Калькулятор", "\uE8EF"),
        new("info", "Информация", "\uE946"),
        new("help", "Помощь", "\uE897"),
        new("warning", "Предупреждение", "\uE7BA"),
        new("blocked", "Заблокировано", "\uE733"),

        // Медиа
        new("play", "Запуск", "\uE768"),
        new("pause", "Пауза", "\uE769"),
        new("stop", "Стоп", "\uE71A"),
        new("record", "Запись", "\uE7C8"),
        new("previous", "Предыдущий", "\uE892"),
        new("next", "Следующий", "\uE893"),
        new("rewind", "Перемотать назад", "\uEB9E"),
        new("fast-forward", "Перемотать вперёд", "\uEB9D"),
        new("shuffle", "Случайный порядок", "\uE8B1"),
        new("repeat", "Повтор", "\uE8EE"),
        new("fullscreen", "Полный экран", "\uE740"),
        new("window", "Оконный режим", "\uE73F"),

        // Звук и видео
        new("volume", "Громкость", "\uE767"),
        new("volume-off", "Громкость: 0", "\uE992"),
        new("volume-low", "Громкость ниже", "\uE993"),
        new("volume-medium", "Средняя громкость", "\uE994"),
        new("volume-high", "Громкость выше", "\uE995"),
        new("mute", "Без звука", "\uE74F"),
        new("audio", "Аудио", "\uE8D6"),
        new("speakers", "Динамики", "\uE7F5"),
        new("microphone", "Микрофон", "\uE720"),
        new("video", "Видео", "\uE714"),
        new("camera", "Камера", "\uE722"),
        new("headphones", "Наушники", "\uE7F6"),

        // Стриминг и общение
        new("broadcast", "Трансляция", "\uE95A"),
        new("switch", "Переключение", "\uE8AB"),
        new("message", "Сообщение", "\uE8BD"),
        new("chat", "Чат", "\uE8F2"),
        new("mail", "Почта", "\uE715"),
        new("phone", "Телефон", "\uE717"),
        new("people", "Люди", "\uE716"),
        new("video-chat", "Видеочат", "\uE8AA"),
        new("megaphone", "Объявление", "\uE789"),

        // Интернет и файлы
        new("globe", "Интернет", "\uE774"),
        new("wifi", "Wi-Fi", "\uE701"),
        new("bluetooth", "Bluetooth", "\uE702"),
        new("usb", "USB", "\uE88E"),
        new("cloud", "Облако", "\uE753"),
        new("download", "Скачать", "\uE896"),
        new("upload", "Загрузить", "\uE898"),
        new("folder", "Папка", "\uE8B7"),
        new("folder-open", "Открытая папка", "\uE838"),
        new("document", "Документ", "\uE8A5"),
        new("image", "Изображение", "\uE8B9"),

        // Игры, реакции и эффекты
        new("star", "Звезда", "\uE734"),
        new("star-fill", "Избранное", "\uE735"),
        new("heart", "Сердце", "\uEB51"),
        new("heart-fill", "Сердце: заливка", "\uEB52"),
        new("like", "Нравится", "\uE8E1"),
        new("game", "Игра", "\uE7FC"),
        new("xbox", "Игровая консоль", "\uE990"),
        new("robot", "Робот", "\uE99A"),
        new("lightning", "Молния", "\uE945"),
        new("emoji", "Эмодзи", "\uE899"),
        new("effects", "Эффекты", "\uE794"),

        // Система и прочее
        new("calendar", "Календарь", "\uE787"),
        new("timer", "Таймер", "\uE916"),
        new("history", "История", "\uE81C"),
        new("home", "Домой", "\uE80F"),
        new("up", "Вверх", "\uE74A"),
        new("down", "Вниз", "\uE74B"),
        new("back", "Назад", "\uE72B"),
        new("forward", "Вперёд", "\uE72A"),
        new("map-pin", "Метка на карте", "\uE707"),
        new("car", "Автомобиль", "\uE804"),
        new("work", "Работа", "\uE821"),
        new("shop", "Магазин", "\uE719"),
        new("shield", "Защита", "\uE83D"),
        new("permissions", "Доступ", "\uE8D7"),
        new("view", "Просмотр", "\uE890"),
        new("color", "Цвет", "\uE790"),
        new("devices", "Устройства", "\uE772"),
        new("keyboard", "Клавиатура", "\uE765"),
        new("print", "Печать", "\uE749"),
        new("flashlight", "Фонарик", "\uE754"),
    ];

    private static readonly IReadOnlyDictionary<string, BuiltInIconOption> OptionsById =
        Options.Append(BlankOption).ToDictionary(option => option.Id, StringComparer.Ordinal);

    public static BuiltInIconOption Get(string id) =>
        OptionsById.TryGetValue(id, out var option) ? option : Options[0];
}
