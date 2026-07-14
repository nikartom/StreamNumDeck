<h1 align="center">StreamNumDeck</h1>

<p align="center">
  <img src="https://flagcdn.com/24x18/us.png" width="24" height="18" alt="English" title="English">
  <img src="https://flagcdn.com/24x18/ru.png" width="24" height="18" alt="Русский" title="Русский">
  <img src="https://flagcdn.com/24x18/de.png" width="24" height="18" alt="Deutsch" title="Deutsch">
  <img src="https://flagcdn.com/24x18/fr.png" width="24" height="18" alt="Français" title="Français">
  <img src="https://flagcdn.com/24x18/es.png" width="24" height="18" alt="Español" title="Español">
  <img src="https://flagcdn.com/24x18/br.png" width="24" height="18" alt="Português do Brasil" title="Português do Brasil">
  <img src="https://flagcdn.com/24x18/it.png" width="24" height="18" alt="Italiano" title="Italiano">
  <img src="https://flagcdn.com/24x18/pl.png" width="24" height="18" alt="Polski" title="Polski">
  <img src="https://flagcdn.com/24x18/ua.png" width="24" height="18" alt="Українська" title="Українська">
  <img src="https://flagcdn.com/24x18/tr.png" width="24" height="18" alt="Türkçe" title="Türkçe">
  <img src="https://flagcdn.com/24x18/nl.png" width="24" height="18" alt="Nederlands" title="Nederlands">
  <img src="https://flagcdn.com/24x18/se.png" width="24" height="18" alt="Svenska" title="Svenska">
  <img src="https://flagcdn.com/24x18/cz.png" width="24" height="18" alt="Čeština" title="Čeština">
  <img src="https://flagcdn.com/24x18/ro.png" width="24" height="18" alt="Română" title="Română">
  <img src="https://flagcdn.com/24x18/cn.png" width="24" height="18" alt="简体中文" title="简体中文">
  <img src="https://flagcdn.com/24x18/jp.png" width="24" height="18" alt="日本語" title="日本語">
  <img src="https://flagcdn.com/24x18/kr.png" width="24" height="18" alt="한국어" title="한국어">
  <img src="https://flagcdn.com/24x18/in.png" width="24" height="18" alt="हिन्दी" title="हिन्दी">
  <img src="https://flagcdn.com/24x18/sa.png" width="24" height="18" alt="العربية" title="العربية">
  <img src="https://flagcdn.com/24x18/id.png" width="24" height="18" alt="Bahasa Indonesia" title="Bahasa Indonesia">
  <img src="https://flagcdn.com/24x18/vn.png" width="24" height="18" alt="Tiếng Việt" title="Tiếng Việt">
  <img src="https://flagcdn.com/24x18/th.png" width="24" height="18" alt="ไทย" title="ไทย">
</p>

<p align="center">
  <img src="docs/images/streamnumdeck-demo-en.png" width="500" alt="StreamNumDeck with a configured streaming profile">
</p>

<p align="center">
  <strong>Turn the keyboard you already own into a configurable control deck for streaming, gaming, and everyday automation.</strong>
</p>

<p align="center">
  <a href="https://github.com/nikartom/StreamNumDeck/releases/latest"><strong>Download the latest release</strong></a>
</p>

## English

StreamNumDeck is a native Windows 11 application that assigns actions and macros to the numeric keypad and the six-key navigation block (`Insert`, `Home`, `Page Up`, `Delete`, `End`, and `Page Down`). While the application is running, it captures those keys globally and executes the actions configured in the active profile.

The physical `NumLock` state selects one of two independent assignment layers. The remaining 22 keys can therefore hold up to **44 actions per profile**, without adding another device to the desk.

### Highlights

- Two action layers controlled by the physical `NumLock` key and synchronized with the real keyboard state.
- Multiple named profiles with custom icons, quick switching from the navigation rail, and profile selection from the system tray.
- A visual key editor with a large built-in icon catalog and support for user-provided images.
- Global key capture with a `Ctrl+Alt+F12` emergency pause.
- Minimize-to-tray behavior, capture control, profile switching, and exit commands in the tray menu.
- Light, dark, and Windows-controlled themes.
- Optional launch at Windows startup.
- Local configuration storage and protected OBS credentials through Windows Password Vault.
- A draggable microphone-mute indicator that can be placed on any monitor.

### Actions

| Group | Available actions |
| --- | --- |
| **OBS Studio** | Switch scenes; show or hide sources; mute or unmute inputs; start or stop streaming; start or stop recording; save the replay buffer; restart a media source. |
| **Sound** | Play an audio file; mute or unmute the default microphone; mute system audio; raise or lower master volume; control the volume of a selected application. |
| **System** | Launch a program; open a file or folder; open an HTTP/HTTPS link; execute a multi-step keyboard macro with shortcuts and timed delays. |

