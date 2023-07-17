using Playnite.SDK.Models;
using System.IO;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using Playnite.SDK;
using System.Linq;
using System.Collections.Generic;

namespace GlosSIIntegration.Models
{
    /// <summary>
    /// Represents a GlosSI target file.
    /// </summary>
    class GlosSITargetFile
    {
        private readonly Game game;
        private readonly bool isPlayniteGame;
        /// <summary>
        /// The filename of the .json GlosSITarget profile, without the extension.
        /// </summary>
        private readonly string jsonFileName;

        public class UnsupportedCharacterException : Exception { }
        public class UnexpectedGlosSIBehaviour : Exception { }

        /// <summary>
        /// Creates a <c>GlosSITargetFile</c> object from a Playnite <see cref="Game"/>.
        /// </summary>
        /// <param name="playniteGame">The Playnite game.</param>
        /// <exception cref="ArgumentException">If the name of the game is null.</exception>
        public GlosSITargetFile(Game playniteGame)
        {
            game = playniteGame;
            if (game.Name == null) throw new ArgumentException("The name of the game is null.");
            jsonFileName = RemoveIllegalFileNameChars(playniteGame.Name);
            isPlayniteGame = true;
        }

        /// <summary>
        /// Creates a <c>GlosSITargetFile</c> object from a name and a path to an icon.
        /// </summary>
        /// <param name="name">The name of the shortcut.</param>
        /// <param name="iconPath">A path to the icon of the shortcut. The path can be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">If the name is null.</exception>
        public GlosSITargetFile(string name, string iconPath)
        {
            if (name == null) throw new ArgumentNullException("name");

            game = new Game()
            {
                Name = name,
                Icon = iconPath
            };
            jsonFileName = RemoveIllegalFileNameChars(name);
            isPlayniteGame = false;
        }

        /// <summary>
        /// Verfies that the game name and icon path only contains supported characters 
        /// for performing operations with GlosSI.
        /// </summary>
        /// <exception cref="UnsupportedCharacterException">If the name of the game or 
        /// its full icon path contains unsupported characters.</exception>
        private void VerifyGameCharacters()
        {
            // Non-ASCII characters are not supported if the GlosSI version is <= 0.0.7.0.
            if ((IsNotAscii(game.Name) || IsNotAscii(GetGameIconPath())) &&
                (GlosSIIntegration.GetSettings().GlosSIVersion == null ||
                GlosSIIntegration.GetSettings().GlosSIVersion <= new Version("0.0.7.0")))
            {
                LogManager.GetLogger().Warn($"Game \"{game.Name}\" skipped due to non-ASCII characters. GlosSI version: " +
                    GlosSIIntegration.GetSettings().GlosSIVersion);
                throw new UnsupportedCharacterException();
            }
        }

        /// <summary>
        /// Checks if a string is not entirely ASCII.
        /// </summary>
        /// <param name="str">The string to be checked.</param>
        /// <returns>true if the string contains a non-ASCII character; false otherwise.</returns>
        private bool IsNotAscii(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;

            return str.Any(c => c > 127);
        }

        public static string RemoveIllegalFileNameChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        /// <summary>
        /// Creates a GlosSITarget and Steam shortcut for a game, using the default .json structure.
        /// Already integrated games and games tagged for ignoring are ignored.
        /// </summary>
        /// <returns>true if the GlosSITarget was created; false if the game was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        /// <exception cref="UnsupportedCharacterException"><see cref="VerifyGameCharacters"/></exception>
        public bool Create()
        {
            if (GlosSIIntegration.GameHasIgnoredTag(game) ||
                GlosSIIntegration.GameHasIntegratedTag(game)) return false;

            VerifyGameCharacters();
            SaveAsJsonTarget();
            SaveToSteamShortcuts();
            if (isPlayniteGame) GlosSIIntegration.AddTagToGame(GlosSIIntegration.LOC_INTEGRATED_TAG, game);
            return true;
        }

        private void SaveAsJsonTarget()
        {
            GlosSITargetSettings settings = GlosSITargetSettings.ReadFrom(
                GlosSIIntegration.GetSettings().DefaultTargetPath);

            settings.Name = game.Name;
            settings.Icon = GetGameIconPath();
            settings.Launch = new GlosSITargetSettings.LaunchOptions();

            // TODO: Send a warning message if there already exists a .json file with the same filename.
            // There is a risk that two different games with different game names have the same filename after illegal characters are removed.
            // There is also a risk that the user already made a GlosSI profile for a game without using this plugin.
            settings.WriteTo(GetJsonFilePath());
        }

