using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Bismuth
{
    [BismuthManagerInfo("BISMUTH_NETWORK", "Network Manager", "Handles HTTP Communications on the network")]
    public class NetworkManager : BismuthGenericManager
    {
        static TcpListener server = null;
        static List<Thread> threadPool = new List<Thread>();

        public static int Port { get; private set; }
        public static int ConnectionTTL { get; private set; }

        public override bool Setup()
        {
            Port = 8080;
            server = new TcpListener(IPAddress.Any, Port); //TODO: Config
            server.Start();
            LogManager.WriteLine("HTTP server started for " + server.LocalEndpoint.ToString(), ConsoleColor.Green);

            ConnectionTTL = 5000;

            return true;
        }

        public override bool Shutdown()
        {
            for (int i = 0; i < threadPool.Count; i++)
            {
                threadPool[i].Abort();
            }

            threadPool.Clear();
            server.Stop();

            return true;
        }

        public static void ListenForNewConnections()
        {
            if (!Program.ShutDown && server.Pending())
            {
                TcpClient client = server.AcceptTcpClient();
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientThread));
                threadPool.Add(clientThread);
                clientThread.Start(client);
            }
        }

        public static void ManageThreadPool()
        {
            for (int i = 0; i < threadPool.Count; i++)
            {
                if (!threadPool[i].IsAlive)
                    threadPool.RemoveAt(i--);
            }
        }

        private static void HandleClientThread(object clientObj)
        {
            TcpClient client = (TcpClient)clientObj;
            try
            {
                HandleClient(client);
            }
            catch (ThreadAbortException)
            {
                LogManager.Warn(client, "Connection forcably closed - Thread aborted");
                if(client.Connected)
                    client.Close();
            }
            catch (Exception e)
            {
                LogManager.Error(client, "Connection forcably closed - An exception occured:");
                LogManager.Error(client, e.ToString());
                if (client.Connected)
                    client.Close();
            }
        }

        private static void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            bool closingConnection = false;
            EHTTPVersion lastHTTPVersion = EHTTPVersion.HTTP10;

            LogManager.Log(client, "Opening connection - Thread Started");

            Stopwatch timeSinceLastRequest = new Stopwatch();
            while (!closingConnection)
            {
                timeSinceLastRequest.Restart();

                while (client.Connected && client.Available == 0)
                {
                    if(timeSinceLastRequest.ElapsedMilliseconds > ConnectionTTL)
                    {
                        closingConnection = true;
                        break;
                    }
                }

                if (closingConnection || !client.Connected)
                    break;

                MemoryStream ms = new MemoryStream();
                while (client.Available > 0)
                {
                    byte[] pdata = new byte[client.Available];
                    stream.Read(pdata, 0, pdata.Length);
                    ms.Write(pdata, 0, pdata.Length);
                }

                byte[] requestData = ms.ToArray();

                //Hand data to plugins if necessary

                //HTTP parse
                string request = Encoding.ASCII.GetString(requestData, 0, requestData.Length);
                HTTPHeaderData requestHeader = new HTTPHeaderData(request);

                if (requestHeader.InvalidHeader)
                {
                    //Maybe it's SSL? Can't handle that
                    LogManager.Error(client, "Recieved SSL - Cannot Handle (Yet)");
                    HTTPResponse sslErrorResponse = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R501_NotImplemented, null);
                    sslErrorResponse.WriteToStream(stream, requestHeader, true);
                    break;
                }

                if (!requestHeader.HasHeaderField("Connection") || !requestHeader.GetHeaderField("Connection").Contains("keep-alive"))
                    closingConnection = true;

                lastHTTPVersion = requestHeader.HTTPVersion;

                //TODO: Plugin parse headers

                string requestLogStr = "Requested " + (requestHeader.HasHeaderField("Host") ? requestHeader.GetHeaderField("Host") : "") + requestHeader.GetRequestedResource();

                VirtualHost vhost = VirtualHostManager.GetVirtualHost(requestHeader.GetHeaderField("Host"));
                HTTPResponse response = null;
                if (vhost == null)
                {
                    response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R404_NotFound, requestHeader);
                    requestLogStr += " - 404";
                }
                else
                {
                    string resourceLoc = vhost.GetFinalResourceLocation(requestHeader);
                    string clientETag = HTTPResponse.MakeETag(resourceLoc);
                    string IFNONEMATCH = requestHeader.GetHeaderField("If-None-Match");
                    bool TEST = clientETag == IFNONEMATCH;

                    if (clientETag != null && (
                            (requestHeader.HasHeaderField("If-Modified-Since") && vhost.HasBeenModifiedSince(resourceLoc, DateTime.Parse(requestHeader.GetHeaderField("If-Modified-Since")))) ||
                            (requestHeader.HasHeaderField("If-None-Match") && requestHeader.GetHeaderField("If-None-Match") == clientETag)
                        ))
                    {
                        response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R304_NotModified, requestHeader);
                        requestLogStr += " - 304";
                    }
                    else
                    {
                        response = vhost.GetResource(requestHeader);
                    }
                }

                if (response == null)
                {
                    response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R500_InternalServerError, requestHeader);
                    requestLogStr += " - 500";
                }

                LogManager.Log(client, requestLogStr);
                response.WriteToStream(stream, requestHeader, closingConnection);
            }

            LogManager.Log(client, "Closing connection");
            client.Close();
        }
    }
}
