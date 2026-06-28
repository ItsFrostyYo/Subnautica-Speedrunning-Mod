using System;
using System.IO;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime
{
    [Serializable]
    public sealed class LoaderConfig
    {
        public bool EnableNetworking { get; set; }
        public string ApiBaseUrl { get; set; }
        public string ModEnvironmentName { get; set; }
        public bool EnableCrashUpload { get; set; }
        public string ModuleFolder { get; set; }
        public string LogLevel { get; set; }

        public LoaderConfig()
        {
            EnableNetworking = false;
            ApiBaseUrl = "https://example.invalid/";
            ModEnvironmentName = "production";
            EnableCrashUpload = false;
            ModuleFolder = "Modules";
            LogLevel = "Info";
        }
    }

    internal static class LoaderConfigStore
    {
        public static LoaderConfig Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    LoaderConfig defaultConfig = new LoaderConfig();
                    Save(path, defaultConfig);
                    return defaultConfig;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(LoaderConfig));
                LoaderConfig config;
                using (FileStream stream = File.OpenRead(path))
                {
                    config = serializer.Deserialize(stream) as LoaderConfig;
                }

                if (config == null)
                {
                    config = new LoaderConfig();
                }

                Save(path, config);
                return config;
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to load config. Falling back to defaults.", ex);
                return new LoaderConfig();
            }
        }

        public static void Save(string path, LoaderConfig config)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            XmlSerializer serializer = new XmlSerializer(typeof(LoaderConfig));
            using (FileStream stream = File.Create(path))
            {
                serializer.Serialize(stream, config);
            }
        }
    }
}
