using Microsoft.Win32;
using System;
using System.IO;

namespace GlosSIIntegration.Models.SteamLauncher
{
    internal static class Steam
    {
        private const string SteamRegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam";
        private const string SteamActiveProcessRegistryPath = SteamRegistryPath + @"\ActiveProcess";
        private static string steamLanguage = null;
        private static string bpmWindowTitle;
        private static string desktopWindowTitle;
        private static string desktopMiniWindowTitle;

        /// <summary>
        /// The path to the Steam directory. For example: <code>c:/program files (x86)/steam</code>
        /// </summary>
        public static string Path { get; }

        /// <summary>
        /// The current mode Steam is in.
        /// <c>null</c> if Steam proper is not currently running.
        /// </summary>
        public static ISteamMode Mode
        {
            get
            {
                WinWindow window;
                ValidateWindowTitles();

                window = FindDesktopModeWindow();
                if (window != null) return new SteamDesktopMode(window);
                window = FindSteamWindow(bpmWindowTitle);
                if (window != null) return new SteamBigPictureMode(window);

                return null;
            }
        }

        /// <summary>
        /// The id of the currently active Steam user, or <c>0</c> if no user is currently active.
        /// </summary>
        public static uint ActiveUser
        {
            get
            {
                object registryValue = Registry.GetValue(SteamActiveProcessRegistryPath, "ActiveUser", null)
                ?? throw new FormatException("ActiveUser not found.");
                return (uint)(int)registryValue;
            }
        }

        static Steam()
        {
            // TODO: Handle exception? Should suffice to verify that Steam is installed in the verification code?
            Path = ReadSteamRegistryString("SteamPath");
            ValidateWindowTitles();
        }

        /// <summary>
        /// Validates the window titles. These can differ depending on which localized strings are used.
        /// </summary>
        private static void ValidateWindowTitles()
        {
            string curSteamLanguage = ReadSteamRegistryString("Language");
            if (steamLanguage != curSteamLanguage)
            {
                steamLanguage = curSteamLanguage;
                string localizations = ReadSteamUILocalizations();
                desktopWindowTitle = ReadSteamLocalizationString(localizations, "WindowName_SteamDesktop");
                desktopMiniWindowTitle = ReadSteamLocalizationString(localizations, "WindowName_SteamDesktopMini");
                bpmWindowTitle = ReadSteamLocalizationString(localizations, "SP_WindowTitle_BigPicture");
            }
        }

        /// <summary>
        /// Tries to find the running desktop mode window, if any.
        /// This is done without checking if the language of Steam (i.e. the searched for window titles) have changed.
        /// </summary>
        /// <returns>The found window, or <c>null</c> if no window was found.</returns>
        internal static WinWindow FindDesktopModeWindow()
        {
            return FindSteamWindow(desktopWindowTitle) ?? FindSteamWindow(desktopMiniWindowTitle);
        }

        /// <summary>
        /// Finds a Steam window with the supplied window title.
        /// </summary>
        /// <param name="windowTitle">The window title.</param>
        /// <returns>The found window, or <c>null</c> if no window was found.</returns>
        private static WinWindow FindSteamWindow(string windowTitle)
        {
            return WinWindow.Find("SDL_app", windowTitle);
        }

        private static string ReadSteamRegistryString(string valueName)
        {
            return Registry.GetValue(SteamRegistryPath, valueName, null) as string 
                ?? throw new FormatException($"\"{valueName}\" Steam registry string not found.");
        }

        private static string ReadSteamUILocalizations()
        {
            return File.ReadAllText($"{Path}/steamui/localization/steamui_{steamLanguage}-json.js");
        }

        // Simple key value reader.
        private static string ReadSteamLocalizationString(string localizations, string key)
        {
            key = "\"" + key + "\":\"";

            int titleKeyStartIndex = localizations.LastIndexOf(key, StringComparison.Ordinal);
            int titleKeyLength = key.Length;
            if (titleKeyStartIndex == -1)
            {
                throw new FileFormatException($"The {key} JSON key " +
                    "could not be found in the Steam localization file.");
            }

            int titleValueStartIndex = titleKeyStartIndex + titleKeyLength;
            // Assuming that this is not the last option, since the value includes ,".
            // This is the case for all currently searched for localized strings.
            int titleValueEndIndex = localizations.IndexOf(@""",""", titleValueStartIndex, StringComparison.Ordinal);
            if (titleValueEndIndex == -1)
            {
                throw new FileFormatException($"The end of the {key} JSON value " +
                    "could not be found in the Steam localization file.");
            }
            int titleValueLength = titleValueEndIndex - titleKeyStartIndex - titleKeyLength;

            return localizations.Substring(titleValueStartIndex, titleValueLength);
        }
    }
}