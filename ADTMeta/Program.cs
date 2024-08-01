using ADTMeta.Provider;
using ADTMeta.Steps;
using System.Diagnostics;

namespace ADTMeta
{
    class Program
    {
        public static bool NoInteractionMode = false;

        static void Main(string[] args)
        {
            Console.WriteLine(@"          _____ _______ __  __      _        ");
            Console.WriteLine(@"    /\   |  __ \__   __|  \/  |    | |       ");
            Console.WriteLine(@"   /  \  | |  | | | |  | \  / | ___| |_ __ _ ");
            Console.WriteLine(@"  / /\ \ | |  | | | |  | |\/| |/ _ \ __/ _` |");
            Console.WriteLine(@" / ____ \| |__| | | |  | |  | |  __/ || (_| |");
            Console.WriteLine(@"/_/    \_\_____/  |_|  |_|  |_|\___|\__\__,_|");
            Console.WriteLine(@"                           Created by Luzifix");
            Console.WriteLine("\n");

            ParseArguments(args);

            // Provider initialization
            ListFile.Initialize();
            CASC.Initialize(AppSettings.Instance.Product);

            // Steps
            Console.WriteLine(" ");
            var timer = new Stopwatch();
            timer.Start();
            TexMeta.Generate();
            Console.WriteLine($"[INFO] TexMeta generation in {timer.ElapsedMilliseconds / 1000} seconds.");
        }

        private static void ParseArguments(string[] args)
        {
            string program = AppSettings.Instance.Product;
            if (args.Length >= 1)
                program = args[0];

            string metaFolder = AppSettings.Instance.MetaFolder;
            if (args.Length >= 2)
                metaFolder = args[1];

            metaFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, metaFolder, program));
            if (!Directory.Exists(metaFolder))
                Directory.CreateDirectory(metaFolder);

            AppSettings.Instance.Product = program;
            AppSettings.Instance.MetaFolder = metaFolder;
        }
    }
}