using System.Collections.Generic;
using Windows.Storage;

namespace EasyDisk.Services;

public static class SettingsService
{
    // Use a safe accessor for local settings: try to use ApplicationData.Current.LocalSettings,
    // but fall back to an in-memory dictionary when ApplicationData is not available (for example
    // in unpackaged WinUI 3 desktop scenarios where ApplicationData.Current can throw).
    private static IDictionary<string, object> _localValues;

    private static IDictionary<string, object> LocalValues
    {
        get
        {
            if (_localValues != null) return _localValues;
            try
            {
                // ApplicationDataContainer.Values implements IPropertySet which is compatible with IDictionary<string, object>
                _localValues = (IDictionary<string, object>)ApplicationData.Current.LocalSettings.Values;
            }
            catch
            {
                // Fallback to an in-memory dictionary when Windows.Storage is unavailable
                _localValues = new Dictionary<string, object>();
            }
            return _localValues;
        }
    }

    public static bool ThemeDark
    {
        get => Get("ThemeDark", true);
        set => Set("ThemeDark", value);
    }

    public static bool UseMica
    {
        get => Get("UseMica", true);
        set => Set("UseMica", value);
    }

    public static bool UseRecycleBin
    {
        get => Get("UseRecycleBin", true);
        set => Set("UseRecycleBin", value);
    }

    private static bool Get(string key, bool def)
    {
        if (LocalValues.TryGetValue(key, out var v) && v is bool b) return b;
        return def;
    }

    private static void Set(string key, bool value) => LocalValues[key] = value;
}
