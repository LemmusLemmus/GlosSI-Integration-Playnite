using Newtonsoft.Json;
using System.IO;

namespace GlosSIIntegration.Models
{
    // TODO: Use this class to keep track of created targets (and related Playnite tags).
    // For now, it is only used for version migration.
    [JsonObject(MemberSerialization.OptIn)]
    internal class KnownTargets
    {
        private const int CurrentVersion = 1;
        [JsonProperty]
        public int Version { get; }

        private KnownTargets()
        {
            // Note: When deserializing, do not set this property.
            Version = CurrentVersion;
        }

        public static void LoadTargets()
        {
            if (!File.Exists(GlosSIIntegration.GetSettings().KnownTargetsPath))
            {
                TargetsVersionMigrator.TryMigrate(0);
                new KnownTargets().Save();
            }
            else
            {
                // Next time migration is neccessary, call TryMigrate here with the deserialized Version.
            }
        }

        private void Save()
        {
            using (StreamWriter file = File.CreateText(GlosSIIntegration.GetSettings().KnownTargetsPath))
            {
                new JsonSerializer().Serialize(file, this);
            }
        }
    }
}
