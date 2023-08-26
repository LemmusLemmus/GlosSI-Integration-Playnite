using Playnite.SDK.Models;
using System;
using System.IO;

namespace GlosSIIntegration.Models.GlosSITargets.Files
{
    /// <summary>
    /// Represents GlosSITarget launch options for launching the <c>StartPlayniteFromGlosSI.vbs</c> script, 
    /// used to launch Playnite games or the Playnite library from Steam.
    /// </summary>
    internal class StartFromSteamLaunchOptions : GlosSITargetSettings.LaunchOptions
    {
        private static readonly string wscriptPath = Path.Combine(Environment.SystemDirectory, "wscript.exe");
        private static readonly string scriptArgument = $@"""{GlosSIIntegration.GetSettings().StartPlayniteFromGlosSIScriptPath}""";

        private StartFromSteamLaunchOptions(string launchAppArgs) : base()
        {
            Launch = true;
            LaunchPath = wscriptPath;
            LaunchAppArgs = launchAppArgs;
        }

        public static StartFromSteamLaunchOptions GetLaunchPlayniteLibraryOptions()
        {
            return new StartFromSteamLaunchOptions(scriptArgument);
        }

        public static StartFromSteamLaunchOptions GetLaunchGameOptions(Game game)
        {
            return new StartFromSteamLaunchOptions($"{scriptArgument} {game.Id}");
        }

        public static bool LaunchesPlaynite(GlosSITargetSettings.LaunchOptions launchOptions)
        {
            return launchOptions.LaunchPath == wscriptPath && launchOptions.LaunchAppArgs == scriptArgument;
        }
    }
}
