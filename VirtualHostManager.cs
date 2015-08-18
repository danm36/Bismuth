using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public static class VirtualHostManager
    {
        static Dictionary<string, VirtualHost> vhosts = new Dictionary<string, VirtualHost>();

        public static void Setup()
        {
            AddVirtualHost("*", new VirtualHost("H:/htdocs"));
            AddVirtualHost("deventas.co.uk", new VirtualHost(Environment.CurrentDirectory + "/htdocs"));
            AddVirtualHost("216.158.230.84", new VirtualHost(Environment.CurrentDirectory + "/htdocs"));
        }

        public static void AddVirtualHost(string domain, VirtualHost vhost)
        {
            vhosts.Add(domain, vhost);
        }

        public static VirtualHost GetVirtualHost(string host)
        {
            if (host == null || host.StartsWith("localhost") || host.StartsWith("127.0.0.1"))
                host = "*:" + NetworkManager.Port;

            int colonIndex = host.IndexOf(':');
            if (colonIndex >= 0)
            {
                string hostPortStr = host.Substring(colonIndex + 1);
                host = host.Substring(0, colonIndex);
                int hostPort = -1;
                if (!int.TryParse(hostPortStr, out hostPort) || hostPort != NetworkManager.Port)
                    return null;
            }

            if (vhosts.ContainsKey(host))
                return vhosts[host];

            return null;
        }
    }
}
