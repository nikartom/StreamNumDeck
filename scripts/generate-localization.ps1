[CmdletBinding()]
param(
    [string[]] $RegenerateKey = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stringsRoot = Join-Path $root 'src\StreamNumDeck.App\Strings'

$strings = [ordered]@{
    AppDisplayName = 'StreamNumDeck'
    AppDescription = 'Custom control deck for streamers and gamers'
    'Nav_NewProfile.Content' = 'New profile'
    'Nav_NewProfile.ToolTipService.ToolTip' = 'New profile'
    'Nav_Settings.Content' = 'Settings'
    'ProfileDialog.CloseButtonText' = 'Cancel'
    'ProfileDialog.PrimaryButtonText' = 'Create'
    'ProfileDialog.Title' = 'New profile'
    'ProfileName.Header' = 'Name'
    'ProfileName.PlaceholderText' = 'For example, Gaming Stream'
    'ProfileIcon.Text' = 'Icon'
    'Settings_Title.Text' = 'Global settings'
    'Settings_Subtitle.Text' = 'Sound, OBS Studio, and application behavior'
    'Common_SaveButton.Content' = 'Save'
    'Settings_SoundSection.Text' = 'Sound'
    'Settings_OutputDevice.Header' = 'Output device'
    Audio_SystemDefaultDevice = 'System default device'
    'Settings_MasterVolume.Header' = 'Master volume'
    'Settings_ConcurrentSounds.Header' = 'Allow simultaneous sounds'
    'Settings_PreloadSounds.Header' = 'Preload short sounds'
    'Settings_SystemSection.Text' = 'System'
    'Settings_StartWithWindows.Header' = 'Start with Windows'
    'Settings_MinimizeToTray.Header' = 'Minimize to notification area'
    'Settings_CaptureOnStartup.Header' = 'Enable key capture on startup'
    'Settings_Theme.Header' = 'Appearance'
    'Theme_System.Content' = 'Use Windows setting'
    'Theme_Light.Content' = 'Light'
    'Theme_Dark.Content' = 'Dark'
    'Settings_PositionOverlay.Content' = 'Position the microphone indicator'
    'Settings_OverlayHint.Text' = 'Click the button, then drag the indicator to the desired position. Its position will be saved.'
    'Settings_KeyboardSection.Text' = 'Keyboard'
    'Settings_EmergencyPause.Header' = 'Turn key capture on/off'
    'Settings_NumLockHint.Text' = 'The physical NumLock key switches between two assignment layers.'
    'Settings_ObsSection.Text' = 'OBS Studio'
    'Settings_ObsAddress.Header' = 'Address'
    'Settings_ObsPort.Header' = 'Port'
    'Settings_ObsPassword.Header' = 'Password'
    'Settings_ObsPassword.PlaceholderText' = 'Leave empty to keep the current password'
    'Settings_PasswordStored.Text' = 'The password is stored securely by Windows'
    'Settings_TestObs.Content' = 'Test connection'
    'Settings_SupportSection.Text' = 'Support development'
    'Settings_SupportHint.Text' = 'If you find StreamNumDeck useful, you can support its development.'
    'Settings_SupportButton.Content' = 'Donate via DonationAlerts'
    'Deck_Capture.Text' = 'Key capture'
    'Deck_CaptureToggle.AutomationProperties.Name' = 'Key capture'
    'Deck_CaptureToggle.ToolTipService.ToolTip' = 'Ctrl+Alt+F12 — emergency pause'
    'Deck_ActionError.Title' = 'Action failed'
    'Deck_NumLock.ToolTipService.ToolTip' = 'Switch NumLock layer'
    'Editor_Dialog.CloseButtonText' = 'Cancel'
    'Editor_Dialog.PrimaryButtonText' = 'Save'
    'Editor_Dialog.SecondaryButtonText' = 'Clear'
    'Editor_Appearance.Text' = 'Appearance'
    'Editor_CustomIcon.Content' = 'Choose a custom icon…'
    'Editor_Label.Header' = 'Label'
    'Editor_Label.PlaceholderText' = 'Optional'
    'Editor_CopyOtherLayer.Content' = 'Copy assignment from the other layer'
    'Editor_ActionSection.Text' = 'Action'
    'Editor_ActionGroup.Header' = 'Group'
    'Editor_ActionType.Header' = 'Action'
    'Editor_AudioFile.Header' = 'Audio file'
    'Common_BrowseButton.Content' = 'Browse…'
    'Editor_PreviewSound.Content' = 'Play preview'
    'Common_StopButton.Content' = 'Stop'
    'Editor_Volume.Header' = 'Volume'
    'Editor_Behavior.Header' = 'Behavior'
    'Editor_Application.Header' = 'Application'
    'Editor_Application.PlaceholderText' = 'Select an app or enter an executable name'
    'Editor_VolumeStep.Header' = 'Volume step, %'
    'Editor_AudioSessionHint.Text' = 'The list includes apps with an active Windows audio session.'
    'Editor_ObsTarget.Header' = 'Scene or source'
    'Editor_ObsTarget.PlaceholderText' = 'Enter the exact name used in OBS'
    'Editor_SystemTarget.Header' = 'Program, path, or address'
    'Editor_Arguments.Header' = 'Arguments'
    'Editor_Arguments.PlaceholderText' = 'Optional'
    'Editor_WorkingDirectory.Header' = 'Working directory'
    'Editor_WorkingDirectory.PlaceholderText' = 'Optional'
    'Editor_MacroHint.Text' = 'Enter each step on a new line. Shortcut: Ctrl+Shift+S. Delay: wait 250. Lines starting with # are comments.'
    'Editor_MacroSequence.Header' = 'Sequence'
    Common_On = 'On'
    Common_Off = 'Off'
    Common_Save = 'Save'
    Common_Create = 'Create'
    Common_Delete = 'Delete'
    Common_Cancel = 'Cancel'
    Common_Close = 'Close'
    Common_Minimize = 'Minimize'
    FileFilter_Audio = 'Audio files'
    FileFilter_Images = 'Images'
    FileFilter_AllFiles = 'All files'
    ActionGroup_None = 'No action'
    ActionGroup_Sound = 'Sound'
    ActionGroup_System = 'System and commands'
    SoundBehavior_Alongside = 'Play alongside other sounds'
    SoundBehavior_Restart = 'Restart if already playing'
    SoundBehavior_StopOthers = 'Stop all other sounds'
    Editor_Title = '{0} · NumLock {1}'
    Action_PlaySound = 'Play sound'
    Action_ToggleMicrophoneMute = 'Mute or unmute the default microphone'
    Action_ToggleMasterMute = 'Mute or unmute system audio'
    Action_AdjustMasterVolume = 'Adjust master volume'
    Action_AdjustApplicationVolume = 'Adjust application volume'
    Action_IncreaseMasterVolume = 'Increase master volume'
    Action_DecreaseMasterVolume = 'Decrease master volume'
    Action_IncreaseApplicationVolume = 'Increase application volume'
    Action_DecreaseApplicationVolume = 'Decrease application volume'
    Action_ObsSwitchScene = 'Switch scene'
    Action_ObsToggleSource = 'Show or hide source'
    Action_ObsToggleMute = 'Mute or unmute input'
    Action_ObsStartStream = 'Start streaming'
    Action_ObsStopStream = 'Stop streaming'
    Action_ObsStartRecord = 'Start recording'
    Action_ObsStopRecord = 'Stop recording'
    Action_ObsSaveReplay = 'Save replay buffer'
    Action_ObsRestartMedia = 'Restart media source'
    Action_LaunchProcess = 'Launch program'
    Action_OpenPath = 'Open file or folder'
    Action_OpenUri = 'Open link'
    Action_KeyboardMacro = 'Run keyboard macro'
    Action_None = 'Do nothing'
    Action_Execute = 'Run action'
    Action_ObsFormat = 'OBS ({0})'
    ActionError_Message = '{0}: {1}'
    ActionError_TargetUnavailable = 'The configured file, folder, or program is unavailable: {0}'
    ActionError_AudioSessionUnavailable = '{0} has no active Windows audio session. Start audio playback in the application and try again.'
    ActionError_ObsUnavailable = 'OBS is not connected. Start OBS and check the WebSocket settings.'
    ActionError_ObsActionRejected = 'OBS rejected the configured action. Check its scene, source, or input.'
    ActionError_AudioFileUnsupported = 'Windows could not play this audio file. Check its format or codec: {0}'
    ActionError_MacroEmpty = 'Enter at least one keyboard shortcut.'
    ActionError_MacroInvalidLine = 'Line {0} has an invalid format. Use a shortcut such as Ctrl+Shift+S or a delay such as wait 250.'
    ActionError_MacroTooManySteps = 'A macro cannot contain more than {0} steps.'
    Field_Address = 'Address'
    Field_ProgramFileFolder = 'Program, file, or folder'
    Field_Scene = 'Scene'
    Field_Source = 'Source'
    Obs_Connected = 'OBS connected'
    Obs_Connecting = 'Connecting to OBS…'
    Obs_Reconnecting = 'Reconnecting to OBS…'
    Obs_ReconnectingError = 'Reconnecting to OBS: {0}'
    Obs_ConnectionFailed = 'Could not connect to OBS'
    Obs_Error = 'OBS: {0}'
    Obs_Disconnected = 'OBS disconnected'
    Obs_TestResult = 'Connected. Scenes: {0}, sources: {1}.'
    Settings_Saved = 'Settings saved'
    Tray_Open = 'Open StreamNumDeck'
    Tray_Capture = 'Key capture'
    Tray_Profiles = 'Profiles'
    Tray_ActionFailed = 'Could not complete the tray action'
    Tray_Exit = 'Exit'
    Profile_DefaultName = 'Main profile'
    Profile_New = 'New profile'
    Profile_Edit = 'Edit profile'
    Profile_Duplicate = 'Duplicate profile'
    Profile_Delete = 'Delete profile'
    Profile_ContextHint = "{0}`nRight-click for profile options"
    Profile_DeleteTitle = 'Delete profile?'
    Profile_DeleteMessage = 'The profile “{0}” and all its assignments will be deleted.'
    Profile_CopySuffix = ' — copy'
    Profile_NumberedCopySuffix = ' — copy {0}'
    Profile_NameRequired = 'Enter a profile name.'
    Profile_NameExists = 'A profile with this name already exists.'
    Error_LoadProfiles = 'Could not load profiles'
    Error_ChangeProfile = 'Could not change profile'
    Error_ProfileNotFound = 'The selected profile no longer exists.'
    Error_EditProfile = 'Could not edit profile'
    Error_CreateProfile = 'Could not create profile'
    Error_DuplicateProfile = 'Could not duplicate profile'
    Error_DeleteProfile = 'Could not delete profile'
    Error_LoadSettings = 'Could not load settings'
    Error_SaveSettings = 'Could not save settings'
    Error_ShowOverlay = 'Could not show the microphone indicator'
    Error_OpenSupportLink = 'Could not open the support page'
    Error_NoUriHandler = 'No application is available to open this link.'
    Error_SaveAssignment = 'Could not save assignment'
    Error_ImportIcon = 'Could not import the selected icon'
    Error_ToggleNumLock = 'Could not switch the NumLock layer'
    Error_ChangeCapture = 'Could not change the key capture state'
    Error_GetAudioSessions = 'Could not get Windows audio sessions: {0}'
    Error_VolumeStepRequired = 'Specify a volume change step.'
    Error_AutostartPolicyEnabled = 'Startup is enabled by Windows policy and cannot be disabled by the application.'
    Error_AutostartDisabledByUser = 'Startup was previously disabled by the user. Allow StreamNumDeck in Windows Settings → Apps → Startup.'
    Error_AutostartPolicyDisabled = 'Startup is disabled by Windows policy.'
    Error_AutostartEnable = 'Windows could not enable StreamNumDeck at startup.'
}

# Russian is the source UI language and is maintained manually.  Never send these
# strings through machine translation: x:Uid resources override the Russian text
# kept in XAML, so a literal translation here would degrade the visible interface.
$russianStrings = [ordered]@{
    AppDisplayName = 'StreamNumDeck'
    AppDescription = 'Панель управления для стримеров и геймеров'
    'Nav_NewProfile.Content' = 'Новый профиль'
    'Nav_NewProfile.ToolTipService.ToolTip' = 'Новый профиль'
    'Nav_Settings.Content' = 'Настройки'
    'ProfileDialog.CloseButtonText' = 'Отмена'
    'ProfileDialog.PrimaryButtonText' = 'Создать'
    'ProfileDialog.Title' = 'Новый профиль'
    'ProfileName.Header' = 'Название'
    'ProfileName.PlaceholderText' = 'Например, Игровой стрим'
    'ProfileIcon.Text' = 'Иконка'
    'Settings_Title.Text' = 'Глобальные настройки'
    'Settings_Subtitle.Text' = 'Звук, OBS Studio и поведение приложения'
    'Common_SaveButton.Content' = 'Сохранить'
    'Settings_SoundSection.Text' = 'Звук'
    'Settings_OutputDevice.Header' = 'Устройство вывода'
    Audio_SystemDefaultDevice = 'Системное устройство по умолчанию'
    'Settings_MasterVolume.Header' = 'Общая громкость'
    'Settings_ConcurrentSounds.Header' = 'Разрешить одновременные звуки'
    'Settings_PreloadSounds.Header' = 'Предзагружать короткие звуки'
    'Settings_SystemSection.Text' = 'Система'
    'Settings_StartWithWindows.Header' = 'Запускать при загрузке Windows'
    'Settings_MinimizeToTray.Header' = 'Сворачивать в область уведомлений'
    'Settings_CaptureOnStartup.Header' = 'Включать перехват при запуске'
    'Settings_Theme.Header' = 'Оформление'
    'Theme_System.Content' = 'Как в Windows'
    'Theme_Light.Content' = 'Светлое'
    'Theme_Dark.Content' = 'Тёмное'
    'Settings_PositionOverlay.Content' = 'Расположить индикатор микрофона'
    'Settings_OverlayHint.Text' = 'После нажатия перетащите индикатор мышью. Позиция сохранится.'
    'Settings_KeyboardSection.Text' = 'Клавиатура'
    'Settings_EmergencyPause.Header' = 'Включить/выключить перехват клавиш'
    'Settings_NumLockHint.Text' = 'Физическая клавиша NumLock переключает два слоя назначений.'
    'Settings_ObsSection.Text' = 'OBS Studio'
    'Settings_ObsAddress.Header' = 'Адрес'
    'Settings_ObsPort.Header' = 'Порт'
    'Settings_ObsPassword.Header' = 'Пароль'
    'Settings_ObsPassword.PlaceholderText' = 'Оставьте пустым, чтобы сохранить текущий'
    'Settings_PasswordStored.Text' = 'Пароль сохранён в защищённом хранилище Windows'
    'Settings_TestObs.Content' = 'Проверить подключение'
    'Settings_SupportSection.Text' = 'Поддержка автора'
    'Settings_SupportHint.Text' = 'Если StreamNumDeck оказался полезен, вы можете поддержать его развитие.'
    'Settings_SupportButton.Content' = 'Поддержать на DonationAlerts'
    'Deck_Capture.Text' = 'Перехват'
    'Deck_CaptureToggle.AutomationProperties.Name' = 'Перехват клавиатуры'
    'Deck_CaptureToggle.ToolTipService.ToolTip' = 'Ctrl+Alt+F12 — аварийная пауза'
    'Deck_ActionError.Title' = 'Действие не выполнено'
    'Deck_NumLock.ToolTipService.ToolTip' = 'Переключить слой NumLock'
    'Editor_Dialog.CloseButtonText' = 'Отмена'
    'Editor_Dialog.PrimaryButtonText' = 'Сохранить'
    'Editor_Dialog.SecondaryButtonText' = 'Очистить'
    'Editor_Appearance.Text' = 'Внешний вид'
    'Editor_CustomIcon.Content' = 'Выбрать свою иконку…'
    'Editor_Label.Header' = 'Подпись'
    'Editor_Label.PlaceholderText' = 'Необязательно'
    'Editor_CopyOtherLayer.Content' = 'Скопировать назначение из другого слоя'
    'Editor_ActionSection.Text' = 'Действие'
    'Editor_ActionGroup.Header' = 'Группа'
    'Editor_ActionType.Header' = 'Действие'
    'Editor_AudioFile.Header' = 'Аудиофайл'
    'Common_BrowseButton.Content' = 'Обзор…'
    'Editor_PreviewSound.Content' = 'Прослушать'
    'Common_StopButton.Content' = 'Стоп'
    'Editor_Volume.Header' = 'Громкость'
    'Editor_Behavior.Header' = 'Поведение'
    'Editor_Application.Header' = 'Приложение'
    'Editor_Application.PlaceholderText' = 'Выберите приложение или введите имя .exe'
    'Editor_VolumeStep.Header' = 'Шаг изменения, %'
    'Editor_AudioSessionHint.Text' = 'В списке отображаются приложения, для которых Windows уже создала аудиосессию.'
    'Editor_ObsTarget.Header' = 'Сцена или источник'
    'Editor_ObsTarget.PlaceholderText' = 'Точное имя в OBS'
    'Editor_SystemTarget.Header' = 'Программа, путь или адрес'
    'Editor_Arguments.Header' = 'Аргументы'
    'Editor_Arguments.PlaceholderText' = 'Необязательно'
    'Editor_WorkingDirectory.Header' = 'Рабочая папка'
    'Editor_WorkingDirectory.PlaceholderText' = 'Необязательно'
    'Editor_MacroHint.Text' = 'Каждый шаг вводится с новой строки. Сочетание: Ctrl+Shift+S. Пауза: wait 250. Строки с # считаются комментариями.'
    'Editor_MacroSequence.Header' = 'Последовательность'
    Common_On = 'включён'
    Common_Off = 'выключен'
    Common_Save = 'Сохранить'
    Common_Create = 'Создать'
    Common_Delete = 'Удалить'
    Common_Cancel = 'Отмена'
    Common_Close = 'Закрыть'
    Common_Minimize = 'Свернуть'
    FileFilter_Audio = 'Аудиофайлы'
    FileFilter_Images = 'Изображения'
    FileFilter_AllFiles = 'Все файлы'
    ActionGroup_None = 'Без действия'
    ActionGroup_Sound = 'Звук'
    ActionGroup_System = 'Система и команды'
    SoundBehavior_Alongside = 'Воспроизводить одновременно'
    SoundBehavior_Restart = 'Перезапускать этот звук'
    SoundBehavior_StopOthers = 'Останавливать другие звуки'
    Editor_Title = '{0} · NumLock {1}'
    Action_PlaySound = 'Воспроизвести звук'
    Action_ToggleMicrophoneMute = 'Включить или выключить микрофон по умолчанию'
    Action_ToggleMasterMute = 'Включить или выключить общий звук'
    Action_AdjustMasterVolume = 'Изменить общую громкость'
    Action_AdjustApplicationVolume = 'Изменить громкость приложения'
    Action_IncreaseMasterVolume = 'Увеличить общую громкость'
    Action_DecreaseMasterVolume = 'Уменьшить общую громкость'
    Action_IncreaseApplicationVolume = 'Увеличить громкость приложения'
    Action_DecreaseApplicationVolume = 'Уменьшить громкость приложения'
    Action_ObsSwitchScene = 'Переключить сцену'
    Action_ObsToggleSource = 'Показать или скрыть источник'
    Action_ObsToggleMute = 'Включить или выключить звук источника'
    Action_ObsStartStream = 'Начать трансляцию'
    Action_ObsStopStream = 'Остановить трансляцию'
    Action_ObsStartRecord = 'Начать запись'
    Action_ObsStopRecord = 'Остановить запись'
    Action_ObsSaveReplay = 'Сохранить повтор'
    Action_ObsRestartMedia = 'Перезапустить медиаисточник'
    Action_LaunchProcess = 'Запустить программу'
    Action_OpenPath = 'Открыть файл или папку'
    Action_OpenUri = 'Открыть ссылку'
    Action_KeyboardMacro = 'Выполнить клавиатурный макрос'
    Action_None = 'Ничего не делать'
    Action_Execute = 'Выполнить действие'
    Action_ObsFormat = 'OBS ({0})'
    ActionError_Message = '{0}: {1}'
    ActionError_TargetUnavailable = 'Указанный файл, папка или программа недоступны: {0}'
    ActionError_AudioSessionUnavailable = 'У приложения {0} нет активной аудиосессии Windows. Запустите в нём воспроизведение звука и повторите попытку.'
    ActionError_ObsUnavailable = 'OBS не подключён. Запустите OBS и проверьте настройки WebSocket.'
    ActionError_ObsActionRejected = 'OBS отклонил настроенное действие. Проверьте выбранную сцену, источник или аудиовход.'
    ActionError_AudioFileUnsupported = 'Windows не удалось воспроизвести аудиофайл. Проверьте его формат или кодек: {0}'
    ActionError_MacroEmpty = 'Введите хотя бы одно сочетание клавиш.'
    ActionError_MacroInvalidLine = 'Строка {0} имеет неверный формат. Используйте сочетание вроде Ctrl+Shift+S или задержку вида wait 250.'
    ActionError_MacroTooManySteps = 'Макрос не может содержать больше {0} шагов.'
    Field_Address = 'Адрес'
    Field_ProgramFileFolder = 'Программа, файл или папка'
    Field_Scene = 'Сцена'
    Field_Source = 'Источник'
    Obs_Connected = 'OBS подключён'
    Obs_Connecting = 'Подключение к OBS…'
    Obs_Reconnecting = 'Повторное подключение к OBS…'
    Obs_ReconnectingError = 'Повторное подключение к OBS: {0}'
    Obs_ConnectionFailed = 'Не удалось подключиться к OBS'
    Obs_Error = 'OBS: {0}'
    Obs_Disconnected = 'OBS отключён'
    Obs_TestResult = 'Подключено. Сцен: {0}, источников: {1}.'
    Settings_Saved = 'Настройки сохранены'
    Tray_Open = 'Открыть StreamNumDeck'
    Tray_Capture = 'Перехват клавиатуры'
    Tray_Profiles = 'Профили'
    Tray_ActionFailed = 'Не удалось выполнить действие из области уведомлений'
    Tray_Exit = 'Выход'
    Profile_DefaultName = 'Основной профиль'
    Profile_New = 'Новый профиль'
    Profile_Edit = 'Изменить профиль'
    Profile_Duplicate = 'Дублировать профиль'
    Profile_Delete = 'Удалить профиль'
    Profile_ContextHint = "{0}`nЩёлкните правой кнопкой мыши для действий с профилем"
    Profile_DeleteTitle = 'Удалить профиль?'
    Profile_DeleteMessage = 'Профиль «{0}» и все его назначения будут удалены.'
    Profile_CopySuffix = ' — копия'
    Profile_NumberedCopySuffix = ' — копия {0}'
    Profile_NameRequired = 'Введите название профиля.'
    Profile_NameExists = 'Профиль с таким названием уже существует.'
    Error_LoadProfiles = 'Не удалось загрузить профили'
    Error_ChangeProfile = 'Не удалось сменить профиль'
    Error_ProfileNotFound = 'Выбранный профиль больше не существует.'
    Error_EditProfile = 'Не удалось изменить профиль'
    Error_CreateProfile = 'Не удалось создать профиль'
    Error_DuplicateProfile = 'Не удалось дублировать профиль'
    Error_DeleteProfile = 'Не удалось удалить профиль'
    Error_LoadSettings = 'Не удалось загрузить настройки'
    Error_SaveSettings = 'Не удалось сохранить настройки'
    Error_ShowOverlay = 'Не удалось показать индикатор'
    Error_OpenSupportLink = 'Не удалось открыть страницу поддержки'
    Error_NoUriHandler = 'В системе нет приложения для открытия этой ссылки.'
    Error_SaveAssignment = 'Не удалось сохранить назначение'
    Error_ImportIcon = 'Не удалось импортировать выбранную иконку'
    Error_ToggleNumLock = 'Не удалось переключить NumLock'
    Error_ChangeCapture = 'Не удалось изменить состояние перехвата'
    Error_GetAudioSessions = 'Не удалось получить аудиосессии Windows: {0}'
    Error_VolumeStepRequired = 'Укажите шаг изменения громкости.'
    Error_AutostartPolicyEnabled = 'Автозапуск включён политикой Windows, поэтому приложение не может его отключить.'
    Error_AutostartDisabledByUser = 'Ранее вы отключили автозапуск. Разрешите StreamNumDeck в разделе «Параметры Windows → Приложения → Автозагрузка».'
    Error_AutostartPolicyDisabled = 'Автозапуск отключён политикой Windows.'
    Error_AutostartEnable = 'Windows не удалось включить автозапуск StreamNumDeck.'
}

$catalogPath = Join-Path $root 'src\StreamNumDeck.App\Presentation\BuiltInIconCatalog.cs'
$catalog = Get-Content -LiteralPath $catalogPath -Raw
[regex]::Matches($catalog, 'new\("([^"]+)",\s*"([^"]+)",') | ForEach-Object {
    $id = $_.Groups[1].Value
    if ($id -eq 'blank') {
        return
    }

    $key = 'Icon_' + $id.Replace('-', '_')
    $russianStrings[$key] = $_.Groups[2].Value
    if (-not $strings.Contains($key)) {
        $words = ($id -split '-') | ForEach-Object {
            if ($_ -in @('usb', 'wifi', 'xbox')) { $_.ToUpperInvariant() }
            else { (Get-Culture).TextInfo.ToTitleCase($_) }
        }
        $strings[$key] = $words -join ' '
    }
}
$strings['Icon_square'] = 'No icon'

# Generated icon labels are only a fallback. Keep labels that need punctuation,
# sentence casing, or a more user-friendly name explicitly curated in English.
$englishIconNames = [ordered]@{
    Icon_check = 'Check mark'
    Icon_command = 'Command prompt'
    Icon_info = 'Information'
    Icon_fast_forward = 'Fast-forward'
    Icon_fullscreen = 'Full screen'
    Icon_window = 'Windowed mode'
    Icon_volume_off = 'Volume off'
    Icon_volume_low = 'Low volume'
    Icon_volume_medium = 'Medium volume'
    Icon_volume_high = 'High volume'
    Icon_video_chat = 'Video chat'
    Icon_wifi = 'Wi-Fi'
    Icon_folder_open = 'Open folder'
    Icon_star_fill = 'Filled star'
    Icon_heart_fill = 'Filled heart'
    Icon_xbox = 'Xbox'
    Icon_map_pin = 'Map pin'
}
foreach ($entry in $englishIconNames.GetEnumerator()) {
    $strings[$entry.Key] = $entry.Value
}

$locales = [ordered]@{
    'en-US' = $null
    'ru-RU' = 'ru'
    'de-DE' = 'de'
    'fr-FR' = 'fr'
    'es-ES' = 'es'
    'pt-BR' = 'pt'
    'it-IT' = 'it'
    'pl-PL' = 'pl'
    'uk-UA' = 'uk'
    'tr-TR' = 'tr'
    'nl-NL' = 'nl'
    'sv-SE' = 'sv'
    'cs-CZ' = 'cs'
    'ro-RO' = 'ro'
    'zh-CN' = 'zh-CN'
    'ja-JP' = 'ja'
    'ko-KR' = 'ko'
    'hi-IN' = 'hi'
    'ar-SA' = 'ar'
    'id-ID' = 'id'
    'vi-VN' = 'vi'
    'th-TH' = 'th'
}

function Write-Resw([string] $path, [System.Collections.IDictionary] $values) {
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $writer = [System.Xml.XmlWriter]::Create($path, $settings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('root')
        foreach ($header in ([ordered]@{
            resmimetype = 'text/microsoft-resx'
            version = '2.0'
            reader = 'System.Resources.ResXResourceReader, System.Windows.Forms'
            writer = 'System.Resources.ResXResourceWriter, System.Windows.Forms'
        }).GetEnumerator()) {
            $writer.WriteStartElement('resheader')
            $writer.WriteAttributeString('name', $header.Key)
            $writer.WriteElementString('value', $header.Value)
            $writer.WriteEndElement()
        }
        foreach ($entry in $values.GetEnumerator()) {
            $writer.WriteStartElement('data')
            $writer.WriteAttributeString('name', [string]$entry.Key)
            $writer.WriteAttributeString('xml', 'space', $null, 'preserve')
            $writer.WriteElementString('value', [string]$entry.Value)
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteWhitespace($settings.NewLineChars)
        $writer.WriteEndDocument()
    }
    finally {
        $writer.Dispose()
    }
}

function Translate-Batch([string[]] $texts, [string] $targetLanguage) {
    $separator = "`n<<<SND_RESOURCE_SEPARATOR>>>`n"
    $source = $texts -join $separator
    $uri = 'https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=' +
        [uri]::EscapeDataString($targetLanguage) + '&dt=t&q=' + [uri]::EscapeDataString($source)
    $response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 45
    $translated = (($response[0] | ForEach-Object { $_[0] }) -join '')
    $parts = $translated -split '\r?\n<<<SND_RESOURCE_SEPARATOR>>>\r?\n'
    if ($parts.Count -ne $texts.Count) {
        throw "Translation service returned $($parts.Count) items instead of $($texts.Count) for $targetLanguage."
    }
    return $parts
}

New-Item -ItemType Directory -Force -Path $stringsRoot | Out-Null
foreach ($locale in $locales.GetEnumerator()) {
    $values = [ordered]@{}
    if ($null -eq $locale.Value) {
        foreach ($entry in $strings.GetEnumerator()) { $values[$entry.Key] = $entry.Value }
    }
    elseif ($locale.Key -eq 'ru-RU') {
        $missingRussianKeys = @($strings.Keys | Where-Object { -not $russianStrings.Contains($_) })
        if ($missingRussianKeys.Count -gt 0) {
            throw "The curated Russian catalog is missing: $($missingRussianKeys -join ', ')"
        }

        foreach ($entry in $strings.GetEnumerator()) {
            $values[$entry.Key] = $russianStrings[$entry.Key]
        }
    }
    else {
        $entries = @($strings.GetEnumerator())
        $existing = @{}
        $existingPath = Join-Path (Join-Path $stringsRoot $locale.Key) 'Resources.resw'
        if (Test-Path -LiteralPath $existingPath) {
            [xml]$existingXml = Get-Content -LiteralPath $existingPath
            foreach ($data in $existingXml.root.data) { $existing[[string]$data.name] = [string]$data.value }
        }
        $missing = @($entries | Where-Object {
            -not $existing.ContainsKey($_.Key) -or $RegenerateKey -contains $_.Key
        })
        for ($offset = 0; $offset -lt $missing.Count; $offset += 24) {
            $last = [Math]::Min($offset + 23, $missing.Count - 1)
            $batch = $missing[$offset..$last]
            $translated = @(Translate-Batch @($batch | ForEach-Object Value) $locale.Value)
            for ($index = 0; $index -lt $batch.Count; $index++) {
                $existing[$batch[$index].Key] = $translated[$index]
            }
            Start-Sleep -Milliseconds 100
        }
        foreach ($entry in $entries) { $values[$entry.Key] = $existing[$entry.Key] }
    }

    $values['Profile_ContextHint'] = $values['Profile_ContextHint'].Replace('`n', "`n")

    $directory = Join-Path $stringsRoot $locale.Key
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Write-Resw (Join-Path $directory 'Resources.resw') $values
    Write-Host "Generated $($locale.Key): $($values.Count) resources"
}
