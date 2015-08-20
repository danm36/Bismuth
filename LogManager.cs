using System;
using System.Net.Sockets;

namespace Bismuth
{
    public static class LogManager
    {
        public enum ELogLevel : byte
        {
            None        = 0,
            Critical    = 1,
            Error       = 2,
            Warning     = 3,
            Display     = 4,
            Notice      = 5,

            All         = 255,
        }

        static ELogLevel logLevel = ELogLevel.All;

        public static void Write(string message, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        {
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
            Console.Write(message);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public static void WriteLine(string message, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        {
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public static void Notice(string message)
        {
            if (logLevel >= ELogLevel.Notice)
                WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + message, ConsoleColor.Cyan);
        }

        public static void Log(string message)
        {
            if (logLevel >= ELogLevel.Display)
                WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + message);
        }

        public static void Warn(string message)
        {
            if (logLevel >= ELogLevel.Warning)
                WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + message, ConsoleColor.Yellow);
        }

        public static void Error(string message)
        {
            if (logLevel >= ELogLevel.Error)
                WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + message, ConsoleColor.Red);
        }

        public static void Critical(string message)
        {
            if (logLevel >= ELogLevel.Critical)
                WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + message, ConsoleColor.White, ConsoleColor.Red);
        }


        public static void Notice(string source, string message) { Notice(source + " - " + message); }
        public static void Log(string source, string message) { Log(source + " - " + message); }
        public static void Warn(string source, string message) { Warn(source + " - " + message); }
        public static void Error(string source, string message) { Error(source + " - " + message); }
        public static void Critical(string source, string message) { Critical(source + " - " + message); }

        public static void Notice(TcpClient client, string message) { Notice(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Log(TcpClient client, string message){ Log(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Warn(TcpClient client, string message) { Warn(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Error(TcpClient client, string message) { Error(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Critical(TcpClient client, string message) { Critical(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
    }
}
