using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bismuth.RCON
{
    [BismuthManagerInfo("BISMUTH_RCON", "RCON Manager", "Handles the RCON Server")]
    public class RCONServer : BismuthGenericManager
    {
        static Thread rconListenThread = null;
        static TcpListener server = null;
        public static int Port { get; private set; }    //TODO: Config
        static List<Thread> threadPool = new List<Thread>();

        static Dictionary<string, Func<string[], object>> commands = new Dictionary<string, Func<string[], object>>();
        readonly static byte[] CRLF = new byte[2] { 13, 10 };

        [ThreadStatic]
        static TcpClient currentClient = null;

        public override bool Setup()
        {
            Port = 1820;
            server = new TcpListener(IPAddress.Any, Port); //TODO: Config
            server.Start();
            LogManager.WriteLine("RCON server started for " + server.LocalEndpoint.ToString(), ConsoleColor.Green);

            rconListenThread = new Thread(new ThreadStart(RCONListenForClients));
            rconListenThread.Start();

            AddRCONCommand("quit", (args) => { return "Bye!"; });
            AddRCONCommand("version", (args) => { return Program.GetFullProgramVersionString(); });
            return true;
        }

        public static void AddRCONCommand(string command, Func<string[], object> function)
        {
            commands.Add(command, function);
        }

        private static void RCONListenForClients()
        {
            while (!Program.ShutDown)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    Thread rconActiveThread = new Thread(new ParameterizedThreadStart(DoHandleClient));
                    threadPool.Add(rconActiveThread);
                    rconActiveThread.Start(client);
                }
                catch(Exception e)
                {
                    LogManager.Error("RCON", "Exception occured in RCON thread");
                    LogManager.Error("RCON", e.ToString());
                }
            }
        }

        private static void DoHandleClient(object objClient)
        {
            currentClient = (TcpClient)objClient;

            try
            {
                HandleClient();
            }
            catch (Exception e)
            {
                LogManager.Error("RCON", "Exception occured in RCON thread for " + currentClient.Client.RemoteEndPoint.ToString());
                LogManager.Error("RCON", e.ToString());
            }

            if (currentClient.Connected)
                currentClient.Close();
        }

        private static void HandleClient()
        {
            LogManager.Log("RCON", "Accepted RCON connection from " + currentClient.Client.RemoteEndPoint.ToString());
            
            bool closingConnection = false;
            NetworkStream stream = currentClient.GetStream();

            byte[] welcomeData = Encoding.ASCII.GetBytes(Program.BismuthWelcomeHeader + "\r\nWelcome to this Bismuth RCON server\r\n");
            stream.Write(welcomeData, 0, welcomeData.Length);
            stream.Flush();

            while (!closingConnection)
            {
                MemoryStream ms = new MemoryStream();
                byte lastByte = 255;
                while (lastByte != 10)
                {
                    while (currentClient.Available == 0) ;

                    if (!currentClient.Connected)
                    {
                        closingConnection = true;
                        break;
                    }

                    byte[] pdata = new byte[currentClient.Available];
                    stream.Read(pdata, 0, pdata.Length);
                    ms.Write(pdata, 0, pdata.Length);
                    lastByte = pdata[pdata.Length - 1];
                }

                if (closingConnection || !currentClient.Connected)
                    break;

                byte[] requestData = ms.ToArray();
                string requestString = Encoding.ASCII.GetString(requestData).TrimEnd('\r', '\n');
                string[] request = requestString.Split(' ');

                if (request.Length == 0)
                    continue;

                LogManager.WriteLine("RCON - " + currentClient.Client.RemoteEndPoint.ToString() + " - " + requestString);

                string command = request[0].ToLower();

                if (command == "quit" || command == "disconnect" || command == "exit")
                {
                    command = "quit";
                    closingConnection = true;
                }

                byte[] responseData;
                if (commands.ContainsKey(command))
                {
                    object response = commands[command](request);
                    if (response is byte[])
                        responseData = (byte[])response;
                    else
                        responseData = Encoding.ASCII.GetBytes(response.ToString());
                }
                else
                {
                    responseData = Encoding.ASCII.GetBytes("Error: Unknown command '" + command + "'");
                }

                stream.Write(responseData, 0, responseData.Length);
                stream.Write(CRLF, 0, CRLF.Length);
                stream.Flush();
            }

            LogManager.Log("RCON", "Closed RCON connection from " + currentClient.Client.RemoteEndPoint.ToString());
            currentClient.Close();
        }
    }
}
