using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GlosSIIntegration
{
    /// <summary>
    /// Represents a shortcut to a Steam game.
    /// </summary>
    class SteamGame
    {
        private readonly ulong gameID;
        private readonly string gameName;

        public SteamGame(string name, string path)
        {
            Crc algorithm = new Crc(32, 0x04C11DB7, true, 0xffffffff, true, 0xffffffff);
            string input = UTF8ToCodeUnits("\"" + path + "\"" + name);
            uint top32 = algorithm.BitByBit(input) | 0x80000000;
            gameID = (((ulong)top32) << 32) | 0x02000000;
            gameName = name;
        }

        private string UTF8ToCodeUnits(string str)
        {
            return new string(Encoding.UTF8.GetBytes(str).Select(b => (char)b).ToArray());
        }

        /// <summary>
        /// Gets the appID of the Steam game.
        /// </summary>
        /// <returns>The appID of the Steam game.</returns>
        public ulong GetID()
        {
            return gameID;
        }

        /// <summary>
        /// Gets the name of the Steam game.
        /// </summary>
        /// <returns>The name of the Steam game.</returns>
        public string GetName()
        {
            return gameName;
        }

        /// <summary>
        /// Runs the Steam game associated with the ID.
        /// </summary>
        /// <returns>true if the process was started; false if starting the process failed.</returns>
        public virtual bool Run()
        {
            try
            {
                LogManager.GetLogger().Info($"Starting Steam game {this}.");
                Process.Start("steam://rungameid/" + GetID().ToString()).Dispose();
                return true;
            }
            catch (Exception e)
            {
                GlosSIIntegration.NotifyError(
                    string.Format(ResourceProvider.GetString("LOC_GI_RunSteamGameUnexpectedError"),
                    e.Message), "GlosSIIntegration-SteamGame-Run");
                return false;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SteamGame other)) return false;

            return gameName == other.gameName;
        }

        public override int GetHashCode()
        {
            return (int)gameID;
        }

        public override string ToString()
        {
            return $"{gameName}: {gameID}";
        }
    }
}
