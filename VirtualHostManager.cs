using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    [BismuthManagerInfo("BISMUTH_VHOST", "Virtual Host Manager", "Handles Virtual Hosts")]
    public class VirtualHostManager : BismuthGenericManager
    {
        static Dictionary<string, VirtualHost> vhosts = new Dictionary<string, VirtualHost>();

        public override bool Setup()
        {
            AddVirtualHost("*", "htdocs");
            AddVirtualHost("deventas.co.uk", "htdocs");
            AddVirtualHost("216.158.230.84", "htdocs");

            RCON.RCONServer.AddRCONCommand("list-vhosts", "Lists all virtual hosts", (args) =>
            {
                StringBuilder toReturn = new StringBuilder();
                toReturn.Append("List of Virtual Hosts:");

                foreach (KeyValuePair<string, VirtualHost> vhost in vhosts)
                {
                    toReturn.Append("\r\n" + vhost.Value.Domain.PadRight(31) + " - " + vhost.Value.LocalRootDirectory);
                }

                return toReturn.ToString();
            });

            return true;
        }

        public static void AddVirtualHost(string domain, string rootDirectory)
        {
            vhosts.Add(domain, new VirtualHost(domain, rootDirectory));
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
