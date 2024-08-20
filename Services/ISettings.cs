using RTJuke.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace RTJuke.UICore.Services
{
    /// <summary>
    /// Klasse, die die Einstellungen der Anwendung hält
    /// </summary>
    public interface ISettings
    {
        public bool RunsInPortableMode { get; }

        public string DataDirectory { get; }

        public int Volume { get; set; }

        public bool RestorePlaylistOnStartup { get; set; }

        public bool RestorePlaybackPositionOnStartup { get; set; }

        /// <summary>
        /// Restores the application's settings as well as the settings of all loaded plugins
        /// </summary>
        public void Load(IPluginService pluginService);

        /// <summary>
        /// Saves the application's settings as well as the settings of all loaded plugins
        /// </summary>
        /// <param name="pluginService"></param>
        public void Save(IPluginService pluginService);
    }
}
