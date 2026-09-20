using System.Collections.Generic;

namespace SircleToSearch;

public static class Strings
{
    private static readonly Dictionary<string, (string En, string Ru)> Table = new()
    {
        ["OverlayHint"] = ("Drag a rectangle around what you want to search - Enter searches again, Esc closes",
                            "Тяни прямоугольник, двигай и меняй размер - Enter ищёт снова, Esc закрывает"),
        ["TrayTooltip"] = ("SircleToSearch - {0}", "SircleToSearch - {0}"),
        ["MenuSettings"] = ("Settings", "Настройки"),
        ["MenuAutostart"] = ("Launch with Windows", "Запускать со стартом Windows"),
        ["MenuFindNow"] = ("Search now ({0})", "Найти сейчас ({0})"),
        ["MenuExit"] = ("Exit", "Выход"),
        ["ResultHeaderIdle"] = ("Search with SircleToSearch", "Найти с SircleToSearch"),
        ["ResultHeaderSearching"] = ("Searching...", "Ищу..."),
        ["ResultErrorCaptcha"] = ("Google is asking for a captcha - wait a moment and try again.",
                                   "Google просит капчу - подожди немного и попробуй снова."),
        ["ResultErrorGeneric"] = ("Search failed - try again.", "Поиск не удался - попробуй ещё раз."),
        ["AlreadyRunning"] = ("SircleToSearch is already running - check the tray.",
                               "SircleToSearch уже запущен - смотри в трее."),
        ["HotkeyFailed"] = ("Couldn't register Win+Shift+Q - another program is already using it (Win32 error: {0}).",
                             "Не удалось зарегистрировать Win+Shift+Q - хоткей уже занят другой программой (код ошибки Win32: {0})."),
        ["AutostartFailed"] = ("Couldn't update autostart in the registry.", "Не удалось изменить автозагрузку в реестре."),

        ["SettingsTitle"] = ("SircleToSearch Settings", "Настройки SircleToSearch"),
        ["SettingsWelcome"] = ("Welcome! SircleToSearch runs in the tray and activates with Win+Shift+Q.",
                                "Добро пожаловать! SircleToSearch сидит в трее и активируется по Win+Shift+Q."),
        ["SettingsLanguage"] = ("Language", "Язык"),
        ["SettingsAutostart"] = ("Launch with Windows", "Запускать со стартом Windows"),
        ["SettingsSearchEngine"] = ("Search engine", "Поисковик"),
        ["SettingsSearchEngineGoogle"] = ("Google (recommended)", "Google (рекомендуется)"),
        ["SettingsFastSearch"] = ("Fast search", "Быстрый поиск"),
        ["SettingsFastSearchHint"] = ("Quicker, but Google may occasionally show a captcha",
                                       "Быстрее, но Google иногда может попросить капчу"),
        ["SettingsReportBug"] = ("Report a bug", "Пожаловаться на баг"),
        ["SettingsHotkey"] = ("Hotkey", "Хоткей"),
        ["SettingsHotkeyRecording"] = ("Press a new combo... (Esc to cancel)", "Нажми новую комбинацию... (Esc - отмена)"),
        ["SettingsHotkeyNeedsModifier"] = ("Needs at least one modifier (Ctrl/Alt/Shift/Win)", "Нужен хотя бы один модификатор (Ctrl/Alt/Shift/Win)"),
        ["SettingsHotkeyConflict"] = ("That combo is already taken by another app", "Эта комбинация уже занята другой программой"),
        ["SettingsClose"] = ("Done", "Готово"),
        ["SettingsVersion"] = ("Version {0}", "Версия {0}"),
        ["SettingsCheckUpdate"] = ("Check for updates", "Проверить обновления"),
        ["SettingsCheckingUpdate"] = ("Checking...", "Проверяю..."),
        ["SettingsUpToDate"] = ("You're on the latest version", "У тебя последняя версия"),
        ["SettingsUpdateAvailable"] = ("Version {0} is available", "Доступна версия {0}"),
        ["SettingsUpdateNow"] = ("Update now", "Обновить сейчас"),
        ["SettingsDownloadingUpdate"] = ("Downloading update... {0}%", "Скачиваю обновление... {0}%"),
        ["SettingsDownload"] = ("Download", "Скачать"),
        ["SettingsUpdateCheckFailed"] = ("Couldn't check for updates", "Не удалось проверить обновления"),
        ["SettingsAutoUpdate"] = ("Update automatically", "Обновлять автоматически"),
        ["SettingsNavGeneral"] = ("General", "Общее"),
        ["SettingsNavSearch"] = ("Search", "Поиск"),
        ["SettingsNavSelection"] = ("Selection", "Выделение"),
        ["SettingsNavUpdates"] = ("Updates", "Обновления"),
        ["SettingsNavAbout"] = ("About", "О программе"),
        ["SettingsSelectionHint"] = ("How the selection frame looks while you drag", "Как выглядит рамка выделения, пока тянешь область"),
        ["SettingsSelectionBracketsSection"] = ("Corner brackets", "Скобки по углам"),
        ["SettingsSelectionGlowSection"] = ("Glow", "Свечение"),
        ["SettingsSelectionAreaSection"] = ("Area", "Область"),
        ["SettingsSelectionHug"] = ("Distance from edge", "Отступ от края"),
        ["SettingsSelectionCornerRadius"] = ("Corner rounding", "Скругление угла"),
        ["SettingsSelectionArmLength"] = ("Arm length", "Длина плеч"),
        ["SettingsSelectionThickness"] = ("Thickness", "Толщина"),
        ["SettingsSelectionGlowGap"] = ("Gap from area", "Зазор от области"),
        ["SettingsSelectionGlowThickness"] = ("Glow thickness", "Толщина свечения"),
        ["SettingsSelectionGlowBlur"] = ("Blur", "Размытие"),
        ["SettingsSelectionGlowOpacity"] = ("Opacity", "Прозрачность"),
        ["SettingsSelectionRadius"] = ("Area rounding", "Скругление области"),
        ["SettingsSelectionReset"] = ("Reset to defaults", "Сбросить по умолчанию"),
        ["SettingsAccentColor"] = ("Accent color", "Акцентный цвет"),
        ["SettingsAccentHexApply"] = ("Apply", "Применить"),
        ["SettingsAccentHexInvalid"] = ("That doesn't look like a color (try #009FAA)", "Не похоже на цвет (пример: #009FAA)"),
        ["UpdateBalloonText"] = ("Version {0} is available - click to update", "Доступна версия {0} - нажми, чтобы обновить"),
        ["UrgentUpdateInstalled"] = ("Update with critical fixes successfully installed", "Обновление с критическими исправлениями успешно установлено"),
        ["UpdatePromptQuestion"] = ("Update to version {0}?", "Обновить до версии {0}?"),
        ["UpdatePromptHint"] = ("It'll install right after you finish and close the current search (plus a few seconds).",
                                 "Установится сразу после того, как ты закроешь текущий поиск (плюс пара секунд)."),
        ["UpdatePromptAutoUpdate"] = ("Update automatically from now on", "Обновлять автоматически в будущем"),
        ["UpdatePromptLater"] = ("Later", "Позже"),
        ["UpdatePromptUpdate"] = ("Update", "Обновить"),
        ["SettingsGitHub"] = ("GitHub", "GitHub"),
        ["SettingsAuthor"] = ("Author", "Автор"),
        ["SettingsBySomeone"] = ("by Kiki", "от Kiki"),
    };

    public static string Get(string key)
    {
        if (!Table.TryGetValue(key, out var pair)) return key;
        return AppSettings.Current.Language == "ru" ? pair.Ru : pair.En;
    }

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
