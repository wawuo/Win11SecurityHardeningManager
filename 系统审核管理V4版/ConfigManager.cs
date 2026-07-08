using System.Text.Json;
using System.Text;

namespace Win11SecurityHardeningManager;

public static class ConfigManager
{
    public static string AppRoot => FindAppRoot();
    public static string ConfigPath => Path.Combine(AppRoot, "Config", "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(ConfigPath))
            throw new FileNotFoundException("settings.json not found", ConfigPath);
        var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();
    }

    private static string FindAppRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Config", "settings.json")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}
