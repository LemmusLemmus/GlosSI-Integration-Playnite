using Playnite.SDK.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
using System;

namespace GlosSIIntegration
{
    class GlosSITarget
    {
        private static readonly string TARGET_FILENAME_PREFIX = "[GI] ";
        private static readonly string STEAM_SOURCE = "Steam";

        private readonly Game playniteGame;
        private readonly string jsonFileName; // The filname of the .json GlosSITarget profile.

        public GlosSITarget(Game playniteGame)
        {
            this.playniteGame = playniteGame;
            this.jsonFileName = TARGET_FILENAME_PREFIX + playniteGame.GameId + ".json";
        }

        // TODO: Testing
        /// <summary>
        /// Creates a GlosSITarget for a game, using the default .json structure. 
        /// Steam games, already integrated games and games tagged for ignoring are ignored.
        /// </summary>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        public void Create()
        {
            // TODO: It might be a bad idea to simply compare the name of the source.
            if (playniteGame.Source.Name == STEAM_SOURCE || 
                GlosSIIntegration.GameHasIgnoredTag(playniteGame) || 
                GlosSIIntegration.GameHasIntegratedTag(playniteGame)) return;

            string jsonString = File.ReadAllText("DefaultTarget.json");
            JObject jObject = (JObject) Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);

            jObject.SelectToken("name").Replace(playniteGame.Name);
            jObject.SelectToken("icon").Replace(playniteGame.Icon);

            jsonString = jObject.ToString();

            // TODO: INTEGRATED_TAG_ID
            File.WriteAllText(GetJsonFilePath(), jsonString);
            playniteGame.TagIds.Add(INTEGRATED_TAG_ID);
            SaveToSteamShortcuts();
        }

        public string GetJsonFileName()
        {
            return jsonFileName;
        }

        private string GetJsonFilePath()
        {
            return Environment.ExpandEnvironmentVariables("%appdata%/GlosSI/Targets/" + jsonFileName);
        }

        public bool HasJsonFile()
        {
            return File.Exists(GetJsonFilePath());
        }

        private int GetIndexOfIntegratedTag()
        {
            return playniteGame.Tags.FindIndex(t => t.Name == GlosSIIntegration.INTEGRATED_TAG);
        }

        public void Remove()
        {
            int integratedTagIndex = GetIndexOfIntegratedTag();

            if (integratedTagIndex == -1) return;
            playniteGame.Tags.RemoveAt(integratedTagIndex);
            RemoveFromSteamShortcuts();
            RemoveJsonFile();

        }

        private void RemoveJsonFile()
        {
            if(HasJsonFile())
            {
                File.Delete(GetJsonFilePath());
            }
        }

        private static string GetSteamShortcutsPath() // TODO: Fix this!
        {
            string steamUserdataPath = Environment.ExpandEnvironmentVariables("%programfiles(x86)%/Steam/userdata");
            return null;
        }

        /// <summary>
        /// Saves the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private void SaveToSteamShortcuts()
        {
            RunGlosSIConfigWithArguments("add");
        }

        /// <summary>
        /// Removes the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <param name="jsonFileName">The filname of the .json GlosSITarget profile.</param>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private void RemoveFromSteamShortcuts()
        {
            RunGlosSIConfigWithArguments("remove");
        }

        private void RunGlosSIConfigWithArguments(string initialArgument)
        {
            // TODO: glosSIConfigPath
            Process glosSIConfig = Process.Start(glosSIConfigPath, $"{initialArgument} \"{jsonFileName}\" \"{GetSteamShortcutsPath()}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
