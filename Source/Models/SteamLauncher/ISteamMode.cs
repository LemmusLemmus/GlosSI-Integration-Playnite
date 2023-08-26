namespace GlosSIIntegration.Models.SteamLauncher
{
    /// <summary>
    /// Represents the currently running mode of Steam. 
    /// Note that it can become invalid at any time: if the user changes mode or exits Steam.
    /// </summary>
    internal interface ISteamMode
    {
        /// <summary>
        /// The main Steam window.
        /// </summary>
        WinWindow MainWindow { get; }
    }
}