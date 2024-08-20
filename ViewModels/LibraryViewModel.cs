using CommunityToolkit.Mvvm.Input;
using RTJuke.Core.Library;
using RTJuke.Core.Plugins;
using RTJuke.Core.Services;
using RTJuke.Core.Types;
using RTJuke.UICore.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace RTJuke.UICore.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        IAlbumArtProvider AlbumArtProvider { get; set; }
        IPluginService PluginService { get; set; }

        IMusicLibrary musicLib;
        string _filterText = "";
        bool _searchCompleteLibrary = false;

        IPlaylistService playlistService;
        public ObservableCollection<SongViewModel> FilteredList { get; private set; }

        #region Commands

        public ICommand AddItemToPlaylistCommand { get; private set; }
        public ICommand QuickplayItemCommand { get; private set; }

        #endregion

        public String FilterText
        {
            get
            {
                return _filterText;
            }
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged(nameof(FilterText));
                    Refilter();
                }
            }
        }

        /// <summary>
        /// true = search complete library
        /// false = only search unplayed songs
        /// </summary>
        public bool SearchCompleteLibrary
        {
            get
            {
                return _searchCompleteLibrary;
            }
            set
            {
                if (_searchCompleteLibrary != value)
                {
                    _searchCompleteLibrary = value;
                    OnPropertyChanged(nameof(SearchCompleteLibrary));
                    Refilter();
                }
            }
        }

        public LibraryViewModel(IMusicLibrary _musicLib, IPluginService pluginService, IAlbumArtProvider albumArtProvider, IPlaylistService _playlistService)
        {
            PluginService = pluginService;
            AlbumArtProvider = albumArtProvider;            

            musicLib = _musicLib;
            playlistService = _playlistService;

            FilteredList = new ObservableCollection<SongViewModel>();

            // Commands            
            AddItemToPlaylistCommand = new RelayCommand<SongViewModel?>((svm) => playlistService.Enqueue(svm.Model), svm => svm != null);
            QuickplayItemCommand = new RelayCommand<SongViewModel>((svm) => playlistService.Quickplay(svm.Model), svm => svm != null);
        }

        /// <summary>
        /// Führt den Filter erneut aus
        /// </summary>
        private void Refilter()
        {
            FilteredList.Clear();

            String[] filterWords = FilterText.Split(' ').Select(x => x.ToLower()).ToArray();

            IEnumerable<Song> filteredSongs;

            if (SearchCompleteLibrary)
            {
                filteredSongs = musicLib.GetSongs().Where(x => IsSongInFilter(x, filterWords));
            }
            else
            {
                filteredSongs = musicLib.GetSongs().Where(x => !x.PlayedAlready && IsSongInFilter(x, filterWords));
            }

            foreach (Song s in filteredSongs)
            {
                FilteredList.Add(new SongViewModel(s, PluginService.GetLibraryProvider(s.ProviderId), AlbumArtProvider));
            }
        }

        private bool IsSongInFilter(Song song, String[] filterWords)
        {
            bool inFilter = true;

            foreach (string w in filterWords)
            {
                if (song.Title.ToLower().Contains(w))
                    continue;

                if (song.Artist.ToLower().Contains(w))
                    continue;

                if (song.Album.ToLower().Contains(w))
                    continue;

                if (song.Keywords.Any(x => x.Name.ToLower().Contains(w)))
                    continue;

                inFilter = false;
                break;
            }

            return inFilter;
        }      
    }
}
