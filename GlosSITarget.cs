using Playnite.SDK.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;

namespace GlosSIIntegration
{
    static class GlosSITarget
    {
        private static readonly string TARGET_FILENAME_PREFIX = "[GI] ";

        // TODO: Testing
        /// <summary>
        /// Creates a GlosSITarget for a game, using the default .json structure.
        /// </summary>
        /// <param name="playniteGame">The game to be added to GlosSI</param>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        public static void Create(Game playniteGame)
        {
            string jsonString = File.ReadAllText("DefaultTarget.json");
            JObject jObject = (JObject) Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);

            jObject.SelectToken("name").Replace(playniteGame.Name);
            jObject.SelectToken("icon").Replace(playniteGame.Icon);

            string jsonFileName = GetJsonFileName(playniteGame.GameId);
            jsonString = jObject.ToString();

            // TODO: glosSITargetsPath & INTEGRATED_TAG_ID
            File.WriteAllText(glosSITargetsPath + jsonFileName, jsonString);
            playniteGame.TagIds.Add(INTEGRATED_TAG_ID);
            SaveToSteamShortcuts(jsonFileName);
        }

        public static string GetJsonFileName(string playniteGameId)
        {
            return TARGET_FILENAME_PREFIX + playniteGameId + ".json";
        }

        private static void SaveToSteamShortcuts(string jsonFileName)
        {
            // TODO: glosSIConfigPath & steamShortcutsPath
            Process glosSIConfig = Process.Start(glosSIConfigPath, $"add {jsonFileName} {steamShortcutsPath}");
            glosSIConfig.WaitForExit();
        }
    }
}
