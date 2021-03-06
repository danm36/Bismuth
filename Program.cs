﻿using Bismuth.RCON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Bismuth
{
    public static class Program
    {
        public static string BismuthWelcomeHeader { get; private set; }

        enum ERunState
        {
            RS_Normal,
            RS_RCONOnly,
        }

        public static DateTime LaunchTime { get; private set; }
        static ERunState runState = ERunState.RS_Normal;
        public static bool ShutDown { get; private set; }

        static List<Tuple<BismuthManagerInfo, BismuthGenericManager>> managers = new List<Tuple<BismuthManagerInfo, BismuthGenericManager>>();

        static void Main(string[] args)
        {
            LaunchTime = DateTime.Now;

            Console.CancelKeyPress += (s, e) => { Shutdown(); };
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);

            BismuthWelcomeHeader = @"
  ____ _____  _____ __  __ _    _ _______ _    _ 
 |  _ \_   _|/ ____|  \/  | |  | |__   __| |  | |
 | |_) || | | (___ | \  / | |  | |  | |  | |__| |
 |  _ < | |  \___ \| |\/| | |  | |  | |  |  __  |
 | |_) || |_ ____) | |  | | |__| |  | |  | |  | |
 |____/_____|_____/|_|  |_|\____/   |_|  |_|  |_|
                                                 
 Version " + GetFullProgramVersionString() + @"
