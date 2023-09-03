using GlosSIIntegration.Models.GlosSITargets.Types;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace GlosSIIntegration.Models
{
    internal static class TargetsVersionMigrator
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Attempts to migrate targets to the latest version, if possible.
        /// </summary>
        /// <param name="lastVersion">The last "Targets.json" version, 
        /// or <c>0</c> if there was no earlier version.</param>
        public static void TryMigrate(int lastVersion)
        {
            if (lastVersion == 0)
            {
                

                List<GlosSITarget> targetsToMigrate = GetTargetsToMigrate();
                if (targetsToMigrate.Count == 0)
                {
                    logger.Info("The extension has not been used before: " +
                        "target settings migration is not needed.");
                }
                else if (string.IsNullOrEmpty(GlosSIIntegration.GetSettings().SteamShortcutsPath))
                {
                    throw new InvalidOperationException("Cannot migrate settings: The path to Steam has not been set.");
                }
                else
                {
                    logger.Info("Migrating: Overwriting all target settings.");
                    MigrateOverwriteTargetSettings(targetsToMigrate);
                }
            }
        }

        private static List<GlosSITarget> GetTargetsToMigrate()
        {
            List<GlosSITarget> targets = new List<GlosSITarget>();

            if (PlayniteGlosSITarget.Exists())
            {
                targets.Add(new PlayniteGlosSITarget());
            }

            if (DefaultGlosSITarget.Exists())
            {
                targets.Add(new DefaultGlosSITarget());
            }

            foreach (Game game in GlosSIIntegration.Api.Database.Games)
            {
                if (GlosSIIntegration.GameHasIntegratedTag(game))
                {
                    targets.Add(new GameGlosSITarget(game));
                }
            }

            return targets;
        }

        private static void MigrateOverwriteTargetSettings(List<GlosSITarget> targetsToMigrate)
        {
            GlosSIIntegration.GetSettings().CreateDefaultTarget();

            ShowMigrationMessage();

            GlobalProgressResult result = GlosSIIntegration.Api.Dialogs.ActivateGlobalProgress(
                (progressBar) => OverwriteTargetSettings(progressBar, targetsToMigrate),
                new GlobalProgressOptions("Updating GlosSI Targets...", false)
                {
                    IsIndeterminate = false
                });

            if (result.Error != null)
            {
                throw new Exception("Failed to overwrite target settings!", result.Error);
            }
        }

        private static void OverwriteTargetSettings(GlobalProgressActionArgs progressBar, List<GlosSITarget> targets)
        {
            progressBar.ProgressMaxValue = targets.Count;

            foreach (GlosSITarget target in targets)
            {
                if (target.File.Exists())
                {
                    target.File.Overwrite();
                }
                else
                {
                    logger.Warn($"Could not find the \"{target.Name}\" target file.");
                }
                progressBar.CurrentProgressValue++;
            }
        }

        private static void ShowMigrationMessage()
        {
            // Most strings are not localized here,
            // since the migration message will be shown to users only once, most likely before the strings are ever localized.
            const string message =
                "The new version of the GlosSI Integration extension adds support for running the shortcuts created by this extension from Steam. " +
                "Additionally, if you use the Playnite library overlay, you can now switch between Steam and fullscreen Playnite. " +
                "Start the Playnite overlay shortcut from Steam to enter Playnite. " +
                "Exit the Playnite overlay (via the Steam overlay) to switch to Steam Big Picture mode.\r\n\r\n" +
                "The settings of the currently existing GlosSI targets have to be updated and " +
                "Playnite game images will be added to the Steam shortcuts (when possible). " +
                "Click the OK button to update all targets used by this extension. " +
                "The default settings have also been updated: " +
                "if you know what you are doing and want to change them before proceeding, click the \"Review DefaultTarget.json\" button.";

            List<MessageBoxOption> options = new List<MessageBoxOption>
                        {
                            new MessageBoxOption("Review DefaultTarget.json", false, false),
                            new MessageBoxOption(ResourceProvider.GetString("LOCOKLabel"), true, true)
                        };
            MessageBoxOption result = GlosSIIntegration.Api.Dialogs.ShowMessage(message,
                ResourceProvider.GetString("LOC_GI_DefaultWindowTitle") + " – Update notice", MessageBoxImage.Information, options);

            if (result == options[0])
            {
                GlosSIIntegrationSettingsView.OpenDefaultGlosSITarget();
                ShowMigrationMessage();
            }
        }
    }
}
