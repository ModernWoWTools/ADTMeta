using CASCLib;

namespace ADTMeta.Provider
{
    public static class CASC
    {
        private static CASCHandler? _instance = null;

        public static CASCHandler Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("CASC handler not initialized");

                return _instance;
            }
        }

        public static void Initialize(string program)
        {
            _instance?.Clear();

            CASCConfig.ValidateData = false;
            CASCConfig.ThrowOnFileNotFound = false;
            CASCConfig.ThrowOnMissingDecryptionKey = false;
            CASCConfig.UseWowTVFS = false;

            CDNCache.CachePath = Path.Combine(AppSettings.Instance.GetCachePath(), "casc");
            LocaleFlags locale = AppSettings.Instance.Locale;

            Console.WriteLine("[INFO] Initializing CASC from web for program " + program + " and locale " + locale);
            _instance = CASCHandler.OpenOnlineStorage(program, AppSettings.Instance.Region);
            _instance.Root.SetFlags(locale);
        }
    }
}
