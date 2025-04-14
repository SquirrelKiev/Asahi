using Newtonsoft.Json;

namespace April.Config.Editor;

public class ConfigService
{
    public ConfigFile? ConfigFile { get; private set; }
    public event Action<ConfigFile>? OnConfigLoaded;

    public void SaveConfigToFile(string configPath)
    {
        var json = JsonConvert.SerializeObject(ConfigFile, Formatting.Indented);

        File.WriteAllText(configPath, json);
    }

    /// <remarks>This should only be called when we are confident nothing is accessing the config really</remarks>
    public ConfigFile? LoadConfigFromFile(string configPath)
    {
        if (!File.Exists(configPath)) return null;

        var contents = File.ReadAllText(configPath);
        return LoadConfig(contents);
    }

    public ConfigFile? LoadConfig(string configContents)
    {
        var config = JsonConvert.DeserializeObject<ConfigFile>(configContents);
        if (config == null) return null;

        ConfigFile = config;
        OnConfigLoaded?.Invoke(ConfigFile);
        return ConfigFile;
    }

    public ConfigFile? NewConfig()
    {
        ConfigFile = new ConfigFile();

        return ConfigFile;
    }

    public void ClearConfig()
    {
        ConfigFile = null;
    }
}