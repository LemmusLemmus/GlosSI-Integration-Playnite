using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GlosSIIntegration
{
    public class GlosSIIntegrationSettings : ObservableObject
    {
        private bool integrationEnabled = false;
        private bool closeGameWhenOverlayIsClosed = true;
        private string glosSIPath = null;
        private string glosSITargetsPath = Environment.ExpandEnvironmentVariables("%appdata%/GlosSI/Targets");
        private string steamShortcutsPath = null;
        private string defaultTargetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "DefaultTarget.json"); // TODO: Use ExtensionsData folder instead.
        private string playniteOverlayName = null;
        private bool usePlayniteOverlay = false;

        public bool IntegrationEnabled { get => integrationEnabled; set => SetValue(ref integrationEnabled, value); }
        public bool CloseGameWhenOverlayIsClosed { get => closeGameWhenOverlayIsClosed; set => SetValue(ref closeGameWhenOverlayIsClosed, value); }
        public string GlosSIPath { get => glosSIPath; set => SetValue(ref glosSIPath, value); }
        public string SteamShortcutsPath { get => steamShortcutsPath; set => SetValue(ref steamShortcutsPath, value); }
        public string PlayniteOverlayName { get => playniteOverlayName; set => SetValue(ref playniteOverlayName, value); }
        public bool UsePlayniteOverlay { get => usePlayniteOverlay; set => SetValue(ref usePlayniteOverlay, value); }

        [DontSerialize]
        public string GlosSITargetsPath { get => glosSITargetsPath; }
        [DontSerialize]
        public string DefaultTargetPath { get => defaultTargetPath; }
    }

    public class GlosSIIntegrationSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GlosSIIntegration plugin;
        private readonly IPlayniteAPI playniteApi;
        private GlosSIIntegrationSettings editingClone { get; set; }

        private GlosSIIntegrationSettings settings;
        public GlosSIIntegrationSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public GlosSIIntegrationSettingsViewModel(GlosSIIntegration plugin, IPlayniteAPI playniteApi)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;
            this.playniteApi = playniteApi;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<GlosSIIntegrationSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new GlosSIIntegrationSettings();
            }

            if(string.IsNullOrEmpty(Settings.SteamShortcutsPath))
            {
                string newSteamShortcutsPath = GetSteamShortcutsPath();
                if(newSteamShortcutsPath != null)
                {
                    Settings.SteamShortcutsPath = newSteamShortcutsPath;
                    plugin.SavePluginSettings(Settings);
                }
            }
        }

        private string GetSteamShortcutsPath()
        {
            // Get the Steam userdata folder.
            string curPath = Environment.ExpandEnvironmentVariables(@"%programfiles(x86)%\Steam\userdata");
            if (!Directory.Exists(curPath))
            {
                curPath = Environment.ExpandEnvironmentVariables(@"%programfiles%\Steam\userdata");
                if (!Directory.Exists(curPath))
                {
                    // TODO: Check all running processes, and if one of them is Steam use the directory associated with it.
                    return null;
                }
            }

            // Find the path that leads to shortcuts.vdf
            string[] dirs = Directory.GetDirectories(curPath);
            List<string> validPaths = new List<string>();

            foreach (string dir in dirs)
            {
                string newPath = Path.Combine(dir, @"config\shortcuts.vdf");
                if (File.Exists(newPath)) validPaths.Add(newPath);
            }
            if (validPaths.Count == 0)
            {
                return null;
            }
            else if (validPaths.Count == 1)
            {
                return validPaths[0];
            }
            else
            {
                return SelectSteamShortcutsPath(validPaths);
            }
        }

        private string SelectSteamShortcutsPath(List<string> validPaths)
        {
            List<GenericItemOption> items = new List<GenericItemOption>();

            foreach (string path in validPaths)
            {
                items.Add(new GenericItemOption(GetSteamPersona(path), path));
            }
            return playniteApi.Dialogs.ChooseItemWithSearch(items,
                (str) => string.IsNullOrWhiteSpace(str) ? items : items.Where(item => item.Name.Contains(str)).ToList(),
                null,
                "Which of these Steam accounts should the GlosSI integration use? The plugin needs the path to shortcuts.vdf.")
                ?.Description;
        }

        /// <summary>
        /// Tries to find the persona/username corresponding to a shortcuts.vdf path.
        /// </summary>
        /// <param name="shortcutsPath">The path to shortcuts.vdf</param>
        /// <returns>The persona as a string. 
        /// If no persona was found, a descriptive text with the Friend Code (i.e. the ID of the folder) is returned.</returns>
        private string GetSteamPersona(string shortcutsPath)
        {
            try
            {
                string fileText = File.ReadAllText(Path.GetFullPath(Path.Combine(shortcutsPath, "..", "localconfig.vdf")));
                int startIndex = fileText.IndexOf("\"PersonaName\"");

                if (startIndex != -1)
                {
                    startIndex = fileText.IndexOf('\"', startIndex + "\"PersonaName\"".Length) + 1;
                    int endIndex = fileText.IndexOf('\n', startIndex, 40) - 1;
                    if (endIndex != -1)
                    {
                        return fileText.Substring(startIndex, endIndex - startIndex);
                    }
                }
            } 
            catch { }

            return $"User with Friend Code: \"{Path.GetFileName(Path.GetFullPath(Path.Combine(shortcutsPath, @"..\..")))}\"";
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();

            if(Settings.UsePlayniteOverlay && !VerifyPlayniteOverlayName(ref errors))
            {
                return false;
            }

            return true;
        }

        public RelayCommand<object> BrowseSteamShortcutsFile
        {
            get => new RelayCommand<object>((o) =>
            {
                string filePath = playniteApi.Dialogs.SelectFile("Steam Shortcuts file|shortcuts.vdf");
                if (!string.IsNullOrEmpty(filePath)) settings.SteamShortcutsPath = filePath;
            });
        }

        public RelayCommand<object> BrowseGlosSIFolder
        {
            get => new RelayCommand<object>((o) =>
            {
                string filePath = playniteApi.Dialogs.SelectFolder();
                if (!string.IsNullOrEmpty(filePath)) settings.GlosSIPath = filePath;
            });
        }

        private bool VerifySteamShortcutsPath(string path)
        {
            // TODO: Check that path contains steam/userdata and config/shortcuts.vdf.
            return true;
        }

        private bool VerifyGlosSIPath(string path)
        {
            // TODO: Check that the folder contains the two executables.
            return true;
        }

        /// <summary>
        /// Verifies the Playnite overlay name.
        /// The <c>GlosSITargetsPath</c> and <c>SteamShortcutsPath</c> should be verified before running this method.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the Playnite overlay name is valid; false otherwise.</returns>
        private bool VerifyPlayniteOverlayName(ref List<string> errors)
        {
            string targetName = Settings.PlayniteOverlayName;
            string fileName = GlosSITarget.RemoveIllegalFileNameChars(targetName);

            if (string.IsNullOrEmpty(targetName))
            {
                errors.Add("The name of the Playnite overlay has not been set.");
                return false;
            }

            string jsonString;
            try
            {
                jsonString = File.ReadAllText(GlosSITarget.GetJsonFilePath(fileName));
            }
            catch (FileNotFoundException) // Verify that the corresponding .json file actually exists
            {
                errors.Add("The target file referenced by the Playnite overlay name could not be found.");
                return false;
            }
            catch (Exception e)
            {
                errors.Add($"Something went wrong when attempting to read the target file referenced by the Playnite overlay name: {e}");
                return false;
            }
            
            JObject jObject = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);
            string actualName = jObject.GetValue("name")?.ToString();

            if(string.IsNullOrEmpty(actualName))
            {
                errors.Add($"Something is wrong with the file referenced by the Playnite overlay name. " +
                    $"The name property in the target .json file in %appdata%/GlosSI/Targets could not be found or has not been set.");
                return false;
            }
            else if (actualName != targetName)
            {
                // If there is a mismatch between the entered name and the name stored in the .json file,
                // use the name in the .json file instead.
                Settings.PlayniteOverlayName = actualName;
            }

            // Verify that the shortcut has actually been added to Steam (i.e. the shortcuts.vdf file)
            try
            {
                if (!ShortcutsContainsTarget(fileName))
                {

                    playniteApi.Dialogs.ShowMessage("The GlosSI target referenced by the Playnite overlay has not been added to Steam. Press OK to automatically add it. " +
                        "Steam has to be restarted afterwards for the changes to take effect.", "GlosSI Integration");
                    try
                    {
                        GlosSITarget.SaveToSteamShortcuts(fileName);
                    }
                    catch (Exception e)
                    {
                        errors.Add($"The Playnite overlay could not be added automatically to Steam: {e}");
                        return false;
                    }

                    if (!ShortcutsContainsTarget(fileName))
                    {
                        errors.Add($"The Playnite overlay could not be added automatically to Steam: shortcuts.vdf file was not successfully updated.");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                errors.Add($"Something went wrong when trying to read the shortcuts.vdf file: {e}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the shortcuts.vdf file is <i>likely</i> to contain the target.
        /// </summary>
        /// <param name="fileName">The filename of the .json file, excluding the extension.</param>
        /// <returns>true if the shortcut likely contains the target; false if it definitely does not.</returns>
        private bool ShortcutsContainsTarget(string fileName)
        {
            return File.ReadAllText(Settings.SteamShortcutsPath).Contains($"{fileName}.json");
        }
    }
}