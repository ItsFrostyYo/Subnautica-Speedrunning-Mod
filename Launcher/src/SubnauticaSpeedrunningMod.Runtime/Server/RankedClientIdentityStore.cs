using System;
using System.IO;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModClientIdentityStore
    {
        private static readonly object Sync = new object();
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ModClientIdentity));
        private static bool _initialized;
        private static string _identityPath = string.Empty;
        private static ModClientIdentity _identity;

        public static void Initialize(RuntimeContext context)
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                string clientDataDirectory = Path.Combine(Path.Combine(context.ModRoot, "Data"), "Client");
                Directory.CreateDirectory(clientDataDirectory);
                _identityPath = Path.Combine(clientDataDirectory, "client-identity.xml");
                _identity = LoadOrCreateIdentity(_identityPath);
                _initialized = true;
                ModLog.Info("Client identity loaded: playerId='" + _identity.PlayerId + "', displayName='" + _identity.DisplayName + "'.");
            }
        }

        public static ModClientIdentity GetIdentity()
        {
            lock (Sync)
            {
                return _identity ?? new ModClientIdentity();
            }
        }

        private static ModClientIdentity LoadOrCreateIdentity(string path)
        {
            ModClientIdentity identity = null;
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        identity = Serializer.Deserialize(stream) as ModClientIdentity;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to read client identity file '" + path + "': " + ex.Message);
            }

            if (identity == null)
            {
                identity = new ModClientIdentity();
            }

            if (string.IsNullOrEmpty(identity.PlayerId))
            {
                identity.PlayerId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrEmpty(identity.DisplayName))
            {
                string machineName = Environment.MachineName ?? string.Empty;
                machineName = machineName.Trim();
                if (string.IsNullOrEmpty(machineName))
                {
                    machineName = "Player";
                }

                if (machineName.Length > 24)
                {
                    machineName = machineName.Substring(0, 24);
                }

                identity.DisplayName = machineName;
            }

            Save(path, identity);
            return identity;
        }

        private static void Save(string path, ModClientIdentity identity)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = File.Create(path))
            {
                Serializer.Serialize(stream, identity);
            }
        }
    }

    [Serializable]
    public sealed class ModClientIdentity
    {
        public string PlayerId { get; set; }
        public string DisplayName { get; set; }

        public ModClientIdentity()
        {
            PlayerId = string.Empty;
            DisplayName = string.Empty;
        }
    }
}
