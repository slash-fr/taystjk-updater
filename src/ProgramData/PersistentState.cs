using LightJson;
using LightJson.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.ProgramData
{
    internal class PersistentState
    {
        /// <summary>
        /// The GitHub API must not be called before this date/time
        /// </summary>
        public DateTime? GithubThrottledUntil { get; set; }

        /// <summary>
        /// Date/time of the last check for updates by file path.
        /// </summary>
        public Dictionary<string, DateTime> LastCheckedByPath;

        private readonly string pathToJsonFile;

        public PersistentState()
        {
            GithubThrottledUntil = null;
            LastCheckedByPath = new Dictionary<string, DateTime>();

            pathToJsonFile = AppEnvironment.PathToUpdaterProgramData + Path.DirectorySeparatorChar + "state.json";
        }


        /// <summary>
        /// Loads the persistent state
        /// </summary>
        /// <exception cref="ProgramDataNotReadableException"></exception>
        public void Load()
        {
            if (!File.Exists(pathToJsonFile))
            {
                return;
            }

            try
            {
                JsonValue jsonData = JsonReader.ParseFile(pathToJsonFile);

                if (!jsonData.IsJsonObject)
                {
                    return;
                }

                if (jsonData["github_throttled_until"].IsDateTime)
                {
                    GithubThrottledUntil = jsonData["github_throttled_until"];
                }

                if (jsonData["last_checked"].IsJsonObject)
                {
                    foreach (KeyValuePair<string, JsonValue> item in jsonData["last_checked"].AsJsonObject)
                    {
                        if (item.Value.IsDateTime)
                        {
                            LastCheckedByPath[item.Key] = item.Value;
                        }
                    }
                }

            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is DirectoryNotFoundException ||
                    exception is SecurityException || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException || exception is IOException)
                {
                    throw new ProgramDataNotReadableException(exception);
                }

                // Unexpected exception, rethrow without encapsulating:
                throw exception;
            }
        }

        /// <summary>
        /// Saves the persistent state
        /// </summary>
        /// <exception cref="ProgramDataNotReadableException"></exception>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public void Save()
        {
            if (!Directory.Exists(AppEnvironment.PathToUpdaterProgramData))
            {
                try
                {
                    Directory.CreateDirectory(AppEnvironment.PathToUpdaterProgramData);
                }
                catch (Exception exception)
                {
                    if (exception is DirectoryNotFoundException || exception is IOException ||
                        exception is UnauthorizedAccessException || exception is PathTooLongException)
                    {
                        throw new ProgramDataNotWritableException(exception);
                    }

                    // Unexpected exception, rethrow without encapsulating:
                    throw exception;
                }
            }

            FileStream fileStream = null;
            StreamWriter streamWriter = null;

            try
            {
                fileStream = new FileStream(pathToJsonFile, FileMode.OpenOrCreate, FileAccess.Write);
                streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
                JsonWriter jsonWriter = new JsonWriter(streamWriter, pretty: true);

                JsonObject lastCheckedByPathJson = new JsonObject();
                foreach (KeyValuePair<string, DateTime> item in LastCheckedByPath)
                {
                    lastCheckedByPathJson.Add(item.Key, item.Value);
                }

                jsonWriter.Write(new JsonValue(new JsonObject{
                    ["github_throttled_until"] = GithubThrottledUntil,
                    ["last_checked"] = lastCheckedByPathJson,
                }));
                streamWriter.Flush();
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is DirectoryNotFoundException ||
                    exception is SecurityException || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException || exception is IOException)
                {
                    throw new ProgramDataNotReadableException(exception);
                }

                // Unexpected exception, rethrow without encapsulating:
                throw exception;
            }
            finally
            {
                streamWriter?.Dispose();
                fileStream?.Dispose();
            }
        }
    }
}
