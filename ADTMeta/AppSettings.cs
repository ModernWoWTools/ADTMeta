using CASCLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ADTMeta
{
    public class AppSettings
    {
        private const string CONFIG_FILE = "ADTMeta.json";

        private static string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Config");

        private static string _cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Cache");

        // Product key i.e. wow
        public string Region { private set; get; } = "eu";

        // Locale i.e. deDE
        [JsonConverter(typeof(StringEnumConverter))]
        public LocaleFlags Locale { private set; get; } = LocaleFlags.enUS;

        // ListFile Url
        public string ListFileUrl { private set; get; } = "https://github.com/wowdev/wow-listfile/releases/latest/download/community-listfile.csv";

        // Output debug messages in console
        public bool Verbose { private set; get; } = false;

        // Product key i.e. wow  (set over argument)
        [JsonIgnore]
        public string Product = "wow";

        // Meta Folder (set over argument)
        [JsonIgnore]
        public string MetaFolder = "Meta";

        private AppSettings() { }
        public static AppSettings Instance { get { return lazy.Value; } }
        private static readonly Lazy<AppSettings> lazy = new Lazy<AppSettings>(() =>
        {
            if (!Directory.Exists(_configPath))
                Directory.CreateDirectory(_configPath);

            string configFile = Path.Combine(_configPath, CONFIG_FILE);

            if (!File.Exists(configFile))
                (new AppSettings()).Save();

            return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(configFile));
        });

        public void Save()
        {
            string configFile = Path.Combine(_configPath, CONFIG_FILE);
            File.WriteAllText(configFile, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public string GetCachePath()
        {
            if (!Directory.Exists(_cachePath))
                Directory.CreateDirectory(_cachePath);

            return _cachePath;
        }
    }
}
