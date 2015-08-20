using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private class RCONClient
        {
            public enum ELoginState
            {
                NotLoggedIn,
                NeedPassword,
                LoggedIn,
            }

            public ELoginState LoginState { get; private set; }
            public TcpClient tcpClient { get; private set; }
            public string Username { get; private set; }
            public byte[] UsernameBytes { get; private set; }
            public string RemoteEndpoint { get { return tcpClient.Client.RemoteEndPoint.ToString(); } }

            public RCONClient(TcpClient client)
            {
                tcpClient = client;
                SetUsername(tcpClient.Client.RemoteEndPoint.ToString());
                SetLoginState(ELoginState.NotLoggedIn);
            }

            public void SetUsername(string name)
            {
                Username = name;
                UsernameBytes = Encoding.ASCII.GetBytes(name);
            }

            public void SetLoginState(ELoginState newState)
            {
                LoginState = newState;
            }

            public void Login(string username)
            {
                SetUsername(username);
                SetLoginState(ELoginState.LoggedIn);
            }
        }

        private class RCONCommand
        {
            Func<string[], object> command = null;
            public string Name { get; private set; }
            public string Documentation { get; private set; }

            public RCONCommand(string name, string doc, Func<string[], object> func)
            {
                Name = name;
                Documentation = doc;
                command = func;
            }

            public object Invoke(string[] args)
            {
                if(command != null)
                    return command(args);

                return "ERROR: Undefined command function '" + Name + "'\r\nNo function was set to invoke";
            }
        }


        static Thread rconListenThread = null;
        static TcpListener server = null;
        public static int Port { get; private set; }    //TODO: Config
        readonly static int RCONConnectionFullTTL = 20000;
        readonly static int RCONConnectionInputTTL = 15000;

        readonly static byte[] CRLF = Encoding.ASCII.GetBytes("\r\n");
        readonly static string TelnetInputPromptStr = " => ";
        readonly static byte[] TelnetInputPrompt = Encoding.ASCII.GetBytes(TelnetInputPromptStr);
        readonly static string UsernamePrompt = "Please enter your username:";
        readonly static string PasswordPrompt = "Please enter your password:";
        readonly static string LoginSuccessfulText = "\r\nLogin successful\r\nWelcome ";
        readonly static string LoginFailedText = "\r\nERROR: Login failed\r\n" + UsernamePrompt;
        readonly static string ttlInputExpiredText = "\r\n\r\nNo input recieved for " + (RCONConnectionInputTTL / 1000) + " seconds.\r\nYou have been forceably disconnected";
        readonly static string ttlFullExpiredText = "\r\n\r\nConnection exeeeded max idle time of " + (RCONConnectionFullTTL / 1000) + " seconds.\r\nYou have been forceably disconnected";

        static Dictionary<string, RCONCommand> commands = new Dictionary<string, RCONCommand>();
        static Dictionary<string, string> ValidRCONClients = new Dictionary<string, string>()
        {
            { "root", "test" }
        };

        [ThreadStatic]
        static RCONClient currentClient = null;

        public override bool Setup()
        {
            Port = 1820;
            server = new TcpListener(IPAddress.Any, Port); //TODO: Config
            server.Start();
            LogManager.WriteLine("RCON server started for " + server.LocalEndpoint.ToString(), ConsoleColor.Green);

            rconListenThread = new Thread(new ThreadStart(RCONListenForClients));
            rconListenThread.Start();

            AddRCONCommand("quit", "Logs out of and disconnects from the server", (args) => { return "Bye!"; });
            AddRCONCommand("exit", "Logs out of and disconnects from the server", (args) => { return "Bye!"; });
            AddRCONCommand("disconnect", "Logs out of and disconnects from the server", (args) => { return "Bye!"; });
            AddRCONCommand("logout", "Logs out of and disconnects from the server", (args) => { return "Bye!"; });
            AddRCONCommand("help", "Displays a full list of commands with associated documentation", (args) =>
                {
                    List<string> commandInfoLines = new List<string>();
                    foreach (KeyValuePair<string, RCONCommand> command in commands)
                    {
                        commandInfoLines.Add(command.Value.Name.PadRight(15) + " - " + command.Value.Documentation);
                    }
                    commandInfoLines.Sort();

                    StringBuilder returnString = new StringBuilder();
                    returnString.Append("List of valid RCON commands\r\n");
                    for(int i = 0; i < commandInfoLines.Count; i++)
                        returnString.Append(commandInfoLines[i] + "\r\n");

                    return returnString.ToString();
                });
            return true;
        }

        public static void AddRCONCommand(string command, string documentation, Func<string[], object> function)
        {
            commands.Add(command, new RCONCommand(command, documentation, function));
        }

        private static void RCONListenForClients()
        {
            while (!Program.ShutDown)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    BismuthThreadPool.StartThread(DoHandleClient, new RCONClient(client));
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
            currentClient = (RCONClient)objClient;

            try
            {
                HandleClient();
            }
            catch (Exception e)
            {
                LogManager.Error("RCON", "Exception occured in RCON thread for " + currentClient.RemoteEndpoint);
                LogManager.Error("RCON", e.ToString());
            }

            if (currentClient.tcpClient.Connected)
                currentClient.tcpClient.Close();
        }

        private static void HandleClient()
        {
            LogManager.Log("RCON", "Accepted RCON connection from " + currentClient.Username + " (" + currentClient.RemoteEndpoint + ")");
            
            bool closingConnection = false;
            NetworkStream stream = currentClient.tcpClient.GetStream();

            string welcomeString = Program.BismuthWelcomeHeader + "\r\nWelcome to this Bismuth RCON server\r\n";

            if (true) //Check if login required
                welcomeString += "\r\n" + UsernamePrompt;

            SendClientResponse(stream, Encoding.ASCII.GetBytes(welcomeString));

            Stopwatch timeSinceLastRequest = new Stopwatch();
            Stopwatch timeSinceLastInput = new Stopwatch();
            while (!closingConnection)
            {
                timeSinceLastRequest.Restart();

                LinkedList<byte> requestData = new LinkedList<byte>();
                byte lastByte = 255;
                while (lastByte != 10)
                {
                    timeSinceLastInput.Restart();

                    while (currentClient.tcpClient.Available == 0)
                    {
                        Thread.Sleep(2);
                        if (timeSinceLastRequest.ElapsedMilliseconds > RCONConnectionFullTTL)
                        {
                            SendClientResponse(stream, ttlFullExpiredText, true);
                            closingConnection = true;
                            break;
                        }
                        else if (timeSinceLastInput.ElapsedMilliseconds > RCONConnectionInputTTL)
                        {
                            SendClientResponse(stream, ttlInputExpiredText, true);
                            closingConnection = true;
                            break;
                        }
                    }

                    if (!currentClient.tcpClient.Connected || closingConnection)
                    {
                        closingConnection = true;
                        break;
                    }

                    byte[] pdata = new byte[currentClient.tcpClient.Available];
                    stream.Read(pdata, 0, pdata.Length);
                    for (int i = 0; i < pdata.Length; i++)
                    {
                        if (pdata[i] == 8) //Backspace
                        {
                            if(requestData.Count > 0)
                                requestData.RemoveLast();

                            continue;
                        }

                        requestData.AddLast(pdata[i]);
                    }

                    lastByte = requestData.Last == null ? lastByte: requestData.Last.Value;
                }

                if (closingConnection || !currentClient.tcpClient.Connected)
                    break;

                string requestString = Encoding.ASCII.GetString(requestData.ToArray()).TrimEnd('\r', '\n');
                requestString = new string(requestString.Where(c => !char.IsControl(c)).ToArray());

                if (currentClient.LoginState == RCONClient.ELoginState.NotLoggedIn)
                {
                    currentClient.SetUsername(requestString);
                    currentClient.SetLoginState(RCONClient.ELoginState.NeedPassword);
                    SendClientResponse(stream, PasswordPrompt);
                }
                else if (currentClient.LoginState == RCONClient.ELoginState.NeedPassword)
                {
                    if (ValidRCONClients.ContainsKey(currentClient.Username) && ValidRCONClients[currentClient.Username] == requestString)
                    {
                        SendClientResponse(stream, LoginSuccessfulText + currentClient.Username + "!\r\n");
                        currentClient.SetLoginState(RCONClient.ELoginState.LoggedIn);
                        LogManager.Log("RCON", "Client " + currentClient.Username + " successfully logged in");
                    }
                    else
                    {
                        LogManager.Log("RCON", "Client " + currentClient.RemoteEndpoint + " failed to login as " + currentClient.Username);
                        currentClient.SetUsername(currentClient.RemoteEndpoint);
                        SendClientResponse(stream, LoginFailedText);
                        currentClient.SetLoginState(RCONClient.ELoginState.NotLoggedIn);
                    }
                }
                else if (currentClient.LoginState == RCONClient.ELoginState.LoggedIn)
                {
                    string[] request = requestString.Split(' ');

                    if (request.Length == 0)
                        continue;

                    LogManager.Log("RCON - " + currentClient.Username + TelnetInputPromptStr + requestString);

                    string command = request[0].ToLower();

                    if (command == "quit" || command == "disconnect" || command == "exit")
                    {
                        command = "quit";
                        closingConnection = true;
                    }

                    byte[] responseData;
                    if (commands.ContainsKey(command))
                    {
                        object response = commands[command].Invoke(request);
                        if (response is byte[])
                            responseData = (byte[])response;
                        else
                            responseData = Encoding.ASCII.GetBytes(response.ToString());
                    }
                    else
                    {
                        responseData = Encoding.ASCII.GetBytes("Error: Unknown command '" + command + "'");
                    }

                    SendClientResponse(stream, responseData);
                }
            }

            LogManager.Log("RCON", "Closed RCON connection from " + currentClient.Username + " (" + currentClient.RemoteEndpoint + ")");
            currentClient.tcpClient.Close();
        }

        private static void SendClientResponse(Stream stream, string response, bool isServerMessage = false)
        {
            SendClientResponse(stream, Encoding.ASCII.GetBytes(response), isServerMessage);
        }

        private static void SendClientResponse(Stream stream, byte[] response, bool isServerMessage = false)
        {
            stream.Write(response, 0, response.Length);
            stream.Write(CRLF, 0, CRLF.Length);
            if (!isServerMessage)
            {
                stream.Write(currentClient.UsernameBytes, 0, currentClient.UsernameBytes.Length);
                stream.Write(TelnetInputPrompt, 0, TelnetInputPrompt.Length);
            }
            stream.Flush();
        }
    }
}
