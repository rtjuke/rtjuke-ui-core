using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RTJuke.Core.Library;
using RTJuke.Core.Services;
using RTJuke.Core.Types;
using RTJuke.UICore.Utils;
using System.Linq;
using RTJuke.Core.Plugins;

namespace RTJuke.UICore.ViewModels
{
    /// <summary>
    /// Class which handles (re-)scanning the music library
    /// </summary>
    public class BackgroundLibraryScanner : ViewModelBase
    {
        TaskPool TaskPool { get; set; }

        #region Properties

        static BackgroundLibraryScanner instance = new BackgroundLibraryScanner();

        public static BackgroundLibraryScanner Instance
        {
            get
            {
                return instance;
            }
        }

        public bool IsBusy
        {
            get
            {
                return TaskPool.IsBusy;
            }
        }

        IProgressCallback scanProgress;

        public IProgressCallback ScanProgress
        {
            get
            {
                return scanProgress;
            }
            private set
            {
                if (scanProgress != value)
                {
                    scanProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor
        public BackgroundLibraryScanner()
        {
            TaskPool = new TaskPool();
            TaskPool.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(IsBusy))
                    OnPropertyChanged(nameof(IsBusy));
            };

            ScanProgress = new ScanProgressCallback();
            ScanProgress.IsIndeterminate = false;
            ScanProgress.CurrentStepProgress = 0;
            ScanProgress.CurrentStepMaximum = 1;
            ScanProgress.CurrentStepDescription = "";
        }
        #endregion

        public void Rescan(IMusicLibrary musicLib, IPlayHistory playHistory, IEnumerable<PluginViewModel> libSources)
        {
            ScanProgress.IsIndeterminate = true;
            ScanProgress.CurrentStepDescription = "Bibliothek wird aktualisiert";

            var mLibCallback = new ScanMusicLibraryCallback(musicLib, playHistory);

            TaskPool.Enqeue(() => Task.Run<bool>(async () =>
            {                
                foreach (var p in libSources)
                {
                    if (p.Instance is ILibraryProvider libProvider)
                    {
                        mLibCallback.CurrentProviderId = p.ProviderId;
                        List<Song> songsInLib = musicLib.GetSongs().Where(x => x.ProviderId == p.ProviderId).ToList();

                        libProvider.Update(songsInLib, mLibCallback, ScanProgress);
                    }
                }

                ScanProgress.IsIndeterminate = true;
                ScanProgress.CurrentStepDescription = "Bibliothek wird neu aufgebaut";
                musicLib.RequestRebuild();

                ScanProgress.CurrentStepDescription = "Bibliothek wird gespeichert";
                musicLib.Save();

                ScanProgress.IsIndeterminate = false;
                ScanProgress.CurrentStepDescription = "Abgeschlossen.";

                return true;
            }));
        }
    }


    class ScanMusicLibraryCallback : IMusicLibraryCallback
    {

        IMusicLibrary MusicLibrary { get; set; }
        IPlayHistory PlayHistory { get; set; }

        public string CurrentProviderId { get; set; }

        public ScanMusicLibraryCallback(IMusicLibrary musicLibrary, IPlayHistory playHistory)
        {
            MusicLibrary = musicLibrary;
            PlayHistory = playHistory;
        }

        public bool AddSong(Song song)
        {
            if (!MusicLibrary.GetSongs().Any(x => x.Title == song.Title && x.Artist == song.Artist && x.Album == song.Album))
            {
                song.ProviderId = CurrentProviderId;
                song.PlayedAlready = PlayHistory.WasPlayedAlready(song);
                MusicLibrary.GetSongs().Add(song);

                return true;
            }
            else
                return false;
        }

        public bool RemoveSong(Song song)
        {
            MusicLibrary.GetSongs().Remove(song);
            return true;
        }

        public bool UpdateSong(Song song)
        {
            // todo?
            return true;
        }
    }

    class ScanProgressCallback : ViewModelBase, IProgressCallback
    {
        bool isIndeterminate = false;
        public bool IsIndeterminate
        {
            get
            {
                return isIndeterminate;
            }
            set
            {
                if (isIndeterminate != value)
                {
                    isIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        string currentStepDescription = "";
        public string CurrentStepDescription
        {
            get
            {
                return currentStepDescription;
            }
            set
            {
                if (currentStepDescription != value)
                {
                    currentStepDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        int currentStepProgress = 0;
        public int CurrentStepProgress
        {
            get
            {
                return currentStepProgress;
            }
            set
            {
                if (currentStepProgress != value)
                {
                    currentStepProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        int currentStepMaximum = 0;
        public int CurrentStepMaximum
        {
            get
            {
                return currentStepMaximum;
            }
            set
            {
                if (currentStepMaximum != value)
                {
                    currentStepMaximum = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
