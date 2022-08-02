﻿using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GlosSIIntegration
{
    public class ShortcutCreationViewModel : ObservableObject
    {
        private readonly Image iconPreview;

        private string shortcutName;
        public string ShortcutName { 
            get { return shortcutName; }
            set { if (shortcutName == value) return; SetValue(ref shortcutName, value); }
        }

        private string shortcutIconPath;
        public string ShortcutIconPath
        {
            get { return shortcutIconPath; }
            set
            {
                if (shortcutIconPath == value) return;
                SetValue(ref shortcutIconPath, value);

                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                {
                    try
                    {
                        iconPreview.Source = new BitmapImage(new Uri(value, UriKind.RelativeOrAbsolute));
                        return;
                    }
                    catch { }
                }

                try
                {
                    iconPreview.Source = new BitmapImage(new Uri($"pack://application:,,,/" +
                        $"{Assembly.GetExecutingAssembly()};component/Resources/DefaultSteamShortcutIcon.png"));
                }
                catch (Exception e)
                {
                    // This could lead to log spamming.
                    LogManager.GetLogger().Error(e, "Failed to read \"DefaultSteamShortcutIcon.png\":");
                    iconPreview.Source = null;
                }
            }
        }

        /// <summary>
        /// Creates a new <c>ShortcutCreationViewModel</c> object.
        /// </summary>
        /// <param name="defaultName">The default name of the new Steam shortcut.</param>
        /// <param name="defaultIconPath">The default icon path of the new Steam shortcut.</param>
        /// <param name="previewIconImage">The image used to display the currently selected icon.</param>
        public ShortcutCreationViewModel(string defaultName, string defaultIconPath, Image previewIconImage)
        {
            iconPreview = previewIconImage;
            ShortcutName = defaultName;
            ShortcutIconPath = defaultIconPath;
        }

        public RelayCommand<object> BrowseIcon
        {
            get => new RelayCommand<object>((o) =>
            {
                // TODO: All files are currently accepted,
                // as I am not aware of any exhaustive list of file types supported by Steam as icons.
                // It would probably suffice to simply verify that some common image formats
                // (i.e. primarily those supported by the Image class) work and filter those.
                string filePath = API.Instance.Dialogs.SelectFile("Image|*.*");
                if (!string.IsNullOrEmpty(filePath)) ShortcutIconPath = filePath;
            });
        }

        /// <summary>
        /// Verifies the Steam shortcut name.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the name is valid; false otherwise.</returns>
        private bool VerifyName(ref List<string> errors)
        {
            if (string.IsNullOrEmpty(ShortcutName))
            {
                errors.Add("The name of the Steam shortcut has not been set.");
                return false;
            }

            if (GlosSITarget.HasJsonFile(ShortcutName))
            {
                // TODO: Give the user the choice to use the already existing GlosSI target file instead.
                errors.Add("A GlosSI target file already exists with the chosen shortcut name.");
                LogManager.GetLogger().Warn("A GlosSI target file already exists with the chosen name.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies the Steam shortcut icon path.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the name is valid; false otherwise.</returns>
        private bool VerifyIconPath(ref List<string> errors)
        {
            // The icon can be left unset if the user so wishes.
            if (string.IsNullOrEmpty(ShortcutIconPath))
            {
                return true;
            }

            if (!File.Exists(ShortcutIconPath))
            {
                errors.Add("The icon could not be found.");
                return false;
            }

            if (Path.GetFileName(ShortcutIconPath).EndsWith(".exe"))
            {
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption("Continue", false, false),
                    new MessageBoxOption("Cancel", true, true)
                };

                if (API.Instance.Dialogs.ShowMessage("The icon path leads to an executable (.exe) file. Any transparency will be lost.",
                    "GlosSI Integration", MessageBoxImage.Warning, options).Equals(options[1]))
                {
                    return false;
                }
            }

            try
            {
                shortcutIconPath = Path.GetFullPath(ShortcutIconPath);
            }
            catch (Exception e)
            {
                LogManager.GetLogger().Error(e, "Unexpected error encountered when reading the icon path:");
                errors.Add($"Unexpected error encountered when reading the icon path: {e.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a Steam shortcut and a GlosSITarget.
        /// The new shortcut uses <see cref="ShortcutName"/> and <see cref="ShortcutIconPath"/>.
        /// Anything wrong with the name and icon is shown to the user.
        /// </summary>
        /// <returns>true if the name and icon was valid and a Steam shortcut was successfully created; false otherwise.</returns>
        public bool Create()
        {
            List<string> errors = new List<string>();

            if (VerifyName(ref errors) & VerifyIconPath(ref errors))
            {
                try
                {
                    GlosSITarget target = new GlosSITarget(ShortcutName, ShortcutIconPath);
                    target.Create();
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.GetLogger().Error(ex, "Something went wrong when attempting to create the Steam shortcut:");
                    API.Instance.Dialogs.ShowErrorMessage($"Something went wrong when attempting to create the Steam shortcut: {ex.Message}",
                        "GlosSI Integration");
                }
            }
            else if (errors.Count > 0)
            {
                API.Instance.Dialogs.ShowErrorMessage(string.Join("\n", errors), "GlosSI Integration");
            }

            return false;
        }
    }
}