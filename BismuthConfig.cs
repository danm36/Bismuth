using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public static class BismuthConfig
    {
        private class BismuthConfigSection
        {
            private string filter = "Server";
            Dictionary<string, object> configurations = new Dictionary<string, object>();

            public BismuthConfigSection(string pFilter)
            {
                filter = pFilter;
            }

            public bool MatchFilter(params string[] args)
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    if (args[i] != filter)
                        return false;
                }

                return true;
            }

            public void SetConfigValue(string key, string value)
            {
                if (configurations.ContainsKey(key))
                    configurations[key] = value;
                else
                    configurations.Add(key, value);
            }

            public void AddConfigValue(string key, string value)
            {
                if (!configurations.ContainsKey(key))
                    configurations.Add(key, new List<object>() { value });
                else if (configurations[key] is IList)
                    ((IList)configurations[key]).Add(value);
                else
                    configurations[key] = new List<object>() { configurations[key], value };
            }

            public void RemoveConfigValue(string key, string value)
            {
                if (configurations.ContainsKey(key))
                {
                    if (configurations[key] is IList)
                        ((IList)configurations[key]).Remove(value);
                    else if(configurations[key].ToString() == value)
                        configurations[key] = "";
                }
            }

            public bool HasConfigEntry(string key)
            {
                return configurations.ContainsKey(key);
            }

            public object GetConfigValue(string key)
            {
                if (configurations.ContainsKey(key))
                    return configurations[key];

                LogManager.Warn("Missing of invalid config entry for " + key);
                return null;
            }

            public T GetConfigValue<T>(string key)
            {
                if (configurations.ContainsKey(key))
                {
                    if (configurations[key] is T)
                        return (T)configurations[key];
                    try
                    {
                        return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(configurations[key]);
                    }
                    catch
                    {
                        LogManager.Error("Could not convert config value for " + key + " to " + typeof(T).FullName);
                    }
                }
                else
                {
                    LogManager.Warn("Missing of invalid config entry for " + key);
                }

                return default(T);
            }
        }
        static List<BismuthConfigSection> configSections = new List<BismuthConfigSection>();

        public static bool Setup()
        {
            if (!Directory.Exists("cfg"))
            {
                LogManager.Critical("CFG", "FATAL ERROR: The directory '" + Environment.CurrentDirectory + "/cfg/' could not be found.");
                LogManager.Critical("CFG", "             Configuration could not be found. Program aborted.");
                return false;
            }

            string[] cfgFiles = Directory.GetFiles("cfg");

            if (cfgFiles.Length == 0)
            {
                LogManager.Critical("CFG", "FATAL ERROR: No configuration files in '" + Environment.CurrentDirectory + "/cfg/'.");
                LogManager.Critical("CFG", "             Configuration could not be found. Program aborted.");
                return false;
            }

            foreach (string file in cfgFiles)
            {
                LogManager.Notice("CFG", "Loading CFG file " + file);
                string[] lines = File.ReadAllLines(file);
                string line;
                BismuthConfigSection currentSection = null;

                for (int i = 0; i < lines.Length; ++i)
                {
                    line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                        continue;

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        if (currentSection != null)
                            configSections.Add(currentSection);

                        currentSection = new BismuthConfigSection(line.Substring(1, line.Length - 2));
                        continue;
                    }

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex);
                        string value = line.Substring(equalsIndex + 1);

                        if (key[0] == '+')
                            currentSection.AddConfigValue(key.Substring(1), value);
                        else if(key[0] == '-')
                            currentSection.RemoveConfigValue(key.Substring(1), value);
                        else
                            currentSection.SetConfigValue(key, value);
                    }
                    else
                    {
                        LogManager.Warn("CFG", "Invalid line in config file " + file + ". Skipping. Line:");
                        LogManager.Warn("CFG", line);
                    }
                }

                if (currentSection != null)
                    configSections.Add(currentSection);
                currentSection = null;
            }

            LogManager.Notice("CFG", "Config load complete. Loaded " + configSections.Count + " sections");
            return true;
        }

        public static object GetConfigValue(string key)
        {
            return GetConfigValue(key, (string[])null);
        }

        public static object GetConfigValue(string key, object defaultIfNotFound)
        {
            return GetConfigValue(key, defaultIfNotFound, (string[])null);
        }

        public static object GetConfigValue(string key, string filter)
        {
            return GetConfigValue(key, null, new string[] { filter });
        }

        public static object GetConfigValue(string key, string[] filters)
        {
            return GetConfigValue(key, null, filters);
        }

        public static object GetConfigValue(string key, object defaultIfNotFound, string filter)
        {
            return GetConfigValue(key, defaultIfNotFound, new string[] { filter });
        }

        public static object GetConfigValue(string key, object defaultIfNotFound, string[] filters)
        {
            object currentObject = null;
            List<string> trueFilters = new List<string>() { "Server" };
            if (filters != null && filters.Length > 0)
                trueFilters.AddRange(filters);

            foreach (BismuthConfigSection section in configSections)
            {
                if (!section.HasConfigEntry(key))
                    continue;

                for (int i = 0; i < trueFilters.Count; i++)
                {
                    if (section.MatchFilter(trueFilters[i]))
                        currentObject = section.GetConfigValue(key);
                }
            }

            return currentObject;
        }

        public static T GetConfigValue<T>(string key)
        {
            return GetConfigValue<T>(key, (string[])null);
        }

        public static T GetConfigValue<T>(string key, T defaultIfNotFound)
        {
            return GetConfigValue<T>(key, defaultIfNotFound, (string[])null);
        }

        public static T GetConfigValue<T>(string key, string filter)
        {
            return GetConfigValue<T>(key, default(T), new string[] { filter });
        }

        public static T GetConfigValue<T>(string key, string[] filters)
        {
            return GetConfigValue<T>(key, default(T), filters);
        }

        public static T GetConfigValue<T>(string key, T defaultIfNotFound, string filter)
        {
            return GetConfigValue<T>(key, defaultIfNotFound, new string[] { filter });
        }

        public static T GetConfigValue<T>(string key, T defaultIfNotFound, string[] filters)
        {
            T currentObject = defaultIfNotFound;
            List<string> trueFilters = new List<string>() { "Server" };
            if (filters != null && filters.Length > 0)
                trueFilters.AddRange(filters);

            foreach (BismuthConfigSection section in configSections)
            {
                if (!section.HasConfigEntry(key))
                    continue;

                for (int i = 0; i < trueFilters.Count; i++)
                {
                    if (section.MatchFilter(trueFilters[i]))
                        currentObject = section.GetConfigValue<T>(key);
                }
            }

            return currentObject;
        }

    }
}
