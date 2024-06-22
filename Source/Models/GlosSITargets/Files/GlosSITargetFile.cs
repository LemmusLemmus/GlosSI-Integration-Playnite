using System.IO;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using Playnite.SDK;
using System.Linq;
using System.Collections.Generic;
using GlosSIIntegration.Models.GlosSITargets.Types;

namespace GlosSIIntegration.Models.GlosSITargets.Files
{
    /// <summary>
    /// Represents a GlosSI target file.
    /// </summary>
    internal class GlosSITargetFile : GlosSITargetFileInfo
    {
        private readonly GlosSITarget target;

        // TODO: Increase version requirement and remove everything related to this exception.
        public class UnsupportedCharacterException : Exception { }
        public class UnexpectedGlosSIBehaviourException : Exception { }
        public class TargetNameMissingException : Exception
        {
            public TargetNameMissingException() : base("The name of the target is missing.") { }
        }
        public class TargetNameMismatchException : Exception
        {
            public string ActualName { get; }
            public TargetNameMismatchException(string actualName)
            {
                ActualName = actualName;
            }
        }

        public GlosSITargetFile(GlosSITarget target) : base(target.Name)
        {
            this.target = target;
        }

        /// <summary>
        /// Verfies that the game name and icon path only contains supported characters 
        /// for performing operations with GlosSI.
        /// </summary>
        /// <exception cref="UnsupportedCharacterException">If the name of the game or 
        /// its full icon path contains unsupported characters.</exception>
        private void VerifyTargetCharacters(string iconPath)
        {
            // Non-ASCII characters are not supported if the GlosSI version is <= 0.0.7.0.
            if ((IsNotAscii(Name) || IsNotAscii(iconPath)) &&
                (GlosSIIntegration.GetSettings().GlosSIVersion == null ||
                GlosSIIntegration.GetSettings().GlosSIVersion <= new Version("0.0.7.0")))
            {
                LogManager.GetLogger().Warn($"Game \"{target.Name}\" skipped due to non-ASCII characters. GlosSI version: " +
                    GlosSIIntegration.GetSettings().GlosSIVersion);
                throw new UnsupportedCharacterException();
            }
        }

        /// <summary>
        /// Checks if a string is not entirely ASCII.
        /// </summary>
        /// <param name="str">The string to be checked.</param>
        /// <returns>true if the string contains a non-ASCII character; false otherwise.</returns>
        private static bool IsNotAscii(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;

            return str.Any(c => c > 127);
        }

        /// <summary>
        /// Creates a GlosSITarget and Steam shortcut for a game, using the default .json structure.
        /// </summary>
        /// <param name="iconPath">A path to the icon of the shortcut. The path can be <c>null</c> for no icon.</param>
        /// <returns>true if the GlosSITarget was created; false if creation was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        /// <exception cref="UnsupportedCharacterException"><see cref="VerifyTargetCharacters"/></exception>
        public virtual bool Create(string iconPath)
        {
            SaveAsJsonTarget(iconPath);
            SaveToSteamShortcuts();
            return true;
        }

        public virtual bool Create()
        {
            return Create(null);
        }

        /// <summary>
        /// Overwrites the contents of the target file, without modifiying the Steam shortcut.
        /// Does not change the icon path, since if that is to be changed the Steam shortcut also has to be updated.
        /// The file to be overwritten must already exist.
        /// </summary>
        public virtual void Overwrite()
        {
            GlosSITargetSettings settings = GlosSITargetSettings.ReadFrom(FullPath);
            SaveAsJsonTarget(settings.Icon);
        }

