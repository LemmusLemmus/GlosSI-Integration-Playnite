using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GlosSIIntegration
{
    public class GlosSIIntegrationSettings : ObservableObject
    {
        private bool integrationEnabled = false;
        private bool closeGameWhenOverlayIsClosed = true;
        private string glosSIPath = null;
        private readonly string glosSITargetsPath = Environment.ExpandEnvironmentVariables(@"%appdata%\GlosSI\Targets");
        private string steamShortcutsPath = null;
        private string defaultTargetPath = Path.Combine(GlosSIIntegration.Instance.GetPluginUserDataPath(), "DefaultTarget.json");
        private string playniteOverlayName = null;
        private bool usePlayniteOverlay = false;
        private bool useIntegrationFullscreen = true;
        private bool defaultUseIntegrationDesktop = false;
        private bool useDefaultOverlay = false;
        private string defaultOverlayName = null;

        public bool CloseGameWhenOverlayIsClosed { get => closeGameWhenOverlayIsClosed; set => SetValue(ref closeGameWhenOverlayIsClosed, value); }
        public string GlosSIPath { get => glosSIPath; set => SetValue(ref glosSIPath, value); }
        public string SteamShortcutsPath { get => steamShortcutsPath; set => SetValue(ref steamShortcutsPath, value); }
        public string PlayniteOverlayName { get => playniteOverlayName; set => SetValue(ref playniteOverlayName, value); }
        public bool UsePlayniteOverlay { get => usePlayniteOverlay; set => SetValue(ref usePlayniteOverlay, value); }
        public bool UseIntegrationFullscreen { get => useIntegrationFullscreen; set => SetValue(ref useIntegrationFullscreen, value); }
        public bool DefaultUseIntegrationDesktop { get => defaultUseIntegrationDesktop; set => SetValue(ref defaultUseIntegrationDesktop, value); }
        public bool UseDefaultOverlay { get => useDefaultOverlay; set => SetValue(ref useDefaultOverlay, value); }
        public string DefaultOverlayName { get => defaultOverlayName; set => SetValue(ref defaultOverlayName, value); }

        [DontSerialize]
        public bool IntegrationEnabled { get => integrationEnabled; set => SetValue(ref integrationEnabled, value); }
        [DontSerialize]
        public string GlosSITargetsPath { get => glosSITargetsPath; }
        [DontSerialize]
        public string DefaultTargetPath { get => GetDefaultTargetPath(); set => SetValue(ref defaultTargetPath, value); }
        private string GetDefaultTargetPath()
        {
            // This can potentially fail.
            try
            {
                if (!File.Exists(defaultTargetPath))
                {
                    LogManager.GetLogger().Trace("Creating DefaultTarget file...");
                    File.WriteAllText(defaultTargetPath, Properties.Resources.DefaultTarget);
                    LogManager.GetLogger().Info("DefaultTarget file created.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to access or create the default target file: {e.Message}");
            }

            return defaultTargetPath;
        }
    }

    public class GlosSIIntegrationSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GlosSIIntegration plugin;
        private readonly IPlayniteAPI playniteApi;
        private static readonly ILogger logger = LogManager.GetLogger();
        private GlosSIIntegrationSettings EditingClone { get; set; }

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

            // This will ensure that the the default target file is created initially,
            // so that it may be modified by the user before they add any games.
            try
            {
                _ = Settings.DefaultTargetPath;
            }
            catch (Exception e)
            {
                GlosSIIntegration.Instance.DisplayError("DefaultTargetPathSettings", e.Message, e.ToString());
            }
            

            if (playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                Settings.IntegrationEnabled = Settings.DefaultUseIntegrationDesktop;
            }
            else
            {
                Settings.IntegrationEnabled = Settings.UseIntegrationFullscreen;
            }
        }

        /// <summary>
        /// Checks if the GlosSITargetsPath and settings are OK. If not, a dialog informs the user.
        /// </summary>
        /// <returns>true if the settings were OK; false otherwise.</returns>
        public bool InitialVerification()
        {
            if (!VerifyGlosSITargetsPath()) return false;

            if (VerifySettings(out _))
            {
                InitialBackup();
                return true;
            }

            if (playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption("OK", true, false),
                    new MessageBoxOption("Not now", false, true)
                };

                if (playniteApi.Dialogs.ShowMessage("Requried settings are missing/incorrect. Go to the settings menu?",
                    "GlosSI Integration", System.Windows.MessageBoxImage.Error, options).Equals(options[0]))
                {
                    plugin.OpenSettingsView();
                }
            }
            else
            {
                playniteApi.Dialogs.ShowErrorMessage("Requried settings are missing/incorrect. Please visit the settings menu in desktop mode.", "GlosSI Integration");
            }

            return false;
        }

        /// <summary>
        /// Attempts to back up the important configuration files, if they have not already been backed up. 
        /// Displays a progress bar.
        /// This method assumes that the settings have been verified.
        /// </summary>
        private void InitialBackup()
        {
            string destTargetsDir = Path.Combine(GlosSIIntegration.Instance.GetPluginUserDataPath(), @"Backup\Targets");

            if (!Directory.Exists(destTargetsDir))
            {
                string[] srcTargetFiles = Directory.GetFiles(Settings.GlosSITargetsPath, "*.json", SearchOption.TopDirectoryOnly);

                GlosSIIntegration.Api.Dialogs.ActivateGlobalProgress((progressBar) => BackupFiles(srcTargetFiles, destTargetsDir, progressBar),
                    new GlobalProgressOptions("Backing up GlosSI configuration files...", false)
                    {
                        IsIndeterminate = false
                    });
            }
            else
            {
                BackupShortcutsFile();
            }
        }

        /// <summary>
        /// Backs up the GlosSI targets and the <c>shortcuts.vdf</c> file, if they have not already been backed up.
        /// This method assumes that the settings have been verified.
        /// </summary>
        /// <param name="targetFiles"></param>
        /// <param name="shortcutsFile"></param>
        /// <param name="progressBar"></param>
        private void BackupFiles(string[] targetFiles, string destTargetsDir, GlobalProgressActionArgs progressBar)
        {
            progressBar.ProgressMaxValue = targetFiles.Length + 1;

            try
            {
                CopyFilesToDirectory(targetFiles, destTargetsDir, progressBar);
            }
            catch (Exception e)
            {
                plugin.DisplayError("BackupTargetFiles", $"Failed to backup the GlosSI configuration files: {e.Message}", e.ToString());
            }
            
            progressBar.Text = "Backing up Steam shortcuts file...";
            BackupShortcutsFile();
            progressBar.CurrentProgressValue++;
        }

        /// <summary>
        /// Backs up the <c>shortcuts.vdf</c> file, if it has not already been backed up.
        /// Displays any potential exception as a notification.
        /// This method assumes that the <c>SteamShortcutsPath</c> setting has been verified.
        /// </summary>
        private void BackupShortcutsFile()
        {
            try
            {
                string destShortcutsFile = Path.Combine(GlosSIIntegration.Instance.GetPluginUserDataPath(), 
                    @"Backup", GetIDFromShortcutsPath(Settings.SteamShortcutsPath), @"shortcuts.vdf");

                if (!File.Exists(destShortcutsFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destShortcutsFile));
                    File.Copy(Settings.SteamShortcutsPath, destShortcutsFile);
                    logger.Info($"Shortcuts.vdf file was copied to \"{destShortcutsFile}\"");
                }
            }
            catch (Exception e)
            {
                plugin.DisplayError("BackupShortcutsFile", $"Failed to backup the Shortcuts.vdf file: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Copies files to a directory and updates a progress bar.
        /// The destination directory is created if it does not already exist.
        /// </summary>
        /// <param name="files">The paths to the files that should be copied.</param>
        /// <param name="destDir">The directory to copy the files to.</param>
        /// <param name="progressBar">The progress bar to be updated.</param>
        private void CopyFilesToDirectory(string[] files, string destDir, GlobalProgressActionArgs progressBar)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in files)
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
                progressBar.CurrentProgressValue++;
            }

            logger.Info($"Files were copied to \"{destDir}\"");
        }

        /// <summary>
        /// Attempts to automatically find and set the <c>SteamShortcutsPath</c> if it has not already been set.
        /// </summary>
        private void AutoSetSteamShortcutsPath()
        {
            if (string.IsNullOrEmpty(Settings.SteamShortcutsPath))
            {
                string newSteamShortcutsPath = GetSteamShortcutsPath();
                if (newSteamShortcutsPath != null)
                {
                    Settings.SteamShortcutsPath = newSteamShortcutsPath;
                    plugin.SavePluginSettings(Settings);
                }
            }
        }

        /// <summary>
        /// Attempts to automatically find the <c>SteamShortcutsPath</c>.
        /// </summary>
        /// <returns>If found, the <c>SteamShortcutsPath</c>; <c>null</c> otherwise.</returns>
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

        /// <summary>
        /// Presents the user with a list of found <c>shortcuts.vdf</c> paths corresponding to Steam accounts.
        /// </summary>
        /// <param name="validPaths">The list of <c>shortcuts.vdf</c> paths for the user to choose from.</param>
        /// <returns>The chosen <c>shortcuts.vdf</c> path. Returns <c>null</c> if the dialog was canceled.</returns>
        private string SelectSteamShortcutsPath(List<string> validPaths)
        {
            logger.Trace("Select Steam shortcuts from options menu opened.");

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

            return $"User with Friend Code: \"{GetIDFromShortcutsPath(shortcutsPath)}\"";
        }

        /// <summary>
        /// Gets the Friend Code (i.e. the ID of the folder) corresponding to the <c>shortcuts.vdf</c> path.
        /// </summary>
        /// <param name="shortcutsPath">The <c>shortcuts.vdf</c> file path.</param>
        /// <returns>The Friend Code (i.e. the ID of the folder).</returns>
        private string GetIDFromShortcutsPath(string shortcutsPath)
        {
            return Path.GetFileName(Path.GetFullPath(Path.Combine(shortcutsPath, @"..\..")));
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            EditingClone = Serialization.GetClone(Settings);
            AutoSetSteamShortcutsPath();
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = EditingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return VerifySteamShortcutsPath(ref errors) & VerifyGlosSIPath(ref errors) & 
                (!Settings.UseIntegrationFullscreen || !Settings.UsePlayniteOverlay || VerifyPlayniteOverlayName(ref errors)) &
                (!Settings.UseDefaultOverlay || VerifyDefaultOverlayName(ref errors));
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

        /// <summary>
        /// Verifies the <c>shortcuts.vdf</c> path. Also expands any environment variables in the path.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the <c>shortcuts.vdf</c> path is valid; false otherwise.</returns>
        private bool VerifySteamShortcutsPath(ref List<string> errors)
        {
            string path = Settings.SteamShortcutsPath;

            if (string.IsNullOrEmpty(path))
            {
                errors.Add("The path to shortcuts.vdf has not been set.");
                return false;
            }

            try
            {
                Settings.SteamShortcutsPath = Environment.ExpandEnvironmentVariables(path);
                path = Settings.SteamShortcutsPath;
                Path.GetFullPath(path); // This should throw an exception if the path is incorrectly formatted.
            }
            catch
            {
                errors.Add("The shortcuts.vdf path is incorrectly formatted.");
                return false;
            }

            if (Path.GetFileName(path) != "shortcuts.vdf")
            {
                errors.Add("The shortcuts.vdf path does not lead to a file called \"shortcuts.vdf\".");
                return false;
            }
            else if (!File.Exists(path))
            {
                errors.Add("The shortcuts.vdf file could not be found.");
                return false;
            }
            else if (!path.Contains(@"Steam\userdata") || !path.Contains(@"config\shortcuts.vdf"))
            {
                errors.Add("The shortcuts.vdf file location is incorrect. " +
                    "The file should be located inside the \"config\" folder in the Steam installation folder.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Verifies the GlosSI folder path. Also expands any environment variables in the path.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the GlosSI folder path is valid; false otherwise.</returns>
        private bool VerifyGlosSIPath(ref List<string> errors)
        {
            string path = Settings.GlosSIPath;

            if (string.IsNullOrEmpty(path))
            {
                errors.Add("The path to the GlosSI folder has not been set.");
                return false;
            }

            try
            {
                Settings.GlosSIPath = Environment.ExpandEnvironmentVariables(path);
                path = Settings.GlosSIPath;
                Path.GetFullPath(path); // This should throw an exception if the path is incorrectly formatted.
            }
            catch
            {
                errors.Add("The GlosSI folder path is incorrectly formatted.");
                return false;
            }

            if (!Directory.Exists(path))
            {
                errors.Add("The GlosSI folder location could not be found.");
                return false;
            }
            else if (!File.Exists(Path.Combine(path, "GlosSIConfig.exe")) || !File.Exists(Path.Combine(path, "GlosSITarget.exe")))
            {
                errors.Add("The GlosSI folder location is incorrect: the GlosSI executables could not be found.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Verifies the GlosSI targets path (not set by the user). 
        /// This also verifies that GlosSI has been run at some point.
        /// </summary>
        /// <returns>true if the GlosSI targets path points to an existing directory; false otherwise.</returns>
        public bool VerifyGlosSITargetsPath()
        {
            if (!Directory.Exists(Settings.GlosSITargetsPath))
            {
                logger.Error("The GlosSI Targets folder does not exist.");
                playniteApi.Dialogs.ShowErrorMessage("The GlosSI Targets folder could not be found. " +
                    "GlosSI must be installed and have run at least once before the GlosSI Integration extension can be used.",
                    "GlosSI Integration");
                return false;
            }

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
            return VerifyOverlayName(Settings.PlayniteOverlayName, "Playnite", ref errors);
        }

        /// <summary>
        /// Verifies the default overlay name.
        /// The <c>GlosSITargetsPath</c> and <c>SteamShortcutsPath</c> should be verified before running this method.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the Playnite overlay name is valid; false otherwise.</returns>
        private bool VerifyDefaultOverlayName(ref List<string> errors)
        {
            return VerifyOverlayName(Settings.DefaultOverlayName, "default", ref errors);
        }

        /// <summary>
        /// Verifies an overlay name.
        /// The <c>GlosSITargetsPath</c> and <c>SteamShortcutsPath</c> should be verified before running this method.
        /// </summary>
        /// <param name="overlayName">The name of the overlay.</param>
        /// <param name="overlayType">The type of the overlay. This is used for error messages and logging.</param>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the overlay name is valid; false otherwise.</returns>
        private bool VerifyOverlayName(string overlayName, string overlayType, ref List<string> errors)
        {
            if (string.IsNullOrEmpty(overlayName))
            {
                errors.Add($"The name of the {overlayType} overlay has not been set.");
                return false;
            }

            string fileName = GlosSITarget.RemoveIllegalFileNameChars(overlayName);
            string jsonString;

            try
            {
                jsonString = File.ReadAllText(GlosSITarget.GetJsonFilePath(fileName));
            }
            catch (FileNotFoundException) // Verify that the corresponding .json file actually exists
            {
                errors.Add($"The target file referenced by the {overlayType} overlay name could not be found.");
                return false;
            }
            catch (Exception e)
            {
                errors.Add($"Something went wrong when attempting to read the target file referenced by the {overlayType} overlay name: {e.Message}");
                logger.Error($"Something went wrong when attempting to read the target file referenced by the {overlayType} overlay name: {e}");
                return false;
            }

            JObject jObject = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);
            string actualName = jObject.GetValue("name")?.ToString();

            if (string.IsNullOrEmpty(actualName))
            {
                errors.Add($"Something is wrong with the file referenced by the {overlayType} overlay name. " +
                    $"The name property in the target .json file in %appdata%\\GlosSI\\Targets could not be found or has not been set.");
                return false;
            }
            else if (actualName != overlayName)
            {
                // If there is a mismatch between the entered name and the name stored in the .json file,
                // use the name in the .json file instead.
                if (overlayType == "Playnite")
                {
                    Settings.PlayniteOverlayName = actualName;
                }
                else if (overlayType == "default")
                {
                    Settings.DefaultOverlayName = actualName;
                }
            }

            // Verify that the shortcut has actually been added to Steam (i.e. the shortcuts.vdf file)
            try
            {
                if (!ShortcutsContainsTarget(fileName))
                {
                    playniteApi.Dialogs.ShowMessage($"The GlosSI target referenced by the {overlayType} overlay has not been added to Steam. Press OK to automatically add it. " +
                        "Steam has to be restarted afterwards for the changes to take effect.", "GlosSI Integration");
                    logger.Trace($"Adding the {overlayType} overlay \"{actualName}\" to Steam...");

                    try
                    {
                        GlosSITarget.SaveToSteamShortcuts(fileName);
                    }
                    catch (Exception e)
                    {
                        errors.Add($"The {overlayType} overlay could not be added automatically to Steam: {e.Message}");
                        logger.Error($"The {overlayType} overlay could not be added automatically to Steam: {e}");
                        return false;
                    }

                    if (!ShortcutsContainsTarget(fileName))
                    {
                        errors.Add($"The {overlayType} overlay could not be added automatically to Steam: shortcuts.vdf file was not successfully updated.");
                        return false;
                    }

                    logger.Info($"The {overlayType} overlay was added to Steam as a shortcut.");
                }
            }
            catch (Exception e)
            {
                errors.Add($"Something went wrong when trying to read the shortcuts.vdf file: {e.Message}");
                logger.Error($"Something went wrong when trying to read the shortcuts.vdf file: {e}");
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