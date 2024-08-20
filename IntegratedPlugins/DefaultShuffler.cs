using RTJuke.Core.Plugins.Communication;
using RTJuke.Core.Plugins;
using RTJuke.Core.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RTJuke.UICore.IntegratedPlugins
{
    /// <summary>
    /// The simple, default shuffle logic
    /// Just plays songs which have not been played before
    /// </summary>
    public class DefaultShuffler : IShuffler
    {
        Random rand;

        public string HumanFriendlySettingsText
        {
            get
            {
                return "";
            }
        }

        public bool CanBeConfigured()
        {
            return false;
        }

        public bool Configure()
        {
            throw new NotImplementedException();
        }

        public string GetSettings()
        {
            return "";
        }

        public DefaultShuffler()
        {
            rand = new Random();
        }

        public void Init(IMessageBus messageBus)
        {
            // --
        }

        public void Start()
        {
            // --
        }

        public void SetLocalization(CultureInfo cultureInfo)
        {
            // --
        }

        public bool SetSettings(string settingsStr)
        {
            return true;
        }

        public void Shutdown()
        {
            // --
        }

        public Song Next(IReadOnlyList<Song> enqueuedSongs)
        {
            // MusicLibrary.GetUnplayedSongs().Except(enqueuedSongs)

            /* var lst = availableSongs.Where(x => !x.PlayedAlready).ToList();

             if (lst.Count == 0)
                 // no more songs
                 return null;

             return lst[rand.Next(lst.Count)];*/

            // TODO
            return null;
        }
    }
}