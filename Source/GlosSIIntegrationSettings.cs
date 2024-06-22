using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GlosSIIntegration
{
    public class GlosSIIntegrationSettings : ObservableObject
    {
        // TODO: Consider using System.Uri instead of string.

        private bool closeGameWhenOverlayIsClosed = true;
        private string glosSIPath = null;
        private readonly string glosSITargetsPath = Environment.ExpandEnvironmentVariables(@"%appdata%\GlosSI\Targets");
        private string steamShortcutsPath = null;
        private readonly string defaultTargetPath = Path.Combine(GlosSIIntegration.Instance.GetPluginUserDataPath(), "DefaultTarget.json");
        private readonly string knownTargetsPath = Path.Combine(GlosSIIntegration.Instance.GetPluginUserDataPath(), "Targets.json");
        private readonly string startPlayniteFromGlosSIScriptPath = Path.Combine(Path.GetDirectoryName(typeof(GlosSIIntegrationSettings).Assembly.Location),
            @"Scripts\StartPlayniteFromGlosSI.vbs");
        private string playniteOverlayName = null;
        private bool usePlayniteOverlay = false;
        private bool useIntegrationFullscreen = true;
        private bool defaultUseIntegrationDesktop = true;
        private bool useDefaultOverlay = false;
        private string defaultOverlayName = null;
        private Version glosSIVersion = null;

        public bool CloseGameWhenOverlayIsClosed { get => closeGameWhenOverlayIsClosed; set => SetValue(ref closeGameWhenOverlayIsClosed, value); }
        // TODO: Rename to GlosSIFolderPath or GlosSIDirectory?
        /// <summary>
        /// The path to the GlosSI folder containing <c>GlosSIConfig.exe</c> and <c>GlosSITarget.exe</c>.
        /// Setting GlosSIPath also updates the current GlosSIVersion.
        /// </summary>
        public string GlosSIPath { get => glosSIPath; set { if (value != glosSIPath) { glosSIVersion = null; } SetValue(ref glosSIPath, value); } }
        // TODO: Instead of relying on the path to shortcuts.vdf, only save the Steam user ID and use it
        // (as well as the Steam path registry value) to calculate all needed Steam paths.
        // Set the user ID only once, and do not permit changing it via the UI (unless the user has been informed of the consequences).
        // Could write a short wiki page detailing the process of changing the user ID.
        /// <summary>
        /// The path to the shortcuts.vdf file, containing all Steam non-Steam shortcuts.
        /// Note that the file does not necessarily exist, if no shortcut has ever been added.
        /// </summary>
        public string SteamShortcutsPath { get => steamShortcutsPath; set => SetValue(ref steamShortcutsPath, value); }
        public string PlayniteOverlayName { get => playniteOverlayName; set => SetValue(ref playniteOverlayName, value); }
        public bool UsePlayniteOverlay { get => usePlayniteOverlay; set => SetValue(ref usePlayniteOverlay, value); }
        public bool UseIntegrationFullscreen { get => useIntegrationFullscreen; set => SetValue(ref useIntegrationFullscreen, value); }
        public bool DefaultUseIntegrationDesktop { get => defaultUseIntegrationDesktop; set => SetValue(ref defaultUseIntegrationDesktop, value); }
        public bool UseDefaultOverlay { get => useDefaultOverlay; set => SetValue(ref useDefaultOverlay, value); }
        public string DefaultOverlayName { get => defaultOverlayName; set => SetValue(ref defaultOverlayName, value); }

        /// <summary>
        /// Read-only and therefore thread-safe.
        /// </summary>
        [DontSerialize]
        public string GlosSITargetsPath { get => glosSITargetsPath; }
        [DontSerialize]
        public string StartPlayniteFromGlosSIScriptPath { get => startPlayniteFromGlosSIScriptPath; }
        /// <summary>
        /// If the the file does not exist, creates it.
        /// </summary>
        [DontSerialize]
        public string DefaultTargetPath { get => GetDefaultTargetPath(); }
        [DontSerialize]
        public string KnownTargetsPath { get => knownTargetsPath; }
        [DontSerialize]
        public Version GlosSIVersion { get => GetGlosSIVersion(); set => glosSIVersion = value; }

        private Version GetGlosSIVersion()
        {
            if (glosSIVersion == null && GlosSIPath != null)
            {
                try
                {
                    string glosSIConfigPath = Path.Combine(GlosSIPath, "GlosSIConfig.exe");
                    string version = FileVersionInfo.GetVersionInfo(glosSIConfigPath).ProductVersion;
                    version = string.Concat(version.TakeWhile(c => char.IsDigit(c) || c == '.'));
                    glosSIVersion = new Version(version);
                }
                catch (Exception e)
                {
                    RethrowException(e, "LOC_GI_GetGlosSIVersionUnexpectedError");
                }
            }

            return glosSIVersion;
        }

        private string GetDefaultTargetPath()
        {
            if (!File.Exists(defaultTargetPath))
            {
                CreateDefaultTarget();
            }

            return defaultTargetPath;
        }

        /// <summary>
        /// Creates the "DefaultTargets.json" file. If the file already exists, overwrites it.
        /// </summary>
        public void CreateDefaultTarget()
        {
            try
            {
                LogManager.GetLogger().Trace("Creating DefaultTarget file...");
                File.WriteAllBytes(defaultTargetPath, Properties.Resources.DefaultTarget);
                LogManager.GetLogger().Info("DefaultTarget file created.");
            }
            catch (Exception e)
            {
                RethrowException(e, "LOC_GI_CreateDefaultTargetFileUnexpectedError"); // TODO: Unnecessary localized string?
            }
        }

        private void RethrowException(Exception e, string locKey)
        {
            string message = string.Format(locKey, e.Message);
            LogManager.GetLogger().Error(message);
            throw new Exception(message, e);
        }
    }
}