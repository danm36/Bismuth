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
    public static class NetworkManager
    {
        static TcpListener server = null;
        static List<Thread> threadPool = new List<Thread>();

        public static int Port { get; private set; }
        public static int ConnectionTTL { get; private set; }

        public static bool Setup()
        {
            Port = 8080;
            server = new TcpListener(IPAddress.Any, Port); //TODO: Config
            server.Start();

            ConnectionTTL = 15000;

            return true;
        }

        public static void Shutdown()
        {
            for (int i = 0; i < threadPool.Count; i++)
            {
                threadPool[i].Abort();
            }

            threadPool.Clear();
            server.Stop();
        }

        public static void ListenForNewConnections()
        {
            if (server.Pending())
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
                client.Close();
                LogManager.Warn(client, "Connection forcably closed - Thread aborted");
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
                HTTPHeaderData header = new HTTPHeaderData(request);

                if (header.InvalidHeader)
                {
                    //Maybe it's SSL? Can't handle that
                    LogManager.Error(client, "Recieved SSL - Cannot Handle (Yet)");
                    HTTPResponse sslErrorResponse = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R501_NotImplemented, null);
                    sslErrorResponse.WriteToStream(stream, true);
                    break;
                }

                if (!header.HasHeaderField("Connection") || !header.GetHeaderField("Connection").Contains("keep-alive"))
                    closingConnection = true;

                lastHTTPVersion = header.HTTPVersion;

                //TODO: Plugin parse headers

                LogManager.Log(client, "Requested " + (header.HasHeaderField("Host") ? header.GetHeaderField("Host") : "") + header.GetRequestedResource());

                VirtualHost vhost = VirtualHostManager.GetVirtualHost(header.GetHeaderField("Host"));
                HTTPResponse response = null;
                if (vhost == null)
                {
                    response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R404_NotFound, header);
                }
                else
                {
                    response = vhost.GetResource(header);

                    if(header.HasHeaderField("ETag") && header.GetHeaderField("ETag") == response.ETag)
                        response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R304_NotModified, header);
                }

                if (response == null)
                    response = SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R500_InternalServerError, header);

                response.WriteToStream(stream, closingConnection);
            }

            LogManager.Log(client, "Closing connection");
            client.Close();
        }
    }
}
