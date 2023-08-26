using System.IO;

namespace GlosSIIntegration.Models.GlosSITargets.Files
{
    internal class GlosSITargetFileInfo
    {
        /// <summary>
        /// The filename of the .json GlosSITarget profile, without the ".json" file extension.
        /// </summary>
        public string Name { get; }
        public string FullPath { get; }

        public GlosSITargetFileInfo(string targetName)
        {
            Name = RemoveIllegalFileNameChars(targetName);
            FullPath = GetJsonFilePath(Name);
        }

        /// <summary>
        /// Checks if this object has a corresponding .json file.
        /// The actual name stored inside the .json file is not compared.
        /// </summary>
        /// <returns>true if the target has a corresponding .json file; false otherwise.</returns>
        public bool Exists()
        {
            return File.Exists(FullPath);
        }

        private static string RemoveIllegalFileNameChars(string filename)
        {
            if (filename == null) return null;
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        /// <summary>
        /// Gets the path to the .json with the supplied name.
        /// </summary>
        /// <param name="jsonFileName">The name of the .json file.</param>
        /// <returns>The path to the .json file.</returns>
        private static string GetJsonFilePath(string jsonFileName)
        {
            return Path.Combine(GlosSIIntegration.GetSettings().GlosSITargetsPath, jsonFileName + ".json");
        }
    }
}
