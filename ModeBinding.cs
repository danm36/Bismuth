using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public class ModeFlagBindings
    {
        private static Dictionary<string, MFBinding> mfBindings = new Dictionary<string, MFBinding>();

        public static void AddBindings(string identifier, string documentation, string[] flags)
        {
            identifier = identifier.ToUpper();

            if (!mfBindings.ContainsKey(identifier))
                mfBindings.Add(identifier, new MFBinding(documentation));

            for (int i = 0; i < flags.Length; i++)
            {
                if(flags[i].Length == 1)
                    mfBindings[identifier].AddFlag(flags[i][0]);
                else
                    mfBindings[identifier].AddFlag(flags[i]);
            }
        }

        public static bool SetFlag(char flag)
        {
            bool hasSet = false;
            foreach (KeyValuePair<string, MFBinding> kvp in mfBindings)
            {
                if (kvp.Value.HasFlag(flag))
                {
                    kvp.Value.Set();
                    hasSet = true;
                }
            }

            return hasSet;
        }

        public static bool SetFlag(string flag)
        {
            bool hasSet = false;
            foreach (KeyValuePair<string, MFBinding> kvp in mfBindings)
            {
                if (kvp.Value.HasFlag(flag))
                {
                    kvp.Value.Set();
                    hasSet = true;
                }
            }

            return hasSet;
        }

        public static bool IsFlagSet(string identifier)
        {
            identifier = identifier.ToUpper();
            if (!mfBindings.ContainsKey(identifier))
                return false;

            return mfBindings[identifier].IsSet();
        }

        public static void PrintHelp()
        {
            Console.WriteLine("Option flags:");
            Console.WriteLine();
            foreach (KeyValuePair<string, MFBinding> kvp in mfBindings)
            {
                char[] chrFlags = kvp.Value.GetCharFlags();
                string[] strFlags = kvp.Value.GetStringFlags();

                string helpString = "  ";
                for (int i = 0; i < chrFlags.Length; i++)
                    helpString += (i > 0 ? ", " : "") + "-" + chrFlags[i];

                helpString = helpString.PadRight(11);
                helpString += " "; //Force a space at end regardless of padding

                for (int i = 0; i < strFlags.Length; i++)
                    helpString += (i > 0 ? ", " : "") + "--" + strFlags[i];

                helpString = helpString.PadRight(23);
                helpString += " "; //Force a space at end regardless of padding

                helpString += kvp.Value.documentation;

                Console.WriteLine(helpString);
            }
        }

        private class MFBinding
        {
            List<char> chrFlags = new List<char>();
            List<string> strFlags = new List<string>();
            private bool isSet = false;
            public string documentation = "";

            public MFBinding(string nDocumentation)
            {
                documentation = nDocumentation;
            }

            public void AddFlag(char flag)
            {
                chrFlags.Add(flag);
            }

            public void AddFlag(string flag)
            {
                strFlags.Add(flag);
            }

            public bool HasFlag(char flag)
            {
                for (int i = 0; i < chrFlags.Count; i++)
                {
                    if (chrFlags[i] == flag)
                        return true;
                }
                return false;
            }

            public bool HasFlag(string flag)
            {
                for (int i = 0; i < strFlags.Count; i++)
                {
                    if (strFlags[i] == flag)
                        return true;
                }
                return false;
            }

            public char[] GetCharFlags()
            {
                return chrFlags.ToArray();
            }

            public string[] GetStringFlags()
            {
                return strFlags.ToArray();
            }


            public void Set()
            {
                isSet = true;
            }

            public void Unset()
            {
                isSet = false;
            }


            public bool IsSet()
            {
                return isSet;
            }
        }
    }
}
