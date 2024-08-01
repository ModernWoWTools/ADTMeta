using System.Collections.Concurrent;
using System.Net;

namespace ADTMeta.Provider
{
    public static class ListFile
    {
        private static ConcurrentDictionary<int, string>? _fileIdXPath = null;
        private static ConcurrentDictionary<string, int>? _pathXFileID = null;

        public static ConcurrentDictionary<int, string> NameMap
        {
            get
            {
                if (_fileIdXPath == null)
                    throw new InvalidOperationException("Listfile not initialized");

                return _fileIdXPath;
            }
        }

        public static ConcurrentDictionary<string, int> FileIdMap
        {
            get
            {
                if (_pathXFileID == null)
                    throw new InvalidOperationException("Listfile not initialized");

                return _pathXFileID;
            }
        }

        public static void Initialize()
        {
            Console.WriteLine("[INFO] Initializing listfile");
            string listFilePath = Path.Combine(AppSettings.Instance.GetCachePath(), "listfile.csv");

            // Download listfile if not exists or older than 1 day
            if (!File.Exists(listFilePath) || (DateTime.Now - File.GetLastWriteTime(listFilePath)).TotalDays >= 1)
            {
                using (var client = new WebClient())
                {
                    Console.WriteLine("[INFO] Downloading listfile from " + AppSettings.Instance.ListFileUrl);
                    client.DownloadFile(AppSettings.Instance.ListFileUrl, listFilePath);
                }
            }

            _fileIdXPath = new ConcurrentDictionary<int, string>();
            _pathXFileID = new ConcurrentDictionary<string, int>();

            Parallel.ForEach(File.ReadAllLines(listFilePath), line =>
            {
                var parts = line.Split(';');
                if (parts.Length < 2)
                    return;

                int fileId = int.Parse(parts[0]);
                string path = parts[1].ToLowerInvariant();

                _fileIdXPath[fileId] = path;
                _pathXFileID[path] = fileId;
            });
        }
    }
}
