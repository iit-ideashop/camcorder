using System;
using System.Globalization;
using System.Text.Json;
using System.IO;
using System.Security.Policy;

namespace Camcorder
{
    [Serializable]
    public class Config
    {
        /// <summary>
        /// The location where video files should be stored.
        /// </summary>
        public string VideoLocation { get; set; }
        /// <summary>
        /// The list of cameras.
        /// </summary>
        public CameraConfig[] Cameras { get; set; }

        #region Serialization/Deserialization
        public static Config FromFile(string fileName)
        {
            using (FileStream fs = File.OpenRead(fileName))
            {
                StreamReader reader = new StreamReader(fs);
                return JsonSerializer.Deserialize<Config>(reader.ReadToEnd());
            }
        }

        public void ToFile(string fileName)
        {
            using (FileStream fs = File.OpenWrite(fileName))
            {
                StreamWriter writer = new StreamWriter(fs);
                writer.Write(JsonSerializer.Serialize(this));
            }
        }
        #endregion
    }

    [Serializable]
    public class CameraConfig
    {
        /// <summary>
        /// The name of the camera. Will be used to create folder structure.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The format for the URL. {0} will be replaced with the camera index.
        /// </summary>
        public string UrlFormat { get; set; }
        /// <summary>
        /// The index of the first camera to record from. If there's only one camera, set to 0.
        /// </summary>
        public int FirstCamera { get; set; }
        /// <summary>
        /// The index of the last camera to record from. If there's only one camera, set to 0.
        /// </summary>
        public int LastCamera { get; set; }
        /// <summary>
        /// The number of days to retain clips from this device.
        /// </summary>
        public int RetentionDays { get; set; }
        /// <summary>
        /// The maximum length of a recording segment.
        /// </summary>
        public int ClipMinutes { get; set; }

    }
}
