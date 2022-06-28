// Generation of values "top32" and "full64" are done via Corporal Quesadilla's Python function getURL()
// from their "Steam Shortcut Manager" at https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager.
// Corporal Quesadilla's getURL() function is licensed under the MIT License, copyright (c) Corporal Quesadilla 2018.

using Playnite.SDK.Models;
using System.Diagnostics;

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

        public SteamGameID(Game playniteGame) : this((new GlosSITarget(playniteGame)).GetJsonFileName(), GlosSIIntegration.GetSettings().glosSITargetsPath) { }

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

        public Process Run()
        {
            return Process.Start("steam://rungameid/" + GetSteamGameID());
        }
    }
}
