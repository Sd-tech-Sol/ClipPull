using System.Globalization;
using System.IO;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace ClipPull.Localization;

internal static class LocalizationService
{
    private const string EnglishDictionary = "pack://application:,,,/ClipPull;component/Resources/Strings.en.xaml";
    private const string FrenchDictionary = "pack://application:,,,/ClipPull;component/Resources/Strings.fr.xaml";

    internal static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "language-preference.txt");

    public static event EventHandler? LanguageChanged;

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    public static CultureInfo CurrentCulture => CurrentLanguage == AppLanguage.French
        ? CultureInfo.GetCultureInfo("fr-CA")
        : CultureInfo.GetCultureInfo("en-CA");

    public static void Initialize(string? preference = null)
    {
        ApplyLanguage(preference is null ? LoadPreference() : ParsePreference(preference), persist: false, notify: false);
    }

    public static void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
            language = AppLanguage.English;

        ApplyLanguage(language, persist: true, notify: language != CurrentLanguage);
    }

    public static string Get(string key, params object[] arguments)
    {
        var template = WpfApplication.Current?.TryFindResource(key) as string ?? key;
        return arguments.Length == 0
            ? template
            : string.Format(CurrentCulture, template, arguments);
    }

    internal static AppLanguage ParsePreference(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out AppLanguage language) && Enum.IsDefined(language)
            ? language
            : AppLanguage.English;

    private static AppLanguage LoadPreference()
    {
        try
        {
            return File.Exists(PreferencePath)
                ? ParsePreference(File.ReadAllText(PreferencePath).Trim())
                : AppLanguage.English;
        }
        catch
        {
            return AppLanguage.English;
        }
    }

    private static void ApplyLanguage(AppLanguage language, bool persist, bool notify)
    {
        CurrentLanguage = language;
        var culture = CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        ReplaceLanguageDictionary(language);

        if (persist)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
                File.WriteAllText(PreferencePath, language.ToString());
            }
            catch
            {
                // A read-only profile should not prevent an in-session language change.
            }
        }

        if (notify)
            LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ReplaceLanguageDictionary(AppLanguage language)
    {
        if (WpfApplication.Current is null)
            return;

        var dictionaries = WpfApplication.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(language == AppLanguage.French ? FrenchDictionary : EnglishDictionary, UriKind.Absolute)
        };

        if (existing is null)
            dictionaries.Insert(0, replacement);
        else
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
    }
}
