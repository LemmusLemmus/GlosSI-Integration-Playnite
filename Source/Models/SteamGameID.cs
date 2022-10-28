// Generation of values "top32" and "full64" are done via Corporal Quesadilla's Python function getURL()
// from their "Steam Shortcut Manager" at https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager.
// Corporal Quesadilla's getURL() function is licensed under the MIT License, copyright (c) Corporal Quesadilla 2018.

using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GlosSIIntegration
{
    /// <summary>
    /// Represents a Steam Game/App ID. This ID is used to run the Steam game.
    /// </summary>
    class SteamGameID
    {
        private readonly ulong gameID;
        public SteamGameID(string name, string path)
        {
            Crc algorithm = new Crc(32, 0x04C11DB7, true, 0xffffffff, true, 0xffffffff);
            string input = UTF8ToCodeUnits("\"" + path + "\"" + name);
            uint top32 = algorithm.BitByBit(input) | 0x80000000;
            gameID = (((ulong)top32) << 32) | 0x02000000;
        }

        // TODO: If the name of the game in playnite is changed, the correct SteamGameID won't be found.
        // It might therefore be better to store the IDs.
        public SteamGameID(Game playniteGame) : this(playniteGame.Name) { }

        public SteamGameID(string name) : this(name, 
            Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSITarget.exe").Replace('\\', '/')) { }

        public SteamGameID(ulong gameID)
        {
            this.gameID = gameID;
        }

        private string UTF8ToCodeUnits(string str)
        {
            return new string(Encoding.UTF8.GetBytes(str).Select(b => (char)b).ToArray());
        }

        public ulong GetSteamGameID()
        {
            return gameID;
        }

        /// <summary>
        /// Runs the Steam game associated with the ID.
        /// </summary>
        /// <returns>The started process; <c>null</c> if starting the process failed.</returns>
        public Process Run()
        {
            try
            {
                return Process.Start("steam://rungameid/" + GetSteamGameID().ToString());
            }
            catch (Exception e)
            {
                string message = string.Format(ResourceProvider.GetString("LOC_GI_RunSteamGameUnexpectedError"), 
                    e.Message);
                LogManager.GetLogger().Error(e, message);
                GlosSIIntegration.Api.Notifications.Add("GlosSIIntegration-SteamGameID-Run", 
                    message, NotificationType.Error);
                return null;
            }
        }
    }
}
