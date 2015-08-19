using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public class BismuthGenericManager
    {
        public virtual bool Setup() { return true; }
        public virtual bool Shutdown() { return true; }
    }

    public class BismuthManagerInfo : Attribute
    {
        public string UID;
        public string Name;
        public string Description;

        public BismuthManagerInfo(string ManagerID, string ManagerName, string ManagerDescription)
        {
            UID = ManagerID;
            Name = ManagerName;
            Description = ManagerDescription;
        }
    }
}
