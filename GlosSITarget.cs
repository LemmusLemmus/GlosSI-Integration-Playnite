﻿using Playnite.SDK.Models;
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
            if (IsSteamGame() || 
                GlosSIIntegration.GameHasIgnoredTag(playniteGame) || 
                GlosSIIntegration.GameHasIntegratedTag(playniteGame)) return;

            string jsonString = File.ReadAllText("DefaultTarget.json");
            JObject jObject = (JObject) Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);

            jObject.SelectToken("name").Replace(playniteGame.Name);
            jObject.SelectToken("icon").Replace(playniteGame.Icon);

            jsonString = jObject.ToString();

            File.WriteAllText(GetJsonFilePath(), jsonString);
            GlosSIIntegration.AddTagToGame(GlosSIIntegration.INTEGRATED_TAG, playniteGame);
            SaveToSteamShortcuts();
        }

        private bool IsSteamGame()
        {
            return playniteGame.Source.Name == STEAM_SOURCE || 
                (playniteGame.InstallDirectory != null && 
                Path.GetFullPath(playniteGame.InstallDirectory).Contains("Steam\\steamapps\\common"));
        }

        private string GetJsonFilePath()
        {
            return Path.Combine(GlosSIIntegration.GetSettings().GlosSITargetsPath, jsonFileName);
        }

        public bool HasJsonFile()
        {
            return File.Exists(GetJsonFilePath());
        }

        public void Remove()
        {
            if(GlosSIIntegration.GameHasIntegratedTag(playniteGame))
            {
                GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.INTEGRATED_TAG, playniteGame);
                RemoveFromSteamShortcuts();
                RemoveJsonFile();
            }
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
            Process glosSIConfig = Process.Start(Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSIConfig.exe"), 
                $"{initialArgument} \"{jsonFileName}\" \"{GetSteamShortcutsPath()}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
