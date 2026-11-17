using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace HabitApp;

public static class Preferences
{
    private static readonly Dictionary<string, string> LightColors = new()
    {
        ["AppBackground"] = "#F6F4EF",
        ["CardBackground"] = "#FFFFFF",
        ["TextPrimary"] = "#1F2A2E",
        ["TextSecondary"] = "#5C6462",
        ["TextMuted"] = "#8A8F8C",
        ["Line"] = "#E2DDD2",
        ["CellEmpty"] = "#E9E5DC",
        ["CellHover"] = "#D8D2C4",
        ["HoverFill"] = "#EFEBE3",
        ["Accent"] = "#2F7D6B",
        ["AccentHover"] = "#28695A",
        ["AccentPressed"] = "#215649",
        ["ScrollThumb"] = "#C9C3B6"
    };

    private static readonly Dictionary<string, string> DarkColors = new()
    {
        ["AppBackground"] = "#141819",
        ["CardBackground"] = "#1E2426",
        ["TextPrimary"] = "#E6EBE9",
        ["TextSecondary"] = "#B3BCB8",
        ["TextMuted"] = "#7E8885",
        ["Line"] = "#30393B",
        ["CellEmpty"] = "#2A3133",
        ["CellHover"] = "#374043",
        ["HoverFill"] = "#283033",
        ["Accent"] = "#3A957F",
        ["AccentHover"] = "#44A78F",
        ["AccentPressed"] = "#317F6C",
        ["ScrollThumb"] = "#3C4648"
    };

    // "light", "dark" or "system"
    public static string ThemeMode { get; private set; } = "system";

    // "windows", "sunday", "monday" or "saturday"
    public static string WeekStart { get; private set; } = "windows";

    public static bool IsDark { get; private set; }
    public static DayOfWeek WindowsFirstDay { get; private set; }

    public static void Load()
    {
        WindowsFirstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        ThemeMode = Database.GetSetting("theme", "system");
        WeekStart = Database.GetSetting("weekStart", "windows");

        ApplyTheme();
        ApplyWeekStart();

        // Follow Windows when it switches between light and dark.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && ThemeMode == "system")
            {
                Application.Current.Dispatcher.InvokeAsync(ApplyTheme);
            }
        };
    }

    public static void SetTheme(string mode)
    {
        ThemeMode = mode;
        Database.SetSetting("theme", mode);
        ApplyTheme();
    }

    public static void SetWeekStart(string value)
    {
        WeekStart = value;
        Database.SetSetting("weekStart", value);
        ApplyWeekStart();
    }

    private static void ApplyTheme()
    {
        IsDark = ThemeMode == "dark" || (ThemeMode == "system" && WindowsUsesDarkMode());

        Dictionary<string, string> colors = IsDark ? DarkColors : LightColors;
        foreach (KeyValuePair<string, string> pair in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
            brush.Freeze();
            Application.Current.Resources[pair.Key] = brush;
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }
    }

    private static bool WindowsUsesDarkMode()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyWeekStart()
    {
        DayOfWeek first = WeekStart switch
        {
            "sunday" => DayOfWeek.Sunday,
            "monday" => DayOfWeek.Monday,
            "saturday" => DayOfWeek.Saturday,
            _ => WindowsFirstDay
        };

        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek = first;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void ApplyTitleBar(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        int dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); // 20 = dark title bar
    }
}