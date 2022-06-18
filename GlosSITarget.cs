﻿using Playnite.SDK.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
using System;

namespace GlosSIIntegration
{
    static class GlosSITarget
    {
        private static readonly string TARGET_FILENAME_PREFIX = "[GI] ";
        private static readonly string STEAM_SOURCE = "Steam";

        // TODO: Testing
        /// <summary>
        /// Creates a GlosSITarget for a game, using the default .json structure. 
        /// Steam games, already integrated games and games tagged for ignoring are ignored.
        /// </summary>
        /// <param name="playniteGame">The game to be added to GlosSI</param>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        public static void Create(Game playniteGame)
        {
            // TODO: It might be a bad idea to simply compare the name of the source.
            if (playniteGame.Source.Name == STEAM_SOURCE || 
                GlosSIIntegration.GameHasIgnoredTag(playniteGame) || 
                GlosSIIntegration.GameHasIntegratedTag(playniteGame)) return;

            string jsonString = File.ReadAllText("DefaultTarget.json");
            JObject jObject = (JObject) Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);

            jObject.SelectToken("name").Replace(playniteGame.Name);
            jObject.SelectToken("icon").Replace(playniteGame.Icon);

            string jsonFileName = GetJsonFileName(playniteGame.GameId);
            jsonString = jObject.ToString();

            // TODO: INTEGRATED_TAG_ID
            File.WriteAllText(GetJsonFilePath(jsonFileName), jsonString);
            playniteGame.TagIds.Add(INTEGRATED_TAG_ID);
            SaveToSteamShortcuts(jsonFileName);
        }

        public static string GetJsonFileName(string playniteGameId)
        {
            return TARGET_FILENAME_PREFIX + playniteGameId + ".json";
        }

        private static string GetJsonFilePath(string jsonFileName)
        {
            return Environment.ExpandEnvironmentVariables("%appdata%/GlosSI/Targets/" + jsonFileName);
        }

        public static bool HasJsonFile(Game playniteGame)
        {
            return HasJsonFile(GetJsonFileName(playniteGame.GameId));
        }

        public static bool HasJsonFile(string jsonFileName)
        {
            return File.Exists(GetJsonFilePath(jsonFileName));
        }

        private static int GetIndexOfIntegratedTag(Game game)
        {
            return game.Tags.FindIndex(t => t.Name == GlosSIIntegration.INTEGRATED_TAG);
        }

        public static void Remove(Game game)
        {
            int integratedTagIndex = GetIndexOfIntegratedTag(game);

            if (integratedTagIndex == -1) return;
            game.Tags.RemoveAt(integratedTagIndex);

            string jsonFileName = GetJsonFileName(game.GameId);
            RemoveFromSteamShortcuts(jsonFileName);
            RemoveJsonFile(jsonFileName);

        }

        private static void RemoveJsonFile(string jsonFileName)
        {
            if(HasJsonFile(jsonFileName))
            {
                File.Delete(GetJsonFilePath(jsonFileName));
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
        /// <param name="jsonFileName">The filname of the .json GlosSITarget profile.</param>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private static void SaveToSteamShortcuts(string jsonFileName)
        {
            RunGlosSIConfigWithArguments("add", jsonFileName);
        }

        /// <summary>
        /// Removes the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <param name="jsonFileName">The filname of the .json GlosSITarget profile.</param>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private static void RemoveFromSteamShortcuts(string jsonFileName)
        {
            RunGlosSIConfigWithArguments("remove", jsonFileName);
        }

        private static void RunGlosSIConfigWithArguments(string initialArgument, string jsonFileName)
        {
            // TODO: glosSIConfigPath
            Process glosSIConfig = Process.Start(glosSIConfigPath, $"{initialArgument} \"{jsonFileName}\" \"{GetSteamShortcutsPath()}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
