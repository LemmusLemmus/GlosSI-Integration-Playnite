using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlosSIIntegration
{
    public class GlosSIIntegrationSettings : ObservableObject
    {
        private bool integrationEnabled = false;
        private bool closeGameWhenOverlayIsClosed = true;
        private string glosSIPath = null;
        private string glosSITargetsPath = Environment.ExpandEnvironmentVariables("%appdata%/GlosSI/Targets");
        private string steamShortcutsPath = null;

        public bool IntegrationEnabled { get => integrationEnabled; set => SetValue(ref integrationEnabled, value); }
        public bool CloseGameWhenOverlayIsClosed { get => closeGameWhenOverlayIsClosed; set => SetValue(ref closeGameWhenOverlayIsClosed, value); }
        public string GlosSIPath { get => glosSIPath; set => SetValue(ref glosSIPath, value); }
        public string SteamShortcutsPath { get => steamShortcutsPath; set => SetValue(ref steamShortcutsPath, value); }

        [DontSerialize]
        public string GlosSITargetsPath { get => glosSITargetsPath; }
    }

    public class GlosSIIntegrationSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GlosSIIntegration plugin;
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

        public GlosSIIntegrationSettingsViewModel(GlosSIIntegration plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

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
            curPath = Environment.ExpandEnvironmentVariables(curPath);

            // Find the path that leads to shortcuts.vdf
            curPath = null;
            foreach(string dir in Directory.GetDirectories(curPath))
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
            return true;
        }
    }
}