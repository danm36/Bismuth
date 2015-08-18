using System;
using System.Net.Sockets;

namespace Bismuth
{
    public static class LogManager
    {
        public static void Log(string source, string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + source + " - " + message);
        }

        public static void Warn(string source, string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + source + " - " + message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void Error(string source, string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + DateTime.Now.ToUniversalTime().ToString("G") + "] " + source + " - " + message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void Log(TcpClient client, string message){ Log(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Warn(TcpClient client, string message) { Warn(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
        public static void Error(TcpClient client, string message) { Error(client == null ? "Unknown Client" : client.Client.RemoteEndPoint.ToString(), message); }
    }
}
