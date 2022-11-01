using Playnite.SDK;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GlosSIIntegration
{
    /// <summary>
    /// Represents a Steam game.
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

        public SteamGame(string name) : this(name, 
            Path.Combine(GlosSIIntegration.GetSettings().GlosSIPath, "GlosSITarget.exe").Replace('\\', '/')) { }

        private string UTF8ToCodeUnits(string str)
        {
            return new string(Encoding.UTF8.GetBytes(str).Select(b => (char)b).ToArray());
        }

        public ulong GetID()
        {
            return gameID;
        }

        public string GetName()
        {
            return gameName;
        }

        /// <summary>
        /// Runs the Steam game associated with the ID.
        /// </summary>
        /// <returns>true if the process was started; false if starting the process failed.</returns>
        public bool Run()
        {
            try
            {
                LogManager.GetLogger().Info($"Starting Steam game {this}.");
                Process.Start("steam://rungameid/" + GetID().ToString()).Dispose();
                return true;
            }
            catch (Exception e)
            {
                GlosSIIntegration.NotifyError(string.Format(ResourceProvider.GetString("LOC_GI_RunSteamGameUnexpectedError"),
                    e.Message), "GlosSIIntegration-SteamGame-Run");
                return false;
            }
        }

        /// <summary>
        /// Runs the GlosSITarget associated with this <c>SteamGame</c> via Steam.
        /// If the GlosSI configuration file could not be found, the method only displays an error notification.
        /// </summary>
        /// <returns>true if the process was started; false otherwise.</returns>
        /// <seealso cref="Run"/>
        public bool RunGlosSITarget()
        {
            LogManager.GetLogger().Trace($"Running GlosSITarget for {gameName}...");
            if (!GlosSITarget.HasJsonFile(gameName))
            {
                GlosSIIntegration.NotifyError(ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"), 
                    "GlosSIIntegration-SteamGame-RunGlosSITarget");
                return false;
            }

            return Run();
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
