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
        /// <returns>true if the GlosSITarget was created; false if the game was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        public bool Create()
        {
            if (IsSteamGame() || 
                GlosSIIntegration.GameHasIgnoredTag(playniteGame) || 
                GlosSIIntegration.GameHasIntegratedTag(playniteGame)) return false;

            SaveAsJsonTarget();
            SaveToSteamShortcuts();
            GlosSIIntegration.AddTagToGame(GlosSIIntegration.INTEGRATED_TAG, playniteGame);
            return true;
        }

        private void SaveAsJsonTarget()
        {
            string jsonString = File.ReadAllText(GlosSIIntegration.GetSettings().DefaultTargetPath);
            JObject jObject = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);

            jObject.SelectToken("name").Replace(playniteGame.Name);
            jObject.SelectToken("icon").Replace(playniteGame.Icon);

            jsonString = jObject.ToString();

            File.WriteAllText(GetJsonFilePath(), jsonString);
        }

        private bool IsSteamGame()
        {
            return (playniteGame.Source != null && playniteGame.Source.Name == STEAM_SOURCE) || 
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

        /// <summary>
        /// Removes the integration of a game. 
        /// This removes the game's integrated tag, GlosSITarget and entry in Steam shortcuts.vdf file.
        /// </summary>
        /// <returns>true if the integration was removed; false if it was nonexistent to begin with.</returns>
        public bool Remove()
        {
            if(GlosSIIntegration.GameHasIntegratedTag(playniteGame))
            {
                GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.INTEGRATED_TAG, playniteGame);
                RemoveFromSteamShortcuts();
                RemoveJsonFile();
                return true;
            }
            else
            {
                return false;
            }
        }

        private void RemoveJsonFile()
        {
            if(HasJsonFile())
            {
                File.Delete(GetJsonFilePath());
            }
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
                $"{initialArgument} \"{jsonFileName}\" \"{GlosSIIntegration.GetSettings().SteamShortcutsPath}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
