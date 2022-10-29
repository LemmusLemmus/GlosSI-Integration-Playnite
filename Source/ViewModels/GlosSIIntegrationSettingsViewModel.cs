using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace GlosSIIntegration
{
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
                GlosSIIntegration.Instance.DisplayError(e.Message, e);
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
                    new MessageBoxOption(ResourceProvider.GetString("LOCOKLabel"), true, false),
                    new MessageBoxOption(ResourceProvider.GetString("LOCCancelLabel"), false, true)
                };

                if (playniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_RequriedSettingsIncorrectDesktopError"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), MessageBoxImage.Error, options).Equals(options[0]))
                {
                    plugin.OpenSettingsView();
                }
            }
            else
            {
                playniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOC_GI_RequriedSettingsIncorrectFullscreenError"), 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
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
                    new GlobalProgressOptions(ResourceProvider.GetString("LOC_GI_BackingUpGlosSIFiles"), false)
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
                plugin.DisplayError(string.Format(ResourceProvider.GetString("LOC_GI_FailedBackupGlosSIUnexpectedError"), 
                    e.Message), e);
            }
            
            progressBar.Text = ResourceProvider.GetString("LOC_GI_BackingUpSteamFile");
            BackupShortcutsFile();
            progressBar.CurrentProgressValue++;
        }

        /// <summary>
        /// Backs up the <c>shortcuts.vdf</c> file, if it has not already been backed up.
        /// Displays any potential exception as an error message.
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
                plugin.DisplayError(string.Format(ResourceProvider.GetString("LOC_GI_FailedBackupSteamFileUnexpectedError"), 
                    e.Message), e);
            }
        }

        /// <summary>
        /// Copies files to a directory and updates a progress bar.
        /// The destination directory is created if it does not already exist.
        /// </summary>
        /// <param name="files">The paths to the files that should be copied.</param>
        /// <param name="destDir">The directory to copy the files to.</param>
        /// <param name="progressBar">The progress bar to be updated.</param>
        private static void CopyFilesToDirectory(string[] files, string destDir, GlobalProgressActionArgs progressBar)
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
        /// Attempts to automatically find and set the <c>SteamShortcutsPath</c>.
        /// </summary>
        private void AutoSetSteamShortcutsPath()
        {
            string newSteamShortcutsPath = GetSteamShortcutsPath();

            if (newSteamShortcutsPath != null)
            {
                try
                {
                    // GetFullPath is only used for appearance, by converting any '/' to '\'.
                    Settings.SteamShortcutsPath = Path.GetFullPath(newSteamShortcutsPath);
                    plugin.SavePluginSettings(Settings);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to assign the automatically found Steam shortcuts path:");
                }
            }
        }

        /// <summary>
        /// Attempts to automatically find the <c>SteamShortcutsPath</c>.
        /// </summary>
        /// <returns>If found, the <c>SteamShortcutsPath</c>; <c>null</c> otherwise.</returns>
        private string GetSteamShortcutsPath()
        {
            string userdataPath = GetSteamUserdataPath();

            if (string.IsNullOrEmpty(userdataPath)) return null;
            logger.Trace($"Found the Steam userdata path: \"{userdataPath}\".");

            // Find the path that leads to shortcuts.vdf
            string[] dirs = Directory.GetDirectories(userdataPath);
            List<string> validPaths = new List<string>();

            foreach (string dir in dirs)
            {
                string foundPath = Path.Combine(dir, @"config\shortcuts.vdf");
                if (File.Exists(foundPath)) validPaths.Add(foundPath);
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
        /// Attempts to automatically find the Steam userdata path.
        /// </summary>
        /// <returns>If found, the Steam userdata path; <c>null</c> otherwise.</returns>
        private string GetSteamUserdataPath()
        {
            string path;

            // Check the registry.
            try
            {
                path = (string) Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
                path = Path.Combine(path, "userdata");
                if (Directory.Exists(path)) return path;
            }
            catch (Exception e)
            {
                logger.Warn($"Could not read the Steam userdata path from the registry: {e.Message}");
            }

            // Check two common locations.
            path = Environment.ExpandEnvironmentVariables(@"%programfiles(x86)%\Steam\userdata");
            if (Directory.Exists(path)) return path;

            path = Environment.ExpandEnvironmentVariables(@"%programfiles%\Steam\userdata");
            if (Directory.Exists(path)) return path;

            return null;
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
                null, ResourceProvider.GetString("LOC_GI_MultipleSteamUsersFound"))?.Description;
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

            return string.Format(ResourceProvider.GetString("LOC_GI_MultipleSteamUserCode"), GetIDFromShortcutsPath(shortcutsPath));
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

        public static void OpenLink(string link)
        {
            try
            {
                Process.Start(link);
            }
            catch (Exception ex)
            {
                string message = string.Format(ResourceProvider.GetString("LOC_GI_OpenLinkFailedUnexpectedError"), 
                    link, ex.Message);
                LogManager.GetLogger().Error(ex, message);
                GlosSIIntegration.Api.Dialogs.ShowErrorMessage(message,
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            EditingClone = Serialization.GetClone(Settings);

            if (string.IsNullOrEmpty(Settings.SteamShortcutsPath))
            {
                AutoSetSteamShortcutsPath();
            }
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            Settings = EditingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            plugin.SavePluginSettings(Settings);
            CheckGlosSIVersion();
        }

        /// <summary>
        /// Checks if the GlosSI version is up-to-date (at least for the purposes of this extension).
        /// If not, warns the user.
        /// <para><see cref="GlosSIIntegrationSettings.GlosSIPath"/> must be set before running this method.</para>
        /// </summary>
        private void CheckGlosSIVersion()
        {
            if (Settings.GlosSIVersion == null) // Should not be possible if the GlosSI path has been set.
            {
                logger.Warn("Checking GlosSI version failed: version is null.");
                return;
            }

            if (Settings.GlosSIVersion < new Version("0.0.7.0"))
            {
                logger.Warn(ResourceProvider.GetString("LOC_GI_OldGlosSIVersionWarning") +
                    $"\nInstalled date: {Settings.GlosSIVersion}");
                playniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_OldGlosSIVersionWarning"), 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return VerifySteamShortcutsPath(ref errors) & VerifyGlosSIPath(ref errors) && 
                (!Settings.UseIntegrationFullscreen || !Settings.UsePlayniteOverlay || VerifyPlayniteOverlayName(ref errors)) &
                (!Settings.UseDefaultOverlay || VerifyDefaultOverlayName(ref errors)) &
                (!Settings.CloseGameWhenOverlayIsClosed || VerifyCloseGameWhenOverlayIsClosed(ref errors));
        }

        public RelayCommand<object> BrowseSteamShortcutsFile
        {
            get => new RelayCommand<object>((o) =>
            {
                string filePath = playniteApi.Dialogs.SelectFile($"{ResourceProvider.GetString("LOC_GI_SelectShortcutsVDFFileType")}|shortcuts.vdf");
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

        public RelayCommand<object> AddDefaultOverlay
        {
            get => new RelayCommand<object>((o) =>
            {
                string newShortcutName = OpenShortcutCreationView(null, 
                    Path.Combine(playniteApi.Paths.ConfigurationPath, @"Themes\Desktop\Default\Images\applogo.ico"));
                if (newShortcutName != null) settings.DefaultOverlayName = newShortcutName;
            });
        }

        public RelayCommand<object> AddPlayniteOverlay
        {
            get => new RelayCommand<object>((o) =>
            {
                string newShortcutName = OpenShortcutCreationView("Playnite",
                    Path.Combine(playniteApi.Paths.ConfigurationPath, @"Themes\Fullscreen\Default\Images\applogo.ico"));
                if (newShortcutName != null) settings.PlayniteOverlayName = newShortcutName;
            });
        }

        /// <summary>
        /// Verifes relevant settings before calling <see cref="ShortcutCreationView.ShowDialog(string, string)"/>.
        /// </summary>
        /// <param name="defaultName">The default name of the new overlay.</param>
        /// <param name="defaultIconPath">The default icon of the new overlay.</param>
        /// <returns>The name of the new overlay; <c>null</c> if the action was cancelled.</returns>
        private string OpenShortcutCreationView(string defaultName, string defaultIconPath)
        {
            List<string> errors = new List<string>();

            if (VerifyGlosSITargetsPath() && VerifyGlosSIPath(ref errors) & VerifySteamShortcutsPath(ref errors))
            {
                return ShortcutCreationView.ShowDialog(defaultName, defaultIconPath);
            }
            else
            {
                playniteApi.Dialogs.ShowErrorMessage($"{ResourceProvider.GetString("LOC_GI_RequriedSettingsIncorrectCreateShortcutError")}\n\n{string.Join(Environment.NewLine, errors)}", 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
                return null;
            }
        }

        /// <summary>
        /// Verifies that the option closeGameWhenOverlayIsClosed is usable.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if closeGameWhenOverlayIsClosed is usable; false otherwise.</returns>
        private bool VerifyCloseGameWhenOverlayIsClosed(ref List<string> errors)
        {
            if (Settings.GlosSIVersion < new Version("0.1.1.0"))
            {
                logger.Warn($"GlosSI version {Settings.GlosSIVersion} is too old to be able to close the game when the overlay is closed.");
                errors.Add(string.Format(ResourceProvider.GetString("LOC_GI_GlosSITooOldForOption"), ResourceProvider.GetString("LOC_GI_CloseGameWhenOverlayIsClosedCheckBox")));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies the <c>shortcuts.vdf</c> path. Also expands any environment variables in the path.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the <c>shortcuts.vdf</c> path is valid; false otherwise.</returns>
        private bool VerifySteamShortcutsPath(ref List<string> errors)
        {
            if (string.IsNullOrEmpty(Settings.SteamShortcutsPath))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutsVDFNotSetError"));
                return false;
            }

            try
            {
                Settings.SteamShortcutsPath = Path.GetFullPath(Settings.SteamShortcutsPath);
            }
            catch
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutsVDFNotFoundError"));
                return false;
            }

            if (Path.GetFileName(Settings.SteamShortcutsPath) != "shortcuts.vdf")
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutsVDFWrongNameError"));
                return false;
            }
            
            if (!File.Exists(Settings.SteamShortcutsPath))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutsVDFNotFoundError"));
                return false;
            }

            string lowercasePath = Settings.SteamShortcutsPath.ToLower();

            if (!lowercasePath.Contains(@"steam\userdata") || !lowercasePath.Contains(@"config\shortcuts.vdf"))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutsVDFWrongLocationError"));
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
            if (string.IsNullOrEmpty(Settings.GlosSIPath))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_GlosSIFolderNotSetError"));
                return false;
            }

            try
            {
                Settings.GlosSIPath = Path.GetFullPath(Settings.GlosSIPath);
            }
            catch
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_GlosSIFolderNotFoundError"));
                return false;
            }

            if (!Directory.Exists(Settings.GlosSIPath))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_GlosSIFolderNotFoundError"));
                return false;
            }
            else if (!File.Exists(Path.Combine(Settings.GlosSIPath, "GlosSIConfig.exe")) || 
                !File.Exists(Path.Combine(Settings.GlosSIPath, "GlosSITarget.exe")))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_GlosSIFolderExecutablesNotFoundError"));
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
                playniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOC_GI_GlosSITargetsFolderNotFoundError"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
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
            return VerifyOverlayName(Settings.PlayniteOverlayName, 
                ResourceProvider.GetString("LOC_GI_PlayniteOverlayType"), ref errors);
        }

        /// <summary>
        /// Verifies the default overlay name.
        /// The <c>GlosSITargetsPath</c> and <c>SteamShortcutsPath</c> should be verified before running this method.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the Playnite overlay name is valid; false otherwise.</returns>
        private bool VerifyDefaultOverlayName(ref List<string> errors)
        {
            return VerifyOverlayName(Settings.DefaultOverlayName,
                ResourceProvider.GetString("LOC_GI_DefaultOverlayType"), ref errors);
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
                errors.Add(ResourceProvider.GetString("LOC_GI_OverlayNameNotSetError"));
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
                errors.Add(string.Format(ResourceProvider.GetString("LOC_GI_OverlayGlosSITargetNotFoundError"), overlayType));
                return false;
            }
            catch (Exception e)
            {
                string message = string.Format(ResourceProvider.GetString("LOC_GI_ReadOverlayGlosSITargetUnexpectedError"), 
                    overlayType, e.Message);
                errors.Add(message);
                logger.Error(e, message);
                return false;
            }

            JObject jObject = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);
            string actualName = jObject.GetValue("name")?.ToString();

            if (string.IsNullOrEmpty(actualName))
            {
                errors.Add(string.Format(ResourceProvider.GetString("LOC_GI_OverlayGlosSITargetNoNameError"), overlayType));
                return false;
            }
            else if (actualName != overlayName)
            {
                // If there is a mismatch between the entered name and the name stored in the .json file,
                // use the name in the .json file instead.
                if (overlayType == ResourceProvider.GetString("LOC_GI_PlayniteOverlayType"))
                {
                    Settings.PlayniteOverlayName = actualName;
                }
                else if (overlayType == ResourceProvider.GetString("LOC_GI_DefaultOverlayType"))
                {
                    Settings.DefaultOverlayName = actualName;
                }
            }

            return true;
        }
    }
}