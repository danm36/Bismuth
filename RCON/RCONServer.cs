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
        const int RCON_DEFAULT_PORT = 1820;
        const int RCON_DEFAULT_TTL_MAX = 120;
        const int RCON_DEFAULT_TTL_INPUT = 30;

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
            public bool HideArgs { get; private set; }

            public RCONCommand(string name, string doc, Func<string[], object> func, bool hideArgsInLog = false)
            {
                Name = name;
                Documentation = doc;
                command = func;
                HideArgs = hideArgsInLog;
            }

            public object Invoke(string[] args)
            {
                if (HideArgs && args.Length > 0)
                {
                    StringBuilder maskedArgs = new StringBuilder();
                    for (int i = 0; i < args.Length; ++i)
                        maskedArgs.Append(new string('*', args[i].Length) + " ");
                    LogManager.Log("RCON - " + currentClient.Username + TelnetInputPromptStr + Name + " " + maskedArgs + " [MASKED ARGS]");
                }
                else
                    LogManager.Log("RCON - " + currentClient.Username + TelnetInputPromptStr + Name + " " + string.Join(" ", args));

                if(command != null)
                    return command(args);

                return "ERROR: Undefined command function '" + Name + "'\r\nNo function was set to invoke";
            }
        }


        static Thread rconListenThread = null;
        static TcpListener server = null;
        static int Port = RCON_DEFAULT_PORT;
        static int RCONConnectionMaxTTL = RCON_DEFAULT_TTL_MAX;
        static int RCONConnectionInputTTL = RCON_DEFAULT_TTL_INPUT;

        readonly static byte[] CRLF = Encoding.ASCII.GetBytes("\r\n");
        readonly static string TelnetInputPromptStr = " => ";
        readonly static byte[] TelnetInputPrompt = Encoding.ASCII.GetBytes(TelnetInputPromptStr);
        readonly static string UsernamePrompt = "Please enter your username (Enter nothing to disconnect):";
        readonly static string PasswordPrompt = "Please enter your password:";
        readonly static string LoginSuccessfulText = "\r\nLogin successful\r\nWelcome ";
        readonly static string LoginFailedText = "\r\nERROR: Login failed\r\n" + UsernamePrompt;
        static string ttlFullExpiredText = "\r\n\r\nConnection exeeeded max idle time of " + RCON_DEFAULT_TTL_MAX + " seconds.\r\nYou have been forceably disconnected";
        static string ttlInputExpiredText = "\r\n\r\nNo input recieved for " + RCON_DEFAULT_TTL_INPUT + " seconds.\r\nYou have been forceably disconnected";

        static Dictionary<string, RCONCommand> commands = new Dictionary<string, RCONCommand>();
        static Dictionary<string, string> ValidRCONClients = new Dictionary<string, string>();
        static string authType = "basic";

        [ThreadStatic]
        static RCONClient currentClient = null;

        public override bool Setup()
        {
            Port = BismuthConfig.GetConfigValue<int>("RCON.Port", RCON_DEFAULT_PORT, "RCON");
            server = new TcpListener(IPAddress.Any, Port); //TODO: Config
            server.Start();
            LogManager.WriteLine("RCON server started for " + server.LocalEndpoint.ToString(), ConsoleColor.Green);

            RCONConnectionMaxTTL = BismuthConfig.GetConfigValue<int>("RCON.MaxTimeout", RCON_DEFAULT_TTL_MAX, "RCON");
            RCONConnectionInputTTL = BismuthConfig.GetConfigValue<int>("RCON.InputTimeout", RCON_DEFAULT_TTL_INPUT, "RCON");

            ttlFullExpiredText = "\r\n\r\nConnection exeeeded max idle time of " + RCONConnectionMaxTTL + " seconds.\r\nYou have been forceably disconnected";
            ttlInputExpiredText = "\r\n\r\nNo input recieved for " + RCONConnectionInputTTL + " seconds.\r\nYou have been forceably disconnected";

            authType = BismuthConfig.GetConfigValue<string>("RCON.AuthMethod", "basic", "RCON");
            List<object> RCONClients = BismuthConfig.GetConfigValue<List<object>>("RCON.Users", "RCON");
            if (RCONClients == null || RCONClients.Count == 0)
            {
                LogManager.Critical("RCON", "No users defined for RCON! Until this is resolved, anyone can access RCON!");
                LogManager.Critical("RCON", "  It might be wise to disable RCON or limit its access to localhost.");
            }
            else
            {
                int vbarPos;
                string fullstring, username, password;

                for (int i = 0; i < RCONClients.Count; ++i)
                {
                    fullstring = RCONClients[i].ToString();
                    vbarPos = fullstring.IndexOf('|');

                    if(vbarPos < 0)
                    {
                        LogManager.Warn("RCON", "RCON User " + RCONClients[i] + " has no password defined. Skipping.");
                        continue;
                    }

                    username = fullstring.Substring(0, vbarPos);
                    password = fullstring.Substring(vbarPos + 1);
                    ValidRCONClients.Add(username, password);
                }
            }

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

            AddRCONCommand("rcon-add-user", "[SU] Adds a new RCON user encoded with the current auth method", (args) =>
                {
                    StringBuilder returnString = new StringBuilder();

                    if (currentClient.Username[0] != '@')
                    {
                        returnString.Append("Error: You need to be a superuser to delete users.\r\n");
                    }
                    else if (args.Length != 2)
                    {
                        returnString.Append("Invalid number of parameters. Expected 2.\r\n");
                        returnString.Append("Usage: rcon-add-user <username> <password>\r\n");
                    }
                    else
                    {
                        ValidRCONClients.Add(args[0], AuthManager.GetEncryptedText(authType, args[1]));
                        returnString.Append("Added user '" + args[0] + "' with password encoded via auth method " + authType + ".\r\n");
                        LogManager.Log("RCON - " + currentClient.Username + " added RCON user " + args[0]);
                    }

                    return returnString.ToString();
                }, true);
            AddRCONCommand("rcon-delete-user", "[SU] Deletes a given RCON user", (args) =>
            {
                StringBuilder returnString = new StringBuilder();

                if (currentClient.Username[0] != '@')
                {
                    returnString.Append("Error: You need to be a superuser to delete users.\r\n");
                }
                else if (args.Length != 1)
                {
                    returnString.Append("Invalid number of parameters. Expected 1.\r\n");
                    returnString.Append("Usage: rcon-delete-user <username>\r\n");
                }
                else if (ValidRCONClients.ContainsKey(args[0]))
                {
                    ValidRCONClients.Remove(args[0]);
                    returnString.Append("Deleted user '" + args[0] + "'.\r\n");
                    LogManager.Log("RCON - " + currentClient.Username + " deleted RCON user " + args[0]);
                }
                else
                {
                    returnString.Append("User '" + args[0] + "' not found.\r\n");
                }

                return returnString.ToString();
            });

            AddRCONCommand("rcon-list-users", "Lists all registered RCON users", (args) =>
            {
                StringBuilder returnString = new StringBuilder();

                returnString.Append("Registered RCON users:\r\n");
                foreach(KeyValuePair<string, string> kvp in ValidRCONClients)
                    returnString.Append(kvp.Key + "\r\n");

                return returnString.ToString();
            });
            AddRCONCommand("rcon-change-password", "Changes the current user's password", (args) =>
            {
                StringBuilder returnString = new StringBuilder();

                if (args.Length != 2)
                {
                    returnString.Append("Invalid number of parameters. Expected 2\r\n");
                    returnString.Append("Usage: rcon-change-password <current password> <new password>\r\n");
                }
                else
                {
                    if (AuthManager.CheckPlaintextCredentials(authType, args[0], ValidRCONClients[currentClient.Username]))
                    {
                        ValidRCONClients[currentClient.Username] = AuthManager.GetEncryptedText(authType, args[1]);
                        returnString.Append("Password successfully changed.\r\n");
                    }
                    else
                    {
                        returnString.Append("Current password was incorrect. Password not changed.\r\n");
                    }
                }

                return returnString.ToString();
            }, true);
            return true;
        }

        public static void AddRCONCommand(string command, string documentation, Func<string[], object> function, bool hideArgs = false)
        {
            commands.Add(command, new RCONCommand(command, documentation, function, hideArgs));
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
            if (ValidRCONClients.Count == 0)
                currentClient.SetLoginState(RCONClient.ELoginState.LoggedIn);

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
                        if (timeSinceLastRequest.ElapsedMilliseconds > RCONConnectionMaxTTL * 1000)
                        {
                            SendClientResponse(stream, ttlFullExpiredText, true);
                            closingConnection = true;
                            break;
                        }
                        else if (timeSinceLastInput.ElapsedMilliseconds > RCONConnectionInputTTL * 1000)
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

                if (requestString == "" && currentClient.LoginState == RCONClient.ELoginState.NotLoggedIn)
                {
                    requestString = "quit";
                    currentClient.SetLoginState(RCONClient.ELoginState.LoggedIn); //Will be booted straight out again
                }

                if (currentClient.LoginState == RCONClient.ELoginState.NotLoggedIn)
                {
                    currentClient.SetUsername(requestString);
                    currentClient.SetLoginState(RCONClient.ELoginState.NeedPassword);
                    SendClientResponse(stream, PasswordPrompt);
                }
                else if (currentClient.LoginState == RCONClient.ELoginState.NeedPassword)
                {
                    if ((ValidRCONClients.ContainsKey(currentClient.Username) || ValidRCONClients.ContainsKey("@" + currentClient.Username)) && 
                        AuthManager.CheckPlaintextCredentials(authType, requestString, ValidRCONClients[currentClient.Username]))
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

                    string command = request[0].ToLower();
                    string[] commandArgs = request.Skip(1).ToArray();

                    if (command == "quit" || command == "disconnect" || command == "exit")
                    {
                        command = "quit";
                        closingConnection = true;
                    }

                    byte[] responseData;
                    if (commands.ContainsKey(command))
                    {
                        object response = commands[command].Invoke(commandArgs);
                        if (response is byte[])
                            responseData = (byte[])response;
                        else
                            responseData = Encoding.ASCII.GetBytes(response.ToString());
                    }
                    else
                    {
                        //Presume that the request contained secure data
                        LogManager.Log("RCON - " + currentClient.Username + TelnetInputPromptStr + command + " + " + commandArgs.Length + " args");
                        responseData = Encoding.ASCII.GetBytes("Error: Unknown command '" + command + "'");
                    }

                    SendClientResponse(stream, responseData, closingConnection);
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
