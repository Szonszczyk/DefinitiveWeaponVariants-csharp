namespace ModConfigEditor;

/// <summary>
/// The only mod-specific settings in the reusable folder. The Razor route and
/// ModMetadata both reference Route, so changing it here changes both links.
/// Namespaces can remain unchanged when compiling this folder into another DLL.
/// </summary>
public static class ModEditorSettings
{
    public const string Route = "/definitive-weapon-variants/config";
    public const string Title = "Definitive Weapon Variants";
    public const string Description = "Configure weapon variants, loot, variant cores, and integrations.";
    public const string ConfigFile = "config.jsonc";
    public const string DefaultFile = "defaultConfig.jsonc";
    public const string AuthorizationPolicy = "Administrator";

    // Matches SPT's default static-asset prefix when WWWRootUrl is null. If your
    // metadata overrides WWWRootUrl, use that same prefix here instead.
    // A new URL after replacing the installed CSS prevents reuse of a cached theme.
    public static string ThemeUrl => $"/{Uri.EscapeDataString(typeof(ModEditorSettings).Assembly.GetName().Name!)}/config-editor-theme.css?v={File.GetLastWriteTimeUtc(Path.Combine(ModDirectory, "wwwroot", "config-editor-theme.css")).Ticks}";
    public static string ThumbnailUrl => $"/{Uri.EscapeDataString(typeof(ModEditorSettings).Assembly.GetName().Name!)}/thumbnail.png";

    // typeof anchors the path to THIS mod's DLL, not SPT's working directory or
    // a folder name that players may rename. No browser-supplied paths are used.
    private static string ModDirectory => Path.GetDirectoryName(typeof(ModEditorSettings).Assembly.Location)
        ?? throw new InvalidOperationException("Cannot locate the mod DLL.");
    public static string ConfigDirectory => Path.Combine(ModDirectory, "config");
}
