using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.GlosSITargets.Files
{
    /// <summary>
    /// Represents the GlosSI target settings.
    /// <para>
    /// See GlosSI <a href="https://github.com/Alia5/GlosSI/blob/main/GlosSITarget/Settings.h">Settings.h</a> 
    /// </para>
    /// </summary>
    internal class GlosSITargetSettings
    {
        private static readonly HttpClient httpClient;
        private readonly string originalFilePath;
        private readonly JObject jObj;
        private const string nameKey = "name";
        public string Name
        {
            get => jObj.ToObject<string>(nameKey);
            set => jObj.SetPropertyValue(nameKey, value);
        }
        private const string iconKey = "icon";
        /// <summary>
        /// Steam shortcut icon path.
        /// </summary>
        public string Icon
        {
            get => jObj.ToObject<string>(iconKey);
            set => jObj.SetPropertyValue(iconKey, value);
        }
        private const string launchKey = "launch";
        public LaunchOptions Launch { get; set; }

        static GlosSITargetSettings()
        {
            httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:8756/")
            };
        }

        private GlosSITargetSettings(JObject jObj, string originalFilePath)
        {
            this.jObj = jObj;
            this.originalFilePath = originalFilePath;
            Launch = jObj.ToObject<LaunchOptions>(launchKey);
        }

        private GlosSITargetSettings(JObject jObj) : this(jObj, null)
        {
            originalFilePath = new GlosSITargetFileInfo(Name).FullPath;
        }

        public static async Task<GlosSITargetSettings> ReadFromAsync(string filePath)
        {
            return new GlosSITargetSettings(
                await JsonExtensions.ReadFromFileAsync(filePath).ConfigureAwait(false), filePath);
        }

        public static GlosSITargetSettings ReadFrom(string filePath)
        {
            return new GlosSITargetSettings(JsonExtensions.ReadFromFile(filePath), filePath);
        }

        private void RefreshJObj()
        {
            jObj.SetPropertyValue(launchKey, JToken.FromObject(Launch));
        }

        public void WriteTo()
        {
            WriteTo(originalFilePath);
        }

        public void WriteTo(string filePath)
        {
            RefreshJObj();
            jObj.WriteToFile(filePath);
        }

        public async Task WriteToAsync()
        {
            await WriteToAsync(originalFilePath).ConfigureAwait(false);
        }

        public async Task WriteToAsync(string filePath)
        {
            RefreshJObj();
            await jObj.WriteToFileAsync(filePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the settings of the currently running GlosSITarget process.
        /// If GlosSITarget is not currently running, a <see cref="HttpRequestException"/> will be thrown.
        /// </summary>
        /// <returns>The settings object.</returns>
        /// <exception cref="HttpRequestException">If, among other things, GlosSITarget is 
        /// not currently running.</exception>
        public static async Task<GlosSITargetSettings> ReadCurrent()
        {
            using (HttpResponseMessage response = await httpClient.GetAsync("settings").ConfigureAwait(false))
            using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
            {
                return new GlosSITargetSettings(
                    (JObject)await JToken.ReadFromAsync(jsonReader).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Represents the launch options available to a GlosSI target.
        /// </summary>
        public class LaunchOptions // Includes every property as of GlosSI version 0.1.2.0.
        {
            /// <summary>
            /// Whether to launch the <see cref="LaunchPath"/> when the target is run.
            /// </summary>
            [JsonProperty("launch")]
            public bool Launch { get; set; }
            [JsonProperty("launchPath")]
            public string LaunchPath { get; set; }
            [JsonProperty("launchAppArgs")]
            public string LaunchAppArgs { get; set; }
            [JsonProperty("closeOnExit")]
            public bool CloseOnExit { get; set; }
            [JsonProperty("waitForChildProcs")]
            public bool WaitForChildProcs { get; set; }
            [JsonProperty("isUWP")]
            public bool IsUWP { get; set; }
            [JsonProperty("ignoreLauncher")]
            public bool IgnoreLauncher { get; set; }
            [JsonProperty("killLauncher")]
            public bool KillLauncher { get; set; }
            [JsonProperty("launcherProcesses")]
            public List<string> LauncherProcesses { get; set; }

            /// <summary>
            /// Instantiates a <see cref="LaunchOptions"/> object with default values such that 
            /// everything is turned off and <see cref="IsUWP"/> is set to false. 
            /// </summary>
            public LaunchOptions()
            {
                Launch = false;
                LaunchPath = null;
                LaunchAppArgs = null;
                CloseOnExit = false;
                WaitForChildProcs = false;
                IsUWP = false;
                IgnoreLauncher = true;
                KillLauncher = false;
                LauncherProcesses = new List<string>();
            }

            public bool IsEveryPropertyEqual(LaunchOptions other)
            {
                return Launch == other.Launch
                    && LaunchPath == other.LaunchPath
                    && LaunchAppArgs == other.LaunchAppArgs
                    && CloseOnExit == other.CloseOnExit
                    && WaitForChildProcs == other.WaitForChildProcs
                    && IsUWP == other.IsUWP
                    && IgnoreLauncher == other.IgnoreLauncher
                    && KillLauncher == other.KillLauncher
                    && (
                        LauncherProcesses == other.LauncherProcesses 
                        || LauncherProcesses != null 
                        && other.LauncherProcesses != null 
                        && LauncherProcesses.SequenceEqual(other.LauncherProcesses)
                    );
            }

            public override string ToString()
            {
                return JToken.FromObject(this).ToString();
            }
        }
    }
}
