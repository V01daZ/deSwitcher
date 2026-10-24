using System.Collections.Generic;
using System.Globalization;

namespace DeSwitcher.Patcher.Logic
{
    public enum Lang
    {
        Ru,
        En
    }

    public static class Loc
    {
        public static Lang Current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? Lang.Ru : Lang.En;

        private static readonly Dictionary<string, (string Ru, string En)> Table = new Dictionary<string, (string, string)>
        {
            ["subtitle"] = ("Защита от Switch Studio", "Protection by Switch Studio"),
            ["banner"] = ("Безопасные моды скачивайте только с deswitchstudio.com - мы проверяем каждый вручную.", "Download safe mods only from deswitchstudio.com - we check every one by hand."),
            ["home.r1"] = ("Проверяем код каждого мода перед запуском игры", "We check every mod's code before the game starts"),
            ["home.r2"] = ("Опасный мод не загрузится без вашего решения", "A dangerous mod won't load without your say-so"),
            ["home.r3"] = ("Моды с нашего сайта проверены вручную", "Mods from our site are checked by hand"),
            ["mods.off"] = ("Моды выключены", "Mods are off"),
            ["mods.on"] = ("Моды включены", "Mods are on"),
            ["enable"] = ("Включить моды", "Enable mods"),
            ["pick.game"] = ("Папка игры…", "Game folder…"),
            ["guard.on"] = ("Защита активна", "Protection on"),
            ["nav.home"] = ("Главная", "Home"),
            ["nav.mods"] = ("Моды", "Mods"),
            ["nav.quarantine"] = ("Карантин", "Quarantine"),
            ["nav.settings"] = ("Настройки", "Settings"),
            ["tag.site"] = ("с сайта", "from site"),
            ["tag.clean"] = ("чисто", "clean"),
            ["tag.trusted"] = ("доверенный", "trusted"),
            ["tag.suspicious"] = ("подозрительный", "suspicious"),
            ["tag.dangerous"] = ("опасный", "dangerous"),
            ["tag.blocked"] = ("в чёрном списке", "blacklisted"),
            ["scan.none"] = ("Модов не найдено", "No mods found"),
            ["scan.running"] = ("Проверяю моды…", "Checking mods…"),
            ["lastcheck"] = ("Последняя проверка", "Last check"),
            ["remove.patch"] = ("Удалить патч", "Remove patch"),
            ["open.folder"] = ("Открыть папку игры", "Open game folder"),
            ["quarantine.restore"] = ("Вернуть", "Restore"),
            ["quarantine.delete"] = ("Удалить навсегда", "Delete forever"),
            ["quarantine.empty"] = ("В карантине пусто", "Quarantine is empty"),
            ["danger.title"] = ("опасный мод", "dangerous mod"),
            ["danger.body"] = ("Этот мод пытается делать то, что модам для People Playground не нужно. Мы его не загрузили. Так часто выглядят вирусы, которые крадут аккаунты и пароли.", "This mod tries to do things a People Playground mod never needs. We did not load it. Viruses that steal accounts and passwords often look like this."),
            ["danger.quarantine"] = ("В карантин", "Quarantine"),
            ["danger.removeself"] = ("Уберу сам", "I'll remove it"),
            ["danger.trust"] = ("Доверяю…", "Trust anyway…"),
            ["trust.title"] = ("точно доверяете?", "really trust it?"),
            ["trust.body"] = ("Если вы ошибётесь, вирус из этого мода получит доступ ко всему, к чему есть доступ у вас: файлам, паролям, аккаунтам. Делайте это, только если сами написали этот мод или полностью доверяете автору.", "If you are wrong, a virus in this mod gets access to everything you can access: files, passwords, accounts. Do this only if you wrote the mod yourself or fully trust its author."),
            ["trust.confirm"] = ("Всё равно доверяю", "Trust it anyway"),
            ["cancel"] = ("Отмена", "Cancel"),
            ["finding.net"] = ("выход в интернет", "connects to the internet"),
            ["finding.process"] = ("запуск программ", "launches programs"),
            ["finding.fs-outside"] = ("доступ к файлам вне игры", "reads files outside the game"),
            ["finding.registry"] = ("доступ к реестру Windows", "touches the Windows registry"),
            ["finding.pinvoke"] = ("вызов системных библиотек", "calls system libraries"),
            ["finding.asm-load"] = ("загрузка другого кода", "loads other code"),
            ["finding.reflection-dangerous"] = ("скрытые вызовы", "hidden calls"),
            ["finding.env"] = ("чтение данных системы", "reads system data"),
            ["finding.serialization"] = ("опасная распаковка данных", "unsafe data unpacking"),
            ["finding.codegen"] = ("создание кода на лету", "generates code at runtime"),
            ["finding.obfuscation"] = ("запутанный код", "obfuscated code"),
            ["finding.unsafe"] = ("работа с памятью напрямую", "raw memory access"),
            ["finding.unreadable"] = ("повреждённый код", "corrupt code"),
        };

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var pair))
                return Current == Lang.Ru ? pair.Ru : pair.En;
            return key;
        }
    }
}
