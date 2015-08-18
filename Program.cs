using System;
using System.Reflection;

namespace Bismuth
{
    public static class Program
    {
        enum ERunState
        {
            RS_Normal,
            RS_RCONOnly,
        }
        static ERunState runState = ERunState.RS_Normal;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) => { Shutdown(); };
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);

            MIMETypeManager.Setup();

            //TODO: Load plugins to get mode bindings

            ModeFlagBindings.AddBindings("HELP", "Displays this help text", new string[] { "h", "help" });
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-')
                {
                    if (args[i][1] == '-')
                    {
                        ModeFlagBindings.SetFlag(args[i].Substring(2));
                    }
                    else
                    {
                        for (int j = 1; j < args[i].Length; j++)
                            ModeFlagBindings.SetFlag(args[i][j]);
                    }
                }
            }

            Console.WriteLine(@"
  ____ _____  _____ __  __ _    _ _______ _    _ 
 |  _ \_   _|/ ____|  \/  | |  | |__   __| |  | |
 | |_) || | | (___ | \  / | |  | |  | |  | |__| |
 |  _ < | |  \___ \| |\/| | |  | |  | |  |  __  |
 | |_) || |_ ____) | |  | | |__| |  | |  | |  | |
 |____/_____|_____/|_|  |_|\____/   |_|  |_|  |_|
                                                 
 Version " + GetFullProgramVersionString() + @"
-------------------------------------------------------------------
");

            if (ModeFlagBindings.IsFlagSet("HELP"))
            {
                ModeFlagBindings.PrintHelp();
                return;
            }

            VirtualHostManager.Setup();
            NetworkManager.Setup();

            Console.WriteLine("Beginning primary loop");
            while (true)
            {
                //TODO: Handle plugin receive handler
                NetworkManager.ListenForNewConnections();
                NetworkManager.ManageThreadPool();
            }

            Shutdown();
        }

        static void Shutdown()
        {
            Console.WriteLine("Beginning Bismuth shutdown");

            NetworkManager.Shutdown();

            //TODO: Call plugin shutdown scripts

            Console.WriteLine("Bismuth shutdown complete");
        }



        public static string GetFullProgramVersionString()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return "Bismuth Version " + version.ToString(2) + " - " + GetVersionCodename(version) + " (" + Environment.OSVersion.Platform + ")";
        }

        public static string GetVersionString()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return "Bismuth/" + version.ToString(2) + " (" + Environment.OSVersion.Platform + ")";
        }

        public static string GetVersionCodename(Version version)
        {
            switch (version.Major)
            {
                default: return "Unknown";
                case 0: return "Max";
                case 1: return "Musashi";
                case 2: return "Loki";
                case 3: return "Conan";
                case 4: return "Rex";
                case 5: return "Triton";
                case 6: return "Chewy";
                case 7: return "Shiva";
                case 8: return "Hannibal";
            }
        }
    }
}
