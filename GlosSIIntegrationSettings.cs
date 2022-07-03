using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
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
            "DefaultTarget.json");
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

            if(Settings.SteamShortcutsPath == null)
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
            string curPath = "%programfiles(x86)%\\Steam\\userdata";
            if (!Directory.Exists(curPath))
            {
                curPath = "%programfiles%\\Steam\\userdata";
                if (!Directory.Exists(curPath))
                {
                    // The user has to manually input a path.
                    return null;
                }
            }

            // Find the path that leads to shortcuts.vdf
            string[] dirs = Directory.GetDirectories(curPath);
            curPath = null;
            foreach(string dir in dirs)
            {
                string newPath = Path.Combine(dir, "\\config\\shortcuts.vdf");
                if(File.Exists(newPath))
                {
                    if(curPath != null)
                    {
                        // TODO: Let the user choose which directory is the correct one?
                        return null;
                    }
                    curPath = newPath;
                }
            }

            return curPath;
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

        private bool VerifyPlayniteOverlayName(ref List<string> errors)
        {
            string targetName = Settings.PlayniteOverlayName;
            string fileName = GlosSITarget.RemoveIllegalFileNameChars(targetName);

            if (string.IsNullOrEmpty(targetName))
            {
                errors.Add("The name of the Playnite overlay has not been set.");
                return false;
            }

            // Verify that the corresponding .json file actually exists:
            if (!File.Exists(GlosSITarget.GetJsonFilePath(fileName)))
            {
                errors.Add("The target file referenced by the Playnite overlay name could not be found.");
                return false;
            }

            // If there is a mismatch between the entered name and the name stored in the .json file,
            // use the name in the .json file instead.
            string jsonString = File.ReadAllText(GlosSITarget.GetJsonFilePath(fileName));
            JObject jObject = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);
            string actualName = jObject.GetValue("name")?.ToString();
            if(actualName != targetName)
            {
                Settings.PlayniteOverlayName = actualName;
            }

            // TODO: Verify that the shortcut has actually been added to Steam (i.e. the shortcuts.vdf file)?

            return true;
        }
    }
}