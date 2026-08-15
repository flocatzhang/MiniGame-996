using System;
using System.IO;
using UnityEngine;

namespace OfficeHell.Config
{
    public interface IConfigSource
    {
        /// <summary>Returns the raw text of a config file, or null when it cannot be read.</summary>
        string Read(string fileName);

        string Describe();
    }

    /// <summary>
    /// Reads xml straight off disk under StreamingAssets so designers can edit the files that sit
    /// next to a built exe without opening Unity, and so F5 can reload them in play mode.
    /// WebGL would need the UnityWebRequest path instead, which is why this sits behind an interface.
    /// </summary>
    public sealed class XmlConfigSource : IConfigSource
    {
        readonly string _dir;

        public XmlConfigSource(string subFolder)
        {
            _dir = Path.Combine(Application.streamingAssetsPath, subFolder);
        }

        public string Read(string fileName)
        {
            string path = Path.Combine(_dir, fileName);
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogError("[Config] failed to read " + path + ": " + e.Message);
                return null;
            }
        }

        public string Describe()
        {
            return _dir;
        }
    }
}
