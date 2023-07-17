using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    internal static class JsonExtensions
    {
        public static async Task<JObject> ReadFromFileAsync(string filePath)
        {
            using (StreamReader file = File.OpenText(filePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                return (JObject)await JToken.ReadFromAsync(reader).ConfigureAwait(false);
            }
        }

        public static JObject ReadFromFile(string filePath)
        {
            using (StreamReader file = File.OpenText(filePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                return (JObject)JToken.ReadFrom(reader);
            }
        }

        public static async Task WriteToFileAsync(this JObject o, string filePath)
        {
            using (StreamWriter file = File.CreateText(filePath))
            using (JsonTextWriter writer = new JsonTextWriter(file)
            {
                // Use the same indentation GlosSI uses, for consistency.
                Formatting = Formatting.Indented,
                Indentation = 4
            })
            {
                await o.WriteToAsync(writer).ConfigureAwait(false);
                // Write a final new line simply to stay consistent with GlosSI.
                await file.WriteLineAsync().ConfigureAwait(false);
            }
        }

        public static void WriteToFile(this JObject o, string filePath)
        {
            using (StreamWriter file = File.CreateText(filePath))
            using (JsonTextWriter writer = new JsonTextWriter(file)
            {
                // Use the same indentation GlosSI uses, for consistency.
                Formatting = Formatting.Indented,
                Indentation = 4
            })
            {
                o.WriteTo(writer);
                // Write a final new line simply to stay consistent with GlosSI.
                file.WriteLine();
            }
        }

        /// <summary>
        /// Sets the value of a property, by replacing its value or adding the token if it does not exist.
        /// <para>Note: Does not handle nested <paramref name="propertyName"/>.</para>
        /// </summary>
        /// <param name="o">The JObject to modify.</param>
        /// <param name="propertyName">The name of the property to set the value of.</param>
        /// <param name="value">The new value.</param>
        public static void SetPropertyValue(this JObject o, string propertyName, JToken value)
        {
            JToken propertyToken = o.SelectToken(propertyName);
            if (propertyToken == null)
            {
                o.Add(propertyName, value);
            }
            else
            {
                propertyToken.Replace(value);
            }
        }

        /// <summary>
        /// Creates an instance of the specified type <typeparamref name="T"/> from the <paramref name="propertyName"/> 
        /// belonging to the <see cref="JToken"/>.
        /// </summary>
        /// <typeparam name="T">The object type that the token will be deserialized to.</typeparam>
        /// <param name="o">The JToken to access the property from.</param>
        /// <param name="propertyName">The name of the property to get the value of.</param>
        /// <returns>The deserialized object, or <c>null</c> if deserialization failed.</returns>
        public static T ToObject<T>(this JToken o, string propertyName) where T : class
        {
            try
            {
                return o.SelectToken(propertyName)?.ToObject<T>();
            }
            catch (JsonReaderException ex)
            {
                Playnite.SDK.LogManager.GetLogger().Trace(ex,
                    $"Failed to read JSON property {propertyName}.");
                return null;
            }
        }
    }
}
