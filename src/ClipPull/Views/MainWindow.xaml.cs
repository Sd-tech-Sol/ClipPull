using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipPull.ViewModels;
using ClipPull.Localization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfSystemColors = System.Windows.SystemColors;

namespace ClipPull.Views;

public partial class MainWindow : FluentWindow
{
    private static readonly (string Suffix, string[] BrushKeys)[] PaletteBrushMap =
    [
        ("Background", ["ClipPullBackgroundBrush", "ApplicationBackgroundBrush", "LayerFillColorDefaultBrush"]),
        ("Chrome", ["ClipPullChromeBrush"]),
        ("Surface", ["ClipPullSurfaceBrush", "CardBackgroundFillColorDefaultBrush", "SubtleFillColorTransparentBrush", "ControlFillColorDisabledBrush"]),
        ("SurfaceSecondary", ["ClipPullSurfaceSecondaryBrush", "CardBackgroundFillColorSecondaryBrush", "ControlFillColorDefaultBrush", "SubtleFillColorTertiaryBrush"]),
        ("SurfaceElevated", ["ClipPullSurfaceElevatedBrush", "LayerOnAcrylicFillColorDefaultBrush", "ControlFillColorSecondaryBrush"]),
        ("SurfaceHover", ["ClipPullSurfaceHoverBrush", "ControlFillColorTertiaryBrush", "SubtleFillColorSecondaryBrush"]),
        ("DropZone", ["ClipPullDropZoneBrush"]),
        ("DropZoneActive", ["ClipPullDropZoneActiveBrush"]),
        ("Accent", ["ClipPullAccentBrush", "AccentFillColorDefaultBrush", "AccentButtonBackground", "ToggleSwitchStrokeOn", "ToggleSwitchFillOn", "CheckBoxCheckBackgroundFillChecked", "ToggleButtonBackgroundChecked"]),
        ("AccentHover", ["ClipPullAccentHoverBrush", "AccentFillColorSecondaryBrush", "AccentTextFillColorSecondaryBrush", "AccentButtonBackgroundPointerOver", "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchFillOnPointerOver"]),
        ("AccentPressed", ["ClipPullAccentPressedBrush", "AccentFillColorTertiaryBrush", "AccentButtonBackgroundPressed", "ToggleSwitchStrokeOnPressed", "ToggleSwitchFillOnPressed"]),
        ("Cyan", ["ClipPullCyanBrush", "AccentTextFillColorPrimaryBrush", "FocusStrokeColorOuterBrush"]),
        ("Violet", ["ClipPullVioletBrush"]),
        ("TextPrimary", ["ClipPullTextPrimaryBrush", "TextFillColorPrimaryBrush"]),
        ("TextSecondary", ["ClipPullTextSecondaryBrush", "TextFillColorSecondaryBrush"]),
        ("TextTertiary", ["ClipPullTextTertiaryBrush", "TextFillColorTertiaryBrush"]),
        ("TextDisabled", ["ClipPullTextDisabledBrush", "TextFillColorDisabledBrush"]),
        ("Border", ["ClipPullBorderBrush", "ControlStrokeColorDefaultBrush", "CardStrokeColorDefaultBrush"]),
        ("BorderStrong", ["ClipPullBorderStrongBrush", "ControlStrokeColorSecondaryBrush"]),
        ("DropBorder", ["ClipPullDropBorderBrush"]),
        ("Divider", ["ClipPullDividerBrush", "DividerStrokeColorDefaultBrush"]),
        ("Success", ["ClipPullSuccessBrush", "SystemFillColorSuccessBrush"]),
        ("Warning", ["ClipPullWarningBrush", "SystemFillColorCautionBrush"]),
        ("Error", ["ClipPullErrorBrush", "SystemFillColorCriticalBrush"]),
        ("OnAccent", ["ClipPullOnAccentBrush", "TextOnAccentFillColorPrimaryBrush", "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed", "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver", "ToggleSwitchKnobFillOnPressed"])
    ];

    private bool _isInitializingTheme = true;
    private bool _isInitializingLanguage = true;

    internal MainViewModel ViewModel { get; }

    public MainWindow() : this(new MainViewModel())
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();

        RestoreWindowSettings();

        var themePreference = LoadThemePreference();
        _isInitializingTheme = true;
        ThemeSelector.SelectedIndex = (int)themePreference;
        _isInitializingTheme = false;

        _isInitializingLanguage = true;
        LanguageSelector.SelectedIndex = (int)LocalizationService.CurrentLanguage;
        _isInitializingLanguage = false;

        ApplyThemePreference(themePreference);
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;

        Loaded += async (_, _) =>
        {
            ViewModel.PrefillFromClipboard();
            _ = ViewModel.RunStartupUpdateCheckAsync();
            await ViewModel.RunStartupChecksAsync();
        };