        private void SaveAsJsonTarget(string iconPath)
        {
            VerifyTargetCharacters(iconPath);

            GlosSITargetSettings settings = GlosSITargetSettings.ReadFrom(
                GlosSIIntegration.GetSettings().DefaultTargetPath);

            settings.Name = target.Name;
            settings.Icon = iconPath;
            settings.Launch = target.GetPreferredLaunchOptions();

            // TODO: Send a warning message if there already exists a .json file with the same filename. Or even better, permit name conflicts.
            // There is a risk that two different games with different game names have the same filename after illegal characters are removed.
            // There is also a risk that the user already made a GlosSI profile for a game without using this plugin.
            // And there is a risk that two different games have exactly the same name,
            // or that the user may already have a GlosSI shortcut with the same name.
            //
            // In theory, name conflicts can be resolved by
            // 1) Ensuring that target file names are unique.
            // Either by for example namining them based on the database id of the game,
            // or by placing them in different directories when conflicts arise.
            // GlosSI has to support it however.
            // 2) Ensuring that Steam IDs are unique.
            // When calculating Steam IDs, only the name of the shortcut and the path of the shortcut makes a difference
            // (command line arguments are ignored).
            // The name should be up to the user.
            // Although the paths must point to the same GlosSITarget.exe, the string path itself could possibly differ.
            // For example, by writing "dir\..\dir" as many times as necessary for a unique path.
            // One could alternatively simply make use of different letter case,
            // although that assumes that letter case makes no difference in file paths (which is a very reasonable assumption to make on Windows).
            settings.WriteTo(FullPath);
        }

        /// <summary>
        /// Removes the integration of a game. 
        /// This removes the game's integrated tag, GlosSITarget and entry in Steam shortcuts.vdf file.
        /// </summary>
        /// <returns>true if the integration was removed; false if it was nonexistent to begin with.</returns>
        /// <exception cref="UnsupportedCharacterException"><see cref="VerifyTargetCharacters"/></exception>
        public virtual bool Remove()
        {
            if (File.Exists(FullPath))
            {
                VerifyTargetCharacters(null);
                RemoveFromSteamShortcuts();
                File.Delete(FullPath);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Validates the contents of the file to ensure that the configured settings work with the extension.
        /// If necessary, updates the required settings.
        /// </summary>
        /// <exception cref="FileNotFoundException">If the file could not be found.</exception>
        /// <exception cref="TargetNameMissingException">If the name of the shortcut in the file is missing or empty.</exception>
        /// <exception cref="TargetNameMismatchException">If the name of the shortcut in the file does not correspond 
        /// to the name of this <see cref="GlosSITargetFile"/> instance. This condition is check last: 
        /// if this exception is thrown everything else passed.</exception>
        public virtual void Validate()
        {
            if (!File.Exists(FullPath)) throw new FileNotFoundException();

            GlosSITargetSettings targetSettings;

            targetSettings = GlosSITargetSettings.ReadFrom(FullPath);

            string actualName = targetSettings.Name;

            if (string.IsNullOrEmpty(actualName))
            {
                throw new TargetNameMissingException();
            }

            GlosSITargetSettings.LaunchOptions targetLaunchOptions = target.GetPreferredLaunchOptions();

            // Note: This will currently also reset the launch property. Should not be a problem though.
            if (!targetLaunchOptions.IsEveryPropertyEqual(targetSettings.Launch))
            {
                LogManager.GetLogger().Debug($"\"{target.Name}\" target launch options updated from:\n" +
                    $"{targetSettings.Launch}\nto:\n{targetLaunchOptions}");
                targetSettings.Launch = targetLaunchOptions;
                targetSettings.WriteTo();
            }

            if (actualName != target.Name)
            {
                throw new TargetNameMismatchException(actualName);
            }
        }

        /// <summary>
        /// Saves the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        /// <exception cref="Exception">If starting GlosSIConfig failed.</exception>
        private void SaveToSteamShortcuts()
        {
            RunGlosSIConfigWithArguments("add", Name);
        }

        /// <summary>
        /// Removes the GlosSITarget profile to Steam. 
        /// A restart of Steam is required for these changes to take effect.
        /// </summary>
        private void RemoveFromSteamShortcuts()
        {
            // TODO: There is a risk that the user changes the name of the game.
            // The name should therefore be taken from the json file instead.
            // There will have to be a way to identify which json file belongs to which game though.

            // When removing, GlosSI takes the game name with all characters, including illegal file name characters.
            RunGlosSIConfigWithArguments("remove", target.Name);
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
            targetArgument = GetCommandLineArgumentSafeString(targetArgument);
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
        }

        // TODO: Sometimes GlosSIConfig breaks the shortcuts.vdf file. Try to figure out why the problem occurs in the first place.
        // It seems to happen when the shortcuts.vdf file becomes to big.
    }
}