        /// <summary>
        /// Gets the path to the icon of the game.
        /// </summary>
        /// <returns>The absolute path to the icon of the game, or <c>null</c> if it has no icon.</returns>
        private string GetGameIconPath()
        {
            if (string.IsNullOrEmpty(game.Icon)) return null;

            if (isPlayniteGame)
            {
                return Path.Combine(GlosSIIntegration.Api.Paths.ConfigurationPath, @"library\files\", game.Icon);
            }
            else
            {
                return game.Icon;
            }
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
        /// Checks if this object has a corresponding .json file.
        /// The actual name stored inside the .json file is not compared.
        /// </summary>
        /// <returns>true if the target has a corresponding .json file; false otherwise.</returns>
        private bool HasJsonFile()
        {
            return File.Exists(GetJsonFilePath());
        }

        /// <summary>
        /// Checks if there exists a .json file that corresponds to the entered name 
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
        /// <exception cref="UnsupportedCharacterException"><see cref="VerifyGameCharacters"/></exception>
        public bool Remove()
        {
            if (!isPlayniteGame || GlosSIIntegration.GameHasIntegratedTag(game))
            {
                VerifyGameCharacters();

                if (isPlayniteGame)
                {
                    GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.LOC_INTEGRATED_TAG, game);
                    GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.SRC_INTEGRATED_TAG, game);
                }
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
            RunGlosSIConfigWithArguments("remove", GetCommandLineArgumentSafeString(game.Name));
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
            string initialContents = File.ReadAllText(GlosSIIntegration.GetSettings().SteamShortcutsPath);
            string arguments = $"{initialArgument} {targetArgument} \"{GlosSIIntegration.GetSettings().SteamShortcutsPath}\"";

            using (Process glosSIConfig = Process.Start(Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSIConfig.exe"), arguments))
            {
                glosSIConfig.WaitForExit();

                try
                {
                    if (glosSIConfig.ExitCode != 0)
                    {
                        LogManager.GetLogger().Error($"GlosSIConfig returned exit code {glosSIConfig.ExitCode}, " +
                            $"using arguments {initialArgument} and {targetArgument}.");
                        return;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    LogManager.GetLogger().Error(ex, "Failed to check GlosSIConfig exit code.");
                }
            }

            VerifyShortcutModification(initialContents, arguments);
        }

        /// <summary>
        /// Verifes that GlosSIConfig doesn't modify more than one shortcut in shortcuts.vdf.
        /// The content of the file is reverted to <paramref name="initialContents"/> if the verification fails.
        /// </summary>
        /// <param name="initialContents">The initial shortcuts.vdf contents before the modification.</param>
        /// <param name="arguments">The arguments used to modifiy shortcuts.vdf with GlosSIConfig. 
        /// Only used for logging.</param>
        /// <exception cref="UnexpectedGlosSIBehaviour">If the verification failed.</exception>
        private static void VerifyShortcutModification(string initialContents, string arguments)
        {
            string newContents = File.ReadAllText(GlosSIIntegration.GetSettings().SteamShortcutsPath);

            int shortcutCountDiff = Math.Abs(GetShortcutCount(initialContents) - GetShortcutCount(newContents));

            if (shortcutCountDiff > 1)
            {
                LogManager.GetLogger().Error("More than one shortcut was changed unintentionally by GlosSIConfig.\n" +
                    $"Arguments provided: {arguments}\n" +
                    $"Old shortcuts.vdf:\n{initialContents}\nNew shortcuts.vdf:\n{newContents}");
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(ResourceProvider.GetString("LOCOKLabel"), true, false),
                    new MessageBoxOption(ResourceProvider.GetString("LOCCancelLabel"), false, true)
                };
                if (GlosSIIntegration.Api.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_MultipleChangedShortcutsUnexpectedError"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), System.Windows.MessageBoxImage.Error, options).Equals(options[0]))
                {
                    GlosSIIntegrationSettingsViewModel.OpenLink("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/issues");
                }

                // Revert shortcuts.vdf file.
                File.WriteAllText(GlosSIIntegration.GetSettings().SteamShortcutsPath, initialContents);

                throw new UnexpectedGlosSIBehaviour();
            }
            else if (shortcutCountDiff == 0)
            {
                LogManager.GetLogger().Warn("No shortcuts were changed by GlosSIConfig. " +
                    $"Old shortcuts.vdf:\n{initialContents}\nNew shortcuts.vdf:\n{newContents}");
            }
        }

        private static int GetShortcutCount(string shortcutsVdfContents)
        {
            return Regex.Matches(shortcutsVdfContents, "\0exe").Count;
        }
    }
}
