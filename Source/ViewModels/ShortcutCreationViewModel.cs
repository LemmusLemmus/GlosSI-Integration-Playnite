using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GlosSIIntegration.Models.GlosSITargets.Types;
using GlosSIIntegration.Models.GlosSITargets.Files;

namespace GlosSIIntegration
{
    internal class ShortcutCreationViewModel : ObservableObject
    {
        private Image iconPreview;

        private string shortcutName;
        public string ShortcutName
        {
            get => shortcutName;
            set { if (shortcutName == value) { return; } SetValue(ref shortcutName, value); }
        }

        private string shortcutIconPath;
        public string ShortcutIconPath
        {
            get => shortcutIconPath;
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
                        $"{typeof(ShortcutCreationViewModel).Assembly};component/Resources/DefaultSteamShortcutIcon.png")); // TODO: Test! Before Assembly.GetExecutingAssembly()
                }
                catch (Exception e)
                {
                    // This could lead to log spamming.
                    LogManager.GetLogger().Error(e, "Failed to read \"DefaultSteamShortcutIcon.png\":");
                    iconPreview.Source = null;
                }
            }
        }

        private readonly Func<string, GlosSITarget> targetGetter;
        private GlosSITarget createdTarget;

        /// <summary>
        /// Creates a new <c>ShortcutCreationViewModel</c> object.
        /// </summary>
        /// <param name="defaultName">The default name of the new Steam shortcut.</param>
        /// <param name="defaultIconPath">The default icon path of the new Steam shortcut.</param>
        /// <param name="previewIconImage">The image used to display the currently selected icon.</param>
        private ShortcutCreationViewModel(string defaultName, string defaultIconPath, Func<string, GlosSITarget> targetGetter)
        {
            iconPreview = null;
            createdTarget = null;
            ShortcutName = defaultName;
            ShortcutIconPath = defaultIconPath;
            this.targetGetter = targetGetter;
        }

        public void SetIconPreview(Image previewIconImage)
        {
            if (iconPreview != null)
            {
                throw new InvalidOperationException("ShortcutCreationViewModel already has an icon preview.");
            }

            iconPreview = previewIconImage;
        }

        /// <summary>
        /// Shows a shortcut creation dialog.
        /// </summary>
        /// <param name="defaultName">The default name of the new shortcut. 
        /// Can be left as <c>null</c>.</param>
        /// <param name="defaultIconPath">The path to the default icon of the new shortcut. 
        /// Can be left as <c>null</c>.</param>
        /// <returns>The name of the created shortcut; <c>null</c> if no shortcut was created.</returns>
        public static T ShowDialog<T>(string defaultName, string defaultIconPath, Func<string, T> targetGetter) where T : GlosSITarget
        {
            ShortcutCreationViewModel viewModel = new ShortcutCreationViewModel(defaultName, defaultIconPath, targetGetter);
            ShortcutCreationView shortcutCreationView = new ShortcutCreationView(viewModel);
            Window dialogWindow = API.Instance.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowCloseButton = true,
                ShowMaximizeButton = true,
                ShowMinimizeButton = true
            });

            dialogWindow.Content = shortcutCreationView;
            dialogWindow.Title = ResourceProvider.GetString("LOC_GI_ShortcutCreationWindowTitle");
            dialogWindow.SizeToContent = SizeToContent.WidthAndHeight;

            dialogWindow.ShowDialog();
            return (T)viewModel.createdTarget;
        }

        public RelayCommand<object> BrowseIcon
        {
            get => new RelayCommand<object>((o) =>
            {
                // The filter of supported image types is not necessarily exhaustive.
                string filterTypes = "*.PNG;*.ICO;*.JPG;*.JPEG;*.BMP;*.DIB;*.GIF;*.WEBP;*.EXE";
                string filePath = API.Instance.Dialogs.SelectFile($"{ResourceProvider.GetString("LOC_GI_SelectIconFileType")}|{filterTypes}");
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
                errors.Add(ResourceProvider.GetString("LOC_GI_SteamShortcutNameNotSetError"));
                return false;
            }

            if (new GlosSITargetFileInfo(ShortcutName).Exists())
            {
                // TODO: Give the user the choice to use the already existing GlosSI target file instead.
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutTargetAlreadyExistsError"));
                LogManager.GetLogger().Warn(ResourceProvider.GetString("LOC_GI_ShortcutTargetAlreadyExistsError"));
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

            try
            {
                shortcutIconPath = Path.GetFullPath(ShortcutIconPath);
            }
            catch
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutIconNotFoundError"));
                return false;
            }

            if (!File.Exists(ShortcutIconPath))
            {
                errors.Add(ResourceProvider.GetString("LOC_GI_ShortcutIconNotFoundError"));
                return false;
            }

            if (Path.GetFileName(ShortcutIconPath).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(ResourceProvider.GetString("LOCSaveLabel"), false, false),
                    new MessageBoxOption(ResourceProvider.GetString("LOCCancelLabel"), true, true)
                };

                if (API.Instance.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_ShortcutExeIconWarning"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), MessageBoxImage.Warning, options).Equals(options[1]))
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
                string message = string.Format(ResourceProvider.GetString("LOC_GI_ReadIconPathUnexpectedError"), e.Message);
                LogManager.GetLogger().Error(e, message);
                errors.Add(message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a Steam shortcut and a GlosSITarget file.
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
                    GlosSITarget target = targetGetter(ShortcutName);
                    if (target.File.Create(ShortcutIconPath))
                    {
                        createdTarget = target;
                    }
                    else
                    {
                        // TODO: Do something about this?
                    }
                    return true;
                }
                catch (GlosSITargetFile.UnsupportedCharacterException)
                {
                    GlosSIIntegration.WarnUnsupportedCharacters(
                        ResourceProvider.GetString("LOC_GI_ShortcutUnsupportedCharacterError"), MessageBoxImage.Error);
                }
                catch (GlosSITargetFile.UnexpectedGlosSIBehaviourException)
                {
                    LogManager.GetLogger().Error($"Creating shortcut \"{ShortcutName}\" " +
                        $"with icon path \"{ShortcutIconPath}\" lead to UnexpectedGlosSIBehaviour.");
                }
                catch (Exception ex)
                {
                    string message = string.Format(ResourceProvider.GetString("LOC_GI_CreateSteamShortcutUnexpectedError"), ex.Message);
                    LogManager.GetLogger().Error(ex, message);
                    API.Instance.Dialogs.ShowErrorMessage(message, ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
                }
            }
            else if (errors.Count > 0)
            {
                API.Instance.Dialogs.ShowErrorMessage(string.Join("\n", errors), 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }

            return false;
        }
    }
}