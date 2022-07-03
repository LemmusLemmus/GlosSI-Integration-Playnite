using Playnite.SDK.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;

namespace GlosSIIntegration
{
    class GlosSITarget
    {
        private static readonly string STEAM_SOURCE = "Steam";

        private readonly Game playniteGame;
        // The filname of the .json GlosSITarget profile, without the extension.
        private readonly string jsonFileName;

        public GlosSITarget(Game playniteGame)
        {
            this.playniteGame = playniteGame;
            jsonFileName = RemoveIllegalFileNameChars(playniteGame.Name);
        }

        public static string RemoveIllegalFileNameChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

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

            try
            {
                jObject.SelectToken("name").Replace(playniteGame.Name);
                jObject.SelectToken("icon").Replace(GetGameIconPath());
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("The GlosSI default target is missing items.");
            }

            jsonString = jObject.ToString();

            // TODO: Send a warning message if there already exists a .json file with the same path but with a name.
            // There is a risk that two different games with different game names have the same filename after illegal characters are removed.

            File.WriteAllText(GetJsonFilePath(), jsonString);
        }

        private string GetGameIconPath()
        {
            return Path.Combine(GlosSIIntegration.API.Paths.ConfigurationPath, @"library\files\", playniteGame.Icon);
        }

        private bool IsSteamGame()
        {
            return (playniteGame.Source != null && playniteGame.Source.Name == STEAM_SOURCE) || 
                (playniteGame.InstallDirectory != null && 
                Path.GetFullPath(playniteGame.InstallDirectory).Contains(@"Steam\steamapps\common"));
        }

        public static string GetJsonFilePath(string jsonFileName)
        {
            return Path.Combine(GlosSIIntegration.GetSettings().GlosSITargetsPath, jsonFileName + ".json");
        }

        private string GetJsonFilePath()
        {
            return GetJsonFilePath(jsonFileName);
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
            // When adding, GlosSI takes the game name without illegal file name characters.
            RunGlosSIConfigWithArguments("add", "\"" + jsonFileName + "\"");
        }

        /// <summary>
        /// Removes the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private void RemoveFromSteamShortcuts()
        {
            // TODO: There is a risk that the user changes the name of the game.
            // The name should therefore be taken from the json file instead.
            // There will have to be a way to identify which json file belongs to which game though.

            // When removing, GlosSI takes the game name with all characters, including illegal file name characters.
            RunGlosSIConfigWithArguments("remove", GetCommandLineArgumentSafeString(playniteGame.Name));
        }

        private static string GetCommandLineArgumentSafeString(string str)
        {
            // Credit to Stack Overflow user Nas Banov for this magic.
            str = Regex.Replace(str, @"(\\*)" + "\"", @"$1$1\" + "\"");
            return "\"" + Regex.Replace(str, @"(\\+)$", @"$1$1") + "\"";
        }

        private void RunGlosSIConfigWithArguments(string initialArgument, string gameArgument)
        {
            Process glosSIConfig = Process.Start(Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSIConfig.exe"), 
                $"{initialArgument} {gameArgument} \"{GlosSIIntegration.GetSettings().SteamShortcutsPath}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