StreamNumDeck communicates with OBS through the OBS WebSocket 5.x protocol. Enable the WebSocket server in OBS, then enter its address, port, and optional password in the application settings. Starting a stream or recording always requires an explicitly assigned action; connection tests never start either one.

### Localization

The interface follows the Windows display language automatically. Resources are included for:

English, Russian, German, French, Spanish, Brazilian Portuguese, Italian, Polish, Ukrainian, Turkish, Dutch, Swedish, Czech, Romanian, Simplified Chinese, Japanese, Korean, Hindi, Arabic, Indonesian, Vietnamese, and Thai.

English and Russian have been manually reviewed. The remaining translations are currently machine-generated and welcome native-speaker review.

### Download and install

The current beta is available as a self-contained **Windows 11 x64 MSIX** on the [GitHub Releases page](https://github.com/nikartom/StreamNumDeck/releases). It includes .NET and Windows App SDK runtime files, so users do not need to install them separately.

The beta package is test-signed. Before installing the MSIX, an administrator must import the public `.cer` file included with the release into **Local Computer → Trusted People**. Only trust the certificate downloaded from this repository and compare its thumbprint with the value published in the release notes. Detailed bilingual instructions are included in [the MSIX installation guide](docs/install-msix.md).

### Technology and architecture

- .NET 10 and C# 14
- WinUI 3 and Windows App SDK
- Single-project MSIX packaging
- Layered `Core`, `Infrastructure`, and `App` projects
- MSTest coverage for the domain model, persistence, keyboard capture, macros, audio actions, and OBS protocol

The application does not send telemetry. Profiles, assignments, and settings remain on the local computer; the OBS password is stored separately in Windows Password Vault.

### Build from source

Requirements:

- Windows 11
- .NET SDK 10.0.301 or a compatible .NET 10 patch selected by `global.json`
- .NET Desktop Runtime 10.0.9 or a compatible .NET 10 patch
- Windows Developer Mode for registering the unpackaged debug layout

Build and test the solution:

```powershell
./scripts/build.ps1
```

Register the freshly built application layout:

```powershell
./scripts/deploy.ps1
```

Create a tested, self-contained x64 MSIX for a beta release:

```powershell
./scripts/package-msix.ps1 -CreateDevelopmentCertificate
```

After registration, launch StreamNumDeck from the Start menu. The deployment script preserves application data when replacing the current development package.

The repository layout and current implementation stages are documented in [the architecture notes](docs/architecture.md) and [the roadmap](docs/roadmap.md).

### Project status

StreamNumDeck is under active development. The main control-deck workflow, profiles, keyboard capture, action editor, audio integration, OBS integration, tray controls, overlays, localization, and persistent settings are implemented. A test-signed MSIX beta is available; broader stabilization and production-trusted signing remain in progress.

If the application is useful to you, you can [support its development on DonationAlerts](https://www.donationalerts.com/r/kventinburatino).

---

## Русский

StreamNumDeck — нативное приложение для Windows 11, которое назначает действия и макросы на цифровой блок клавиатуры и шесть клавиш навигации: `Insert`, `Home`, `Page Up`, `Delete`, `End` и `Page Down`. Пока приложение работает, оно глобально перехватывает эти клавиши и выполняет команды активного профиля.

Физическое состояние `NumLock` выбирает один из двух независимых слоёв. Поэтому на оставшиеся 22 клавиши можно назначить до **44 действий в каждом профиле**, не покупая отдельное устройство.

### Основные возможности

- Два слоя действий, переключаемые физической клавишей `NumLock` и синхронизированные с реальным состоянием клавиатуры.
- Несколько пользовательских профилей с иконками, переключением на боковой панели и выбором из меню в области уведомлений.
- Визуальный редактор клавиш с большим набором встроенных иконок и загрузкой собственных изображений.
- Глобальный перехват клавиш с аварийной паузой по `Ctrl+Alt+F12`.
- Сворачивание в область уведомлений, включение перехвата, выбор профиля и выход через меню значка в трее.
- Светлое, тёмное и системное оформление Windows.
- Необязательный запуск вместе с Windows.
- Локальное хранение конфигурации и защищённое хранение пароля OBS в Windows Password Vault.
- Перетаскиваемый индикатор выключенного микрофона, который можно разместить на любом мониторе.

### Действия

| Группа | Доступные действия |
| --- | --- |
| **OBS Studio** | Переключение сцен; показ и скрытие источников; выключение звука входов; запуск и остановка трансляции или записи; сохранение буфера повтора; перезапуск медиаисточника. |
| **Звук** | Воспроизведение аудиофайла; включение и выключение микрофона по умолчанию; отключение общего звука; изменение системной громкости; управление громкостью выбранного приложения. |
| **Система** | Запуск программы; открытие файла или папки; открытие HTTP/HTTPS-ссылки; выполнение многошагового клавиатурного макроса с сочетаниями клавиш и задержками. |

StreamNumDeck взаимодействует с OBS по протоколу OBS WebSocket 5.x. Включите WebSocket-сервер в OBS, затем укажите в настройках приложения его адрес, порт и пароль при необходимости. Запуск трансляции или записи происходит только по явно назначенному действию — проверка подключения их никогда не запускает.

### Локализация

Язык интерфейса автоматически выбирается по языку Windows. В комплект входят ресурсы для следующих языков:

английский, русский, немецкий, французский, испанский, бразильский португальский, итальянский, польский, украинский, турецкий, нидерландский, шведский, чешский, румынский, упрощённый китайский, японский, корейский, хинди, арабский, индонезийский, вьетнамский и тайский.

Английский и русский переводы вычитаны вручную. Остальные переводы пока сгенерированы автоматически и нуждаются в проверке носителями языка.

### Загрузка и установка

Текущая beta-версия опубликована в виде автономного **MSIX для Windows 11 x64** на странице [GitHub Releases](https://github.com/nikartom/StreamNumDeck/releases). В пакет уже входят компоненты .NET и Windows App SDK, поэтому пользователю не нужно устанавливать их отдельно.

Beta-пакет подписан тестовым сертификатом. Перед установкой MSIX администратор должен импортировать приложенный публичный файл `.cer` в хранилище **Локальный компьютер → Доверенные лица**. Доверяйте только сертификату, загруженному из этого репозитория, и сравните его отпечаток со значением в примечаниях к выпуску. Подробная инструкция на русском и английском находится в [руководстве по установке MSIX](docs/install-msix.md).

### Технологии и архитектура

- .NET 10 и C# 14
- WinUI 3 и Windows App SDK
- MSIX-упаковка в одном проекте приложения
- Разделение решения на проекты `Core`, `Infrastructure` и `App`
- Автоматические тесты доменной модели, сохранения конфигурации, перехвата клавиатуры, макросов, звуковых действий и протокола OBS

Приложение не отправляет телеметрию. Профили, назначения и настройки остаются на компьютере пользователя, а пароль OBS хранится отдельно в Windows Password Vault.

### Сборка из исходников

Потребуются:

- Windows 11
- .NET SDK 10.0.301 или совместимое обновление .NET 10, выбранное через `global.json`
- .NET Desktop Runtime 10.0.9 или совместимое обновление .NET 10
- режим разработчика Windows для регистрации отладочной сборки

Сборка и запуск тестов:

```powershell
./scripts/build.ps1
```

Регистрация свежей сборки приложения:

```powershell
./scripts/deploy.ps1
```

Создание протестированного автономного MSIX x64 для beta-релиза:

```powershell
./scripts/package-msix.ps1 -CreateDevelopmentCertificate
```

После регистрации запустите StreamNumDeck из меню «Пуск». Скрипт развёртывания сохраняет пользовательские данные при замене текущей отладочной версии.

Структура решения и этапы разработки описаны в [документе об архитектуре](docs/architecture.md) и [плане разработки](docs/roadmap.md).

### Состояние проекта

StreamNumDeck находится в активной разработке. Основная панель, профили, перехват клавиш, редактор действий, управление звуком, интеграция с OBS, меню трея, оверлей, локализация и сохранение настроек уже реализованы. Тестово подписанный MSIX beta уже доступен; дополнительная стабилизация и переход на доверенную производственную подпись продолжаются.

Если приложение оказалось полезным, вы можете [поддержать разработку на DonationAlerts](https://www.donationalerts.com/r/kventinburatino).

## Author and license / Автор и лицензия

Created by **Nikolay Karchevskiy**. Copyright © 2026 Nikolay Karchevskiy.

StreamNumDeck is distributed under the [PolyForm Noncommercial License 1.0.0](LICENSE.md). You may use, study, modify, and redistribute the software for permitted noncommercial purposes. Commercial use requires separate permission from the author.

Because the license restricts commercial use, StreamNumDeck is source-available rather than OSI-approved Open Source software.

Автор — **Nikolay Karchevskiy**. Copyright © 2026 Nikolay Karchevskiy.

StreamNumDeck распространяется по лицензии [PolyForm Noncommercial 1.0.0](LICENSE.md). Приложение и исходный код можно использовать, изучать, изменять и распространять в разрешённых некоммерческих целях. Для коммерческого использования необходимо отдельное разрешение автора.

Из-за ограничения коммерческого использования проект относится к source-available, а не к Open Source в определении OSI.
