// Generation of values "top32" and "full64" are done via Corporal Quesadilla's Python function getURL()
// from their "Steam Shortcut Manager" at https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager.
// Corporal Quesadilla's getURL() function is licensed under the MIT License, copyright (c) Corporal Quesadilla 2018.

using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.IO;

namespace GlosSIIntegration
{
    class SteamGameID
    {
        private readonly uint top32;
        public SteamGameID(string name, string path)
        {
            Crc algorithm = new Crc(32, 0x04C11DB7, true, 0xffffffff, true, 0xffffffff);
            string input = "\"" + path + "\"" + name;
            this.top32 = algorithm.BitByBit(input) | 0x80000000;
        }

        // TODO: If the name of the game in playnite is changed, the correct SteamGameID won't be found.
        // It might therefore be better to store the IDs.
        public SteamGameID(Game playniteGame) : this(playniteGame.Name) { }

        public SteamGameID(string name) : this(name, 
            Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSITarget.exe").Replace('\\', '/')) { }

        public SteamGameID(uint top32)
        {
            this.top32 = top32;
        }

        public string GetSteamGameID()
        {
            ulong full64 = (((ulong)top32) << 32) | 0x02000000;
            return full64.ToString();
        }

        public uint GetTop32ID()
        {
            return top32;
        }

        /// <summary>
        /// Runs the Steam game associated with the ID.
        /// </summary>
        /// <returns>The started process; <c>null</c> if starting the process failed.</returns>
        public Process Run()
        {
            try
            {
                return Process.Start("steam://rungameid/" + GetSteamGameID());
            }
            catch (Exception e)
            {
                string message = $"GlosSI Integration failed to run the Steam Shortcut:\n{e.Message}";
                LogManager.GetLogger().Error($"{message}\t{e}");
                GlosSIIntegration.Api.Notifications.Add("GlosSIIntegration-SteamGameID-Run", 
                    message, NotificationType.Error);
                return null;
            }
        }
    }
}
