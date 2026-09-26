using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using System.Text.RegularExpressions;

namespace DefinitiveWeaponVariants.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomLocales(
        CustomLogger logger,
        ModDatabaseLoader modDatabaseLoader,
        LocaleTable localeTable,
        LocaleService localeService
    )
    {
        public readonly Dictionary<string, Dictionary<string, string>> newLocale = [];

        private readonly Dictionary<string, string> temp = [];

        private readonly Dictionary<string, Dictionary<string, string>> OriginalLocale = [];
        private HashSet<string> AllLangs { get; set; } = [];
        public void Initialize()
        {
            AllLangs = [.. localeTable.Global.Keys];

            foreach (var (lang, _) in modDatabaseLoader.DbLocales)
            {
                OriginalLocale.Add(lang, localeService.GetLocaleDb(lang));
                newLocale.Add(lang, []);
            }
        }

        public void RegisterTag(string key, string value)
        {
            temp[key] = value;
        }
        public bool KeyExistsInDefaultLang(string key)
        {
            if (TryGetLocaleText("en", key) is not null) { return true; } else return false;
        }

        public bool DefaultLangLocaleContainsText(string key, string value)
        {
            newLocale.TryGetValue("en", out var language);
            if (language != null)
            {
                language.TryGetValue(key, out var text);
                if (text != null)
                {
                    return text.Contains(value);
                }
            }
            return false;
        }

        public void AddLocale(string localeKey, string key, bool clean = false)
        {
            var langs = modDatabaseLoader.DbLocales;
            
            foreach (var (lang, _) in langs)
            {
                var text = TryGetLocaleText(lang, key);
                text ??= key;
                text = ReplaceTags(text, lang);
                var newL = newLocale[lang];
                if (newL.TryGetValue(localeKey, out _))
                    newL[localeKey] = clean ? StripHtml(text) : text;
                else
                    newL.Add(localeKey, clean ? StripHtml(text) : text);
                //logger.LogWithColor($"[{GetType().Namespace}] Registred {lang}-{localeKey}: {key} => {text}", LogTextColor.Yellow);
            }
        }

        private string ReplaceTags(string text, string lang)
        {
            while (true)
            {
                var tags = ExtractTags(text);
                if (tags.Count == 0)
                    break;

                foreach (var tag in tags)
                {
                    var tagText = TryGetLocaleText(lang, tag);

                    if (tagText is null)
                    {
                        logger.Warning($"Tag not found: {tag}, Language: {lang}");
                        tagText = tag;
                    }

                    text = text.Replace($"{{{tag}}}", tagText);
                }
            }

            return text;
        }

        public void AddToExistingLocale(string localeKey, string key, bool clean = false)
        {
            AddLocale(localeKey, $"{{{localeKey}}}{key}", clean);
        }

        public void RegisterLocales()
        {
            foreach (var langId in AllLangs)
            {
                if (localeTable is not null && localeTable.Global.TryGetValue(langId, out var lazyloadedValue))
                {
                    newLocale.TryGetValue(langId, out var newLocaleToAdd);
                    if (newLocaleToAdd is null)
                        newLocale.TryGetValue("en", out newLocaleToAdd);

                    if (newLocaleToAdd is null) continue;

                    lazyloadedValue.AddTransformer(lazyloadedLocaleData =>
                    {
                        if (lazyloadedLocaleData is null) return lazyloadedLocaleData;
                        foreach (var (key, value) in newLocaleToAdd)
                        {
                            lazyloadedLocaleData[key] = value;
                        }
                        return lazyloadedLocaleData;
                    });
                }
            }
        }

        private string? TryGetLocaleText(string lang, string key)
        {
            var sources = new[]
            {
                temp,
                newLocale.GetValueOrDefault(lang),
                modDatabaseLoader.DbLocales.GetValueOrDefault(lang),
                OriginalLocale.GetValueOrDefault(lang),
                newLocale.GetValueOrDefault("en"),
                modDatabaseLoader.DbLocales.GetValueOrDefault("en"),
                OriginalLocale.GetValueOrDefault("en")
            };

            foreach (var source in sources)
            {
                if (source != null && source.TryGetValue(key, out var text) && text.Length > 0)
                    return text;
            }

            return null;
        }
        private static List<string> ExtractTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<string>();

            var matches = Regex.Matches(input, @"{([^{}]+)}");

            return matches
                .Select(m => m.Groups[1].Value.Trim())
                .Distinct()
                .ToList();
        }
        private static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public Dictionary<string, LocaleDetails> CreateItemLocale(string Name, string ShortName, string Description, string id)
        {
            var itemLocale = new Dictionary<string, LocaleDetails>();

            foreach (var (langId, _) in newLocale)
            {
                var newItemLocale = new LocaleDetails
                {
                    Name = ReplaceTags(Name, langId),
                    ShortName = ReplaceTags(ShortName, langId),
                    Description = ReplaceTags(Description, langId)
                };
                OriginalLocale[langId].Add($"{id} Name", newItemLocale.Name);
                OriginalLocale[langId].Add($"{id} ShortName", newItemLocale.ShortName);
                OriginalLocale[langId].Add($"{id} Description", newItemLocale.Description);
                itemLocale.Add(langId, newItemLocale);
            }
            return itemLocale;
        }

    }
}
