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
    class SteamShortcut
    {
        /// <summary>
        /// The appID of the Steam shortcut.
        /// </summary>
        public ulong Id { get; }

        /// <summary>
        /// The name of the Steam shortcut.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Constructor for a non-Steam game shortcut.
        /// </summary>
        /// <param name="name">The name of the game.</param>
        /// <param name="path">The path to the game executable.</param>
        public SteamShortcut(string name, string path)
        {
            Crc algorithm = new Crc(32, 0x04C11DB7, true, 0xffffffff, true, 0xffffffff);
            string input = UTF8ToCodeUnits("\"" + path + "\"" + name);
            uint top32 = algorithm.BitByBit(input) | 0x80000000;
            Id = (((ulong)top32) << 32) | 0x02000000;
            Name = name;
        }

        private static string UTF8ToCodeUnits(string str)
        {
            return new string(Encoding.UTF8.GetBytes(str).Select(b => (char)b).ToArray());
        }

        /// <summary>
        /// Runs the Steam shortcut.
        /// </summary>
        /// <exception cref="InvalidOperationException">If starting the process failed.</exception>
        public virtual void Run()
        {
            LogManager.GetLogger().Info($"Starting Steam game \"{this}\".");

            try
            {
                // The command "steam://rungameid/<id>" was used before,
                // since the below command apparently did not work with non-Steam shortcuts before.
                // The command has been changed because "steam://rungameid/<id>" shows
                // a "Launching..." pop-up window, which is undesirable when starting GlosSITarget.
                // Another (in this case irrelevant) difference is that "/Dialog" can be appended
                // to the command below to show multiple launch options (if there are any).
                // Other differences are unknown.
                Process.Start("steam://launch/" + Id.ToString()).Dispose();
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
            if (!(obj is SteamShortcut other)) return false;

            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public override string ToString()
        {
            return $"{Name}: {Id}";
        }
    }
}