-------------------------------------------------------------------
";
            Console.WriteLine(BismuthWelcomeHeader);

            RunBismuth(args);

            Shutdown();
        }

        static void RunBismuth(string[] args)
        {
            //Load plugins
            if (!BismuthConfig.Setup())
                return;

            BismuthThreadPool.Setup();
            SetupManagersFromLoadedAssemblies();

            ModeFlagBindings.AddBindings("HELP", "Displays this help text", new string[] { "h", "help" });
            RCONServer.AddRCONCommand("version", "Lists the Bismuth version information", (a) => { return Program.GetFullProgramVersionString(); });
            RCONServer.AddRCONCommand("status", "Lists various statistics about the current Bismuth process", (a) => { return GetStatus(true); });
            RCONServer.AddRCONCommand("list-managers", "Lists all currently running managers", (a) => { return ListManagers(true); });

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

            if (ModeFlagBindings.IsFlagSet("HELP"))
            {
                ModeFlagBindings.PrintHelp();
                return;
            }

            LogManager.Log("Main Thread", "Beginning primary execution loop");
            while (!ShutDown)
            {
                //TODO: Handle plugin receive handler
                NetworkManager.ListenForNewConnections();
                Thread.Sleep(1);
            }
            LogManager.Log("Main Thread", "Primary execution loop quit");
        }

        static bool shutdownMethodCalled = false;
        static void Shutdown()
        {
            if (shutdownMethodCalled)
                return;

            shutdownMethodCalled = ShutDown = true;

            LogManager.Log("SHUTDOWN", "Beginning Bismuth shutdown");
            ShutdownManagers();
            BismuthThreadPool.Shutdown();
            LogManager.Log("SHUTDOWN", "Bismuth shutdown complete");
            Console.ReadKey();
        }


        private static void SetupManagersFromLoadedAssemblies()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; ++i)
                SetupManagersFromAssembly(assemblies[i]);
        }

        public static void SetupManagersFromAssembly(Assembly assembly)
        {
            Type[] types = null;
            try
            {
                types = (from type in assembly.GetTypes()
                         where Attribute.IsDefined(type, typeof(BismuthManagerInfo))
                         select type).ToArray();
            }
            catch
            {
                //Could only cause an exception error on a plugin
                LogManager.Error("STARTUP", "Could not load types from plugin '" + assembly.Location + "'. This plugin may be corrupt and has been skipped.");
                return;
            }

            for (int i = 0; i < types.Length; ++i)
            {
                BismuthManagerInfo managerInfo = types[i].GetCustomAttribute<BismuthManagerInfo>();
                BismuthGenericManager manager = (BismuthGenericManager)Activator.CreateInstance(types[i]);

                try
                {
                    if (manager.Setup())
                    {
                        LogManager.Log("STARTUP", "Successfully setup " + managerInfo.Name);
                        Tuple<BismuthManagerInfo, BismuthGenericManager> managerTuple = new Tuple<BismuthManagerInfo, BismuthGenericManager>(managerInfo, manager);
                        managers.Add(managerTuple);
                    }
                    else
                    {
                        LogManager.Warn("STARTUP", managerInfo.Name + " did not setup successfully and has been skipped.");
                    }
                }
                catch (Exception e)
                {
                    LogManager.Error("STARTUP", "Exception when setting up " + managerInfo.Name + "\r\n" + e.ToString());
                }
            }
        }

        public static BismuthGenericManager GetManager(string managerUniqueID)
        {
            for (int i = 0; i < managers.Count; ++i)
            {
                if (managers[i].Item1.UID == managerUniqueID)
                    return managers[i].Item2;
            }

            return null;
        }

        public static void ShutdownManager(BismuthGenericManager manager)
        {
            for (int i = 0; i < managers.Count; ++i)
            {
                if (managers[i].Item2 == manager)
                {
                    Tuple<BismuthManagerInfo, BismuthGenericManager> mgr = managers[i];
                    managers.RemoveAt(i);
                    try
                    {
                        if (mgr.Item2.Shutdown())
                            LogManager.Log("SHUTDOWN", "Successfully shutdown " + mgr.Item1.Name);
                        else
                            LogManager.Warn("SHUTDOWN", mgr.Item1.Name + " did not report a successful shutdown");
                    }
                    catch (Exception e)
                    {
                        LogManager.Error("SHUTDOWN", "Exception when shutting down " + mgr.Item1.Name + "\r\n" + e.ToString());
                    }
                }
            }
        }

        public static void ShutdownManager(string managerUniqueId)
        {
            ShutdownManager(GetManager(managerUniqueId));
        }


        private static void ShutdownManagers()
        {
            while (managers.Count > 0)
                ShutdownManager(managers[managers.Count - 1].Item2);
        }


        public static string ListManagers(bool bSilent = false)
        {
            StringBuilder toReturn = new StringBuilder();

            toReturn.Append("List of currently running managers:\r\n");
            if (!bSilent) LogManager.WriteLine("List of currently running managers:");

            for (int i = 0; i < managers.Count; ++i)
            {
                //string uid = ("[" + managers[i].Item1.UID + "]").PadRight(31) + " ";
                //string name = managers[i].Item1.Name + "\r\n";
                //string description =  "".PadRight(32) + managers[i].Item1.Description + "\r\n";
                string uid = "";
                string name = ("[" + managers[i].Item1.Name + "]").PadRight(31) + " ";
                string description = managers[i].Item1.Description + "\r\n";

                toReturn.Append(uid + name + description);

                if (!bSilent)
                {
                    LogManager.Write(uid, ConsoleColor.Yellow);
                    LogManager.Write(name, ConsoleColor.Cyan);
                    LogManager.Write(description, ConsoleColor.Gray);
                }
            }

            return toReturn.ToString();
        }

        public static string GetStatus(bool bSilent = false)
        {
            StringBuilder toReturn = new StringBuilder();
            Process currentProcess = Process.GetCurrentProcess();
            TimeSpan Uptime = DateTime.Now - LaunchTime;
            TimeSpan TrueProcessorTime = TimeSpan.FromTicks(currentProcess.TotalProcessorTime.Ticks / Environment.ProcessorCount);

            toReturn.Append("Server status at " + DateTime.Now.ToString("r") + "\r\n");
            toReturn.Append("     Server Started: " + LaunchTime.ToString("r") + "\r\n");
            toReturn.Append("             Uptime: " + Uptime + "\r\n");
            toReturn.Append("     Total CPU Time: " + TrueProcessorTime + " on " + Environment.ProcessorCount + " cores\r\n");
            toReturn.Append("  Average CPU Usage: " + (100 * ((TrueProcessorTime.TotalMilliseconds * Environment.ProcessorCount) / Uptime.TotalMilliseconds)) + "%\r\n");
            toReturn.Append("    Total RAM Usage: " + (currentProcess.WorkingSet64 / 1024 / 1024) + "MB\r\n");
            toReturn.Append("   Thread Pool Info:\r\n");
            toReturn.Append("                    " + BismuthThreadPool.GetActiveThreadCount() + " active threads\r\n");
            toReturn.Append("                    " + BismuthThreadPool.GetTotalPossibleThreads() + " free threads\r\n");
            toReturn.Append("                    " + BismuthThreadPool.GetPendingTaskCount() + " tasks waiting\r\n");
            return toReturn.ToString();
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
                case 0: return "Think Tank";
                case 1: return "Tachikoma";
                case 2: return "Uchikoma";
                case 3: return "Fuchikoma";
                case 4: return "Logicoma";
                case 5: return "Jigabachi";
            }
        }
    }
}
