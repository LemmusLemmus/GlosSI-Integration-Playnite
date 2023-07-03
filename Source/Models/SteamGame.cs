using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GlosSIIntegration.Models
{
    /// <summary>
    /// Represents a shortcut to a Steam game.
    /// </summary>
    class SteamGame
    {
        private readonly ulong gameID;
        private readonly string gameName;

        /// <summary>
        /// Constructor for a non-Steam game.
        /// </summary>
        /// <param name="name">The name of the game.</param>
        /// <param name="path">The path to the game executable.</param>
        public SteamGame(string name, string path)
        {
            Crc algorithm = new Crc(32, 0x04C11DB7, true, 0xffffffff, true, 0xffffffff);
            string input = UTF8ToCodeUnits("\"" + path + "\"" + name);
            uint top32 = algorithm.BitByBit(input) | 0x80000000;
            gameID = (((ulong)top32) << 32) | 0x02000000;
            gameName = name;
        }

        private static string UTF8ToCodeUnits(string str)
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
        /// <exception cref="InvalidOperationException">If starting the process failed.</exception>
        public virtual void Run()
        {
            LogManager.GetLogger().Info($"Starting Steam game \"{this}\".");

            try
            {
                Process.Start("steam://rungameid/" + GetID().ToString()).Dispose();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception 
                || ex is ObjectDisposedException 
                || ex is System.IO.FileNotFoundException)
            {
                string msg = string.Format(
                    ResourceProvider.GetString("LOC_GI_RunSteamGameUnexpectedError"), ex.Message);
                GlosSIIntegration.NotifyError(msg, "GlosSIIntegration-SteamGame-Run");
                throw new InvalidOperationException(msg, ex);
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SteamGame other)) return false;

            return gameID == other.gameID;
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