        Closing += (_, _) =>
        {
            SaveWindowSettings();
            ViewModel.CancelStartupChecks();
            ViewModel.CancelActiveOperation();
            ViewModel.CancelUpdateChecks();
        };

        Closed += (_, _) =>
        {
            ApplicationThemeManager.Changed -= OnApplicationThemeChanged;
            ViewModel.Dispose();
        };
    }

    private enum ThemePreference
    {
        Dark,
        System,
        Light
    }

    private static void OnApplicationThemeChanged(ApplicationTheme theme, MediaColor _)
    {
        ApplyBrandAccent(theme);
        ApplyClipPullPalette(theme);
    }

    private ThemePreference LoadThemePreference()
    {
        var forcedValue = Environment.GetEnvironmentVariable("CLIPPULL_THEME");
        if (TryParseThemePreference(forcedValue, out var forcedPreference))
            return forcedPreference;

        if (TryParseThemePreference(ViewModel.ThemePreference, out var savedPreference))
            return savedPreference;

        return ThemePreference.Dark;
    }

    private static bool TryParseThemePreference(string? value, out ThemePreference preference)
    {
        if (Enum.TryParse(value, ignoreCase: true, out preference))
            return true;

        preference = ThemePreference.Dark;
        return false;
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingTheme || ThemeSelector.SelectedIndex < 0)
            return;

        var preference = (ThemePreference)ThemeSelector.SelectedIndex;
        ApplyThemePreference(preference);
        ViewModel.SetThemePreference(preference.ToString());
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingLanguage || LanguageSelector.SelectedIndex < 0)
            return;

        var language = (AppLanguage)LanguageSelector.SelectedIndex;
        LocalizationService.SetLanguage(language);
        ViewModel.SetLanguagePreference(language);
    }

    private void RestoreWindowSettings()
    {
        var saved = ViewModel.SavedWindow;
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(saved.Width, MinWidth, Math.Max(MinWidth, workArea.Width));
        Height = Math.Clamp(saved.Height, MinHeight, Math.Max(MinHeight, workArea.Height));
        WindowState = saved.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void SaveWindowSettings()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        var width = bounds.Width > 0 && double.IsFinite(bounds.Width) ? bounds.Width : Width;
        var height = bounds.Height > 0 && double.IsFinite(bounds.Height) ? bounds.Height : Height;
        ViewModel.SaveWindow(width, height, WindowState == WindowState.Maximized);
    }

    private void ApplyThemePreference(ThemePreference preference)
    {
        if (IsLoaded)
            SystemThemeWatcher.UnWatch(this);

        var theme = preference switch
        {
            ThemePreference.Light => ApplicationTheme.Light,
            ThemePreference.System => GetSystemApplicationTheme(),
            _ => ApplicationTheme.Dark
        };

        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: false);
        ApplyBrandAccent(theme);
        ApplyClipPullPalette(theme);

        if (preference == ThemePreference.System)
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: false);
    }

    private static ApplicationTheme GetSystemApplicationTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast())
            return ApplicationTheme.HighContrast;

        return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
    }

    private static void ApplyClipPullPalette(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.HighContrast)
        {
            ApplyHighContrastPalette();
            return;
        }

        var paletteName = theme == ApplicationTheme.Light ? "ClipPullLight" : "ClipPullDark";
        foreach (var (suffix, brushKeys) in PaletteBrushMap)
        {
            if (WpfApplication.Current.TryFindResource($"{paletteName}{suffix}Color") is not MediaColor color)
                continue;

            foreach (var brushKey in brushKeys)
                WpfApplication.Current.Resources[brushKey] = new SolidColorBrush(color);
        }

        SetAccentColorResources(paletteName);

        WpfApplication.Current.Resources["FocusStrokeColorInnerBrush"] =
            WpfApplication.Current.Resources["ClipPullBackgroundBrush"];
    }

    private static void SetAccentColorResources(string paletteName)
    {
        SetColorResource($"{paletteName}AccentColor",
            "SystemAccentColor", "SystemAccentColorPrimary", "AccentFillColorDefault");
        SetColorResource($"{paletteName}AccentHoverColor",
            "SystemAccentColorSecondary", "AccentFillColorSecondary");
        SetColorResource($"{paletteName}AccentPressedColor",
            "SystemAccentColorTertiary", "AccentFillColorTertiary");
        SetColorResource($"{paletteName}OnAccentColor",
            "TextOnAccentFillColorPrimary", "TextOnAccentFillColorSecondary");
    }

    private static void SetColorResource(string sourceKey, params string[] targetKeys)
    {
        if (WpfApplication.Current.TryFindResource(sourceKey) is not MediaColor color)
            return;

        foreach (var targetKey in targetKeys)
            WpfApplication.Current.Resources[targetKey] = color;
    }

    private static void ApplyHighContrastPalette()
    {
        SetBrushes(WpfSystemColors.WindowColor,
            "ClipPullBackgroundBrush", "ClipPullChromeBrush", "ClipPullDropZoneBrush",
            "ApplicationBackgroundBrush", "LayerFillColorDefaultBrush", "FocusStrokeColorInnerBrush");
        SetBrushes(WpfSystemColors.ControlColor,
            "ClipPullSurfaceBrush", "ClipPullSurfaceSecondaryBrush", "ClipPullSurfaceElevatedBrush", "ClipPullSurfaceHoverBrush", "ClipPullDropZoneActiveBrush",
            "CardBackgroundFillColorDefaultBrush", "CardBackgroundFillColorSecondaryBrush", "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush",
            "ControlFillColorTertiaryBrush", "ControlFillColorDisabledBrush", "SubtleFillColorTransparentBrush", "SubtleFillColorSecondaryBrush",
            "SubtleFillColorTertiaryBrush", "LayerOnAcrylicFillColorDefaultBrush");
        SetBrushes(WpfSystemColors.HighlightColor,
            "ClipPullAccentBrush", "ClipPullAccentHoverBrush", "ClipPullAccentPressedBrush", "ClipPullCyanBrush", "ClipPullVioletBrush",
            "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush", "AccentTextFillColorPrimaryBrush",
            "AccentTextFillColorSecondaryBrush", "FocusStrokeColorOuterBrush");
        SetBrushes(WpfSystemColors.WindowTextColor,
            "ClipPullTextPrimaryBrush", "ClipPullTextSecondaryBrush", "ClipPullBorderBrush", "ClipPullBorderStrongBrush", "ClipPullDropBorderBrush", "ClipPullDividerBrush",
            "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "ControlStrokeColorDefaultBrush", "ControlStrokeColorSecondaryBrush",
            "CardStrokeColorDefaultBrush", "DividerStrokeColorDefaultBrush");
        SetBrushes(WpfSystemColors.GrayTextColor,
            "ClipPullTextTertiaryBrush", "ClipPullTextDisabledBrush", "TextFillColorTertiaryBrush", "TextFillColorDisabledBrush");
        SetBrushes(WpfSystemColors.HighlightTextColor, "ClipPullOnAccentBrush", "TextOnAccentFillColorPrimaryBrush");
        SetColorResources(WpfSystemColors.HighlightColor,
            "SystemAccentColor", "SystemAccentColorPrimary", "SystemAccentColorSecondary", "SystemAccentColorTertiary",
            "AccentFillColorDefault", "AccentFillColorSecondary", "AccentFillColorTertiary");
        SetColorResources(WpfSystemColors.HighlightTextColor,
            "TextOnAccentFillColorPrimary", "TextOnAccentFillColorSecondary");
    }

    private static void SetBrushes(MediaColor color, params string[] brushKeys)
    {
        foreach (var brushKey in brushKeys)
            WpfApplication.Current.Resources[brushKey] = new SolidColorBrush(color);
    }

    private static void SetColorResources(MediaColor color, params string[] colorKeys)
    {
        foreach (var colorKey in colorKeys)
            WpfApplication.Current.Resources[colorKey] = color;
    }

    private static void ApplyBrandAccent(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown)
            theme = ApplicationTheme.Dark;

        if (theme == ApplicationTheme.HighContrast)
            return;

        var colorKey = theme == ApplicationTheme.Light
            ? "ClipPullLightAccentColor"
            : "ClipPullDarkAccentColor";
        if (WpfApplication.Current.TryFindResource(colorKey) is MediaColor brandAccent)
            ApplicationAccentColorManager.Apply(brandAccent, theme, systemGlassColor: false, systemAccentColor: false);
    }

    private void SetDropZoneActive(bool isActive)
    {
        DropZoneBorder.SetResourceReference(
            Border.BorderBrushProperty,
            isActive ? "ClipPullCyanBrush" : "ClipPullDropBorderBrush");
        DropZoneBorder.SetResourceReference(
            Border.BackgroundProperty,
            isActive ? "ClipPullDropZoneActiveBrush" : "ClipPullDropZoneBrush");
        DropZoneBorder.BorderThickness = new Thickness(isActive ? 2 : 1);
        DropZoneBorder.Padding = new Thickness(isActive ? 19 : 20);
    }

    private void OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Any(path => string.Equals(System.IO.Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                SetDropZoneActive(true);
                e.Handled = true;
                return;
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            SetDropZoneActive(true);
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        SetDropZoneActive(false);
        e.Handled = true;
    }

    private void OnPreviewDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.X <= 0 || position.Y <= 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
            SetDropZoneActive(false);
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        SetDropZoneActive(false);

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ViewModel.ImportDroppedFiles(files);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.UnicodeText) && e.Data.GetData(DataFormats.UnicodeText) is string unicodeText)
        {
            ViewModel.AddDroppedText(unicodeText);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.Text) && e.Data.GetData(DataFormats.Text) is string text)
        {
            ViewModel.AddDroppedText(text);
        }
    }
}
