using Playnite.SDK.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;

namespace GlosSIIntegration
{
    /// <summary>
    /// Represents a GlosSI target file.
    /// </summary>
    class GlosSITarget
    {
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
        /// Already integrated games and games tagged for ignoring are ignored.
        /// </summary>
        /// <returns>true if the GlosSITarget was created; false if the game was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        public bool Create()
        {
            if (GlosSIIntegration.GameHasIgnoredTag(playniteGame) || 
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

            // TODO: Send a warning message if there already exists a .json file with the same filename.
            // There is a risk that two different games with different game names have the same filename after illegal characters are removed.
            // There is also a risk that the user already made a GlosSI profile for a game without using this plugin.

            File.WriteAllText(GetJsonFilePath(), jsonString);
        }

        /// <summary>
        /// Gets the path to the icon of the Playnite game.
        /// </summary>
        /// <returns>The absolute path to the icon of the Playnite game, or <c>null</c> if it has no icon.</returns>
        private string GetGameIconPath()
        {
            if (string.IsNullOrEmpty(playniteGame.Icon)) return null;

            return Path.Combine(GlosSIIntegration.Api.Paths.ConfigurationPath, @"library\files\", playniteGame.Icon);
        }

        /// <summary>
        /// Gets the path to the .json with the supplied name.
        /// </summary>
        /// <param name="jsonFileName">The name of the .json file.</param>
        /// <returns>The path to the .json file.</returns>
        public static string GetJsonFilePath(string jsonFileName)
        {
            return Path.Combine(GlosSIIntegration.GetSettings().GlosSITargetsPath, jsonFileName + ".json");
        }

        private string GetJsonFilePath()
        {
            return GetJsonFilePath(jsonFileName);
        }

        /// <summary>
        /// Checks if this GlosSITarget has a corresponding .json file.
        /// The actual name stored inside the .json file is not compared.
        /// </summary>
        /// <returns>true if the target has a corresponding .json file; false otherwise.</returns>
        private bool HasJsonFile()
        {
            return File.Exists(GetJsonFilePath());
        }

        /// <summary>
        /// Checks if there exists a .json file that corresponds to the enterned name 
        /// when illegal file name characters have been removed.
        /// The actual name stored inside the .json file is not compared.
        /// </summary>
        /// <returns>true if the name has a corresponding .json file; false otherwise.</returns>
        public static bool HasJsonFile(string gameName)
        {
            return File.Exists(GetJsonFilePath(RemoveIllegalFileNameChars(gameName)));
        }

        /// <summary>
        /// Removes the integration of a game. 
        /// This removes the game's integrated tag, GlosSITarget and entry in Steam shortcuts.vdf file.
        /// </summary>
        /// <returns>true if the integration was removed; false if it was nonexistent to begin with.</returns>
        public bool Remove()
        {
            if (GlosSIIntegration.GameHasIntegratedTag(playniteGame))
            {
                GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.INTEGRATED_TAG, playniteGame);
                if (HasJsonFile())
                {
                    RemoveFromSteamShortcuts();
                    File.Delete(GetJsonFilePath());
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private void SaveToSteamShortcuts()
        {
            SaveToSteamShortcuts(jsonFileName);
        }

        /// <summary>
        /// Saves a GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <param name="jsonFileName">The file name of the .json target file to be added to Steam.</param>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        public static void SaveToSteamShortcuts(string jsonFileName)
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

        /// <summary>
        /// Runs GlosSIConfig with the provided command line arguments and waits for the process to finish.
        /// The last argument is always the path to the Steam <c>shortcuts.vdf</c> file.
        /// </summary>
        /// <param name="initialArgument">The first argument.</param>
        /// <param name="targetArgument">The second argument, corresponding to a GlosSI target .json file.</param>
        private static void RunGlosSIConfigWithArguments(string initialArgument, string targetArgument)
        {
            Process glosSIConfig = Process.Start(Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSIConfig.exe"), 
                $"{initialArgument} {targetArgument} \"{GlosSIIntegration.GetSettings().SteamShortcutsPath}\"");
            glosSIConfig.WaitForExit();
        }
    }
}
