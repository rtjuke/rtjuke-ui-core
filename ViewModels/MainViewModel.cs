using CommunityToolkit.Mvvm.Input;
using RTJuke.Core.Audio;
using RTJuke.Core.Library;
using RTJuke.Core.Logging;
using RTJuke.Core.Plugins.Communication.CoreMessages;
using RTJuke.Core.Plugins.Communication;
using RTJuke.Core.Plugins;
using RTJuke.Core.Services;
using RTJuke.Core.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using RTJuke.UICore.IntegratedPlugins;
using RTJuke.UICore.Services;

namespace RTJuke.UICore.ViewModels
{
    /// <summary>
    /// Das MainViewModel für das MainWindow
    /// </summary>
    public class MainViewModel : ViewModelBase, IPlaylistService
    {
        public const int CROSSFADE_TIME_MS = 2000;

        #region Services        
        protected IAlbumArtProvider AlbumArtProvider;
        protected IMusicLibrary MusicLibrary { get; }
        protected IDialogService DialogService { get; }
        protected IPluginService PluginService { get; }
        protected IPlayHistory PlayHistory { get; }
        protected IMessageBus MessageBus { get; }

        protected ISettings Settings { get; }

        protected IUpdateService UpdateService { get; }
        #endregion

        protected IMainDispatcher Dispatcher { get; }

        //DispatcherTimer visTimer;

        int masterVolume = 255; // 0 - 255        

        private SongViewModel _nowPlaying;

        // used to mark the current song as already played, when it starts playing for the first time
        bool markedAsPlayed = false;

        // the playlist (comingup & queued titles)
        public PlaylistViewModel Playlist { get; }

        IShuffler shuffler;
        //ArtistShuffler artistShuffler = new ArtistShuffler();        

        #region Commands        

        public ICommand ShowLibraryCommand { get; }

        public ICommand NextSongCommand { get; }

        public ICommand MuteCommand { get; }

        public ICommand AddExternalFilesCommand { get; }

        public ICommand ShowSettingsCommand { get; }

        #endregion

        #region Properties

        /// <summary>
        /// Der aktuell gespielte Song
        /// </summary>
        public SongViewModel NowPlaying
        {
            get => _nowPlaying; set
            {
                if (SetProperty(ref _nowPlaying, value))
                {
                    if (_nowPlaying != null && _nowPlaying.CurrentState == PlayState.Closed)
                        _nowPlaying.PrepareAsync();
                }
            }
        }

        /// <summary>
        /// Is audio currently playing?
        /// </summary>
        public bool IsPlaying => NowPlaying != null && NowPlaying.CurrentState == PlayState.Playing;


        public double PlaybackPositionPercent
        {
            get
            {
                if (NowPlaying == null || !NowPlaying.Length.HasValue || NowPlaying.Length.Value.TotalSeconds == 0)
                    return 0;
                else
                    return NowPlaying.PlaybackPosition.TotalSeconds / NowPlaying.Length.Value.TotalSeconds;
            }
        }

        public int Volume
        {
            get
            {
                return masterVolume;
            }
            set
            {
                if (masterVolume != value && value >= 0 && value <= 255)
                {
                    masterVolume = value;

                    if (NowPlaying != null)
                        NowPlaying.Volume = value;

                    OnPropertyChanged();
                }
            }
        }

        bool updateAvailable = false;
        public bool UpdateAvailable { get => updateAvailable; private set => SetProperty(ref updateAvailable, value); }

        /// <summary>
        /// Controls added by Plugins
        /// </summary>
        public ObservableCollection<PluginCustomControl> BelowPlaylistControls { get; set; }
        public ObservableCollection<PluginCustomControl> VisualizationPaneControls { get; set; }

        #endregion

        #region Constructor
        public MainViewModel(IMusicLibrary musicLibrary, IDialogService dialogService, IPluginService pluginService, IPlayHistory playHistory, ISettings settings, IUpdateService updateService, IMainDispatcher dispatcher, IMessageBus messageBus, IAlbumArtProvider defaultAlbumArtProvider)
        {
            if (musicLibrary == null) return; // only for Avalonia designer

            MusicLibrary = musicLibrary;
            DialogService = dialogService;
            PluginService = pluginService;
            MessageBus = messageBus;
            PlayHistory = playHistory;
            AlbumArtProvider = defaultAlbumArtProvider;
            UpdateService = updateService;
            Settings = settings;
            Dispatcher = dispatcher;

            Playlist = new PlaylistViewModel(MessageBus, () =>
            {
                Song song = RandomSong();
                if (song != null)
                    return new SongViewModel(song, PluginService.GetLibraryProvider(song.ProviderId), AlbumArtProvider);
                return null;
            });

            Volume = Settings.Volume;

            // Removed: Plugin Anchors

            var loadedShuffler = PluginService.LoadedPlugins.FirstOrDefault(x => x.Type == PluginType.Shuffler);

            shuffler = (loadedShuffler?.Instance as IShuffler) ?? new DefaultShuffler();

            // timer which updates the visualization plugins
            /*visTimer = new DispatcherTimer();
            visTimer.Interval = TimeSpan.FromMilliseconds(25);
            visTimer.Tick += (s, e) =>
            {
                foreach (var plugin in PluginService.LoadedPlugins.Where(x => x.Type == PluginType.Visualization))
                    ((IVisualizer)plugin.Instance).Update(NowPlaying?.AudioFile);
            };
            visTimer.Start();*/

            // load music library
            MusicLibrary = musicLibrary;
            MusicLibrary.ContentUpdated += (s, e) =>
            {
                RefreshMusicLibraryData();

                // load songs if no songs were there before
                if (NowPlaying == null)
                {
                    LoadStartupSongs();
                }
            };

            PlayHistory.Begin();

            RefreshMusicLibraryData();

            // register to message bus
            MessageBus.Register(null, PlayerCommandMessage.Id, PlayerCommandMessage_Handler);

            // ensure that the audio threads are stopped on shutdown
            // TODO: find a nicer way to end this?
            /*Application.Current.Exit += (s, e) =>
            {
                if (NowPlaying != null)
                {
                    if (NowPlaying.CurrentState == PlayState.Playing)
                    {
                        Task.Run(() =>
                        {
                            // fade out the playing song
                            NowPlaying.AudioFile.FadeOut(1000);
                            Thread.Sleep(1000);
                            NowPlaying.AudioFile.Close();
                        });
                    }
                    else
                        NowPlaying.AudioFile?.Close();
                }

                Playlist.ComingUp?.AudioFile?.Close();
            };*/

            // start plugins
            foreach (var p in PluginService.LoadedPlugins)
            {
                p.Instance.Start();
            }

            // Commands
            ShowLibraryCommand = new RelayCommand(ShowLibraryCommand_Execute);
            NextSongCommand = new RelayCommand(NextSongCommand_Execute, NextSongCommand_CanExecute);
            MuteCommand = new RelayCommand(MuteCommand_Execute);
            AddExternalFilesCommand = new RelayCommand(AddExternalFilesCommand_Execute);
            ShowSettingsCommand = new RelayCommand(ShowSettingsCommand_Execute);

            LoadStartupSongs();

            // check for updates
            UpdateService.CheckForUpdate().ContinueWith((t) =>
            {
                UpdateAvailable = true;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
        #endregion

        #region MusicLib

        /// <summary>
        /// Fill NowPlaying and ComingUp after startup or MusicLibrary update
        /// </summary>
        void LoadStartupSongs()
        {
            // do we have a saved playlist from the last session? if so, restore it
            // TODO: re-implement HW-independent
            /*if (Settings.RestorePlaylistOnStartup && File.Exists(Settings.SessionPlaylistFile))
            {
                try
                {
                    LoadPlaylist(Settings.SessionPlaylistFile);
                }
                catch (Exception e)
                {
                    LogService.Error("Could not restore last session: " + e.Message);
                    NowPlaying = null;
                    Playlist.ReplaceComingUp(null);
                }
            }*/

            // set now playing and coming if they are null            
            if (NowPlaying == null)
            {
                var np = RandomSong();
                if (np != null)
                {
                    NowPlaying = new SongViewModel(np, PluginService.GetLibraryProvider(np.ProviderId), AlbumArtProvider);
                    if (NowPlaying != null)
                        PlaySong(NowPlaying, false, false);
                }
            }

            if (Playlist.ComingUp == null)
            {
                var cu = RandomSong();
                if (cu != null)
                    Playlist.ReplaceComingUp(new SongViewModel(cu, PluginService.GetLibraryProvider(cu.ProviderId), AlbumArtProvider));
            }
        }

        // TODO: re-implement HW-independently
        /*
        void LoadPlaylist(string filename)
        {
            IPlaylistSerializer pSerializer = new M3UPlaylistSerializer();
            ExtendedPlaylistInformation extInfo;

            if (NowPlaying != null && NowPlaying.IsPlaying)
                NowPlaying.PlayPauseCommand.Execute(null);

            Playlist.Clear();

            var songs = pSerializer.Read(filename, MusicLibrary, (s) =>
            {
                // try to read the song as external file
                if (File.Exists(s.Url))
                {
                    return ReadExternalFile(s.Url);
                }
                else
                    return null;
            }, out extInfo);

            if (songs.Count > 0)
            {
                NowPlaying = new SongViewModel(songs[0], PluginService.GetLibraryProvider(songs[0].ProviderId), AlbumArtProvider);

                if (NowPlaying != null)
                {
                    PlaySong(NowPlaying, false, false);

                    if (extInfo.FirstSongPlaybackPosition.HasValue)
                    {
                        NowPlaying.PlaybackPosition = extInfo.FirstSongPlaybackPosition.Value;
                    }
                }

                if (songs.Count > 1)
                {
                    Playlist.ReplaceComingUp(new SongViewModel(songs[1], PluginService.GetLibraryProvider(songs[1].ProviderId), AlbumArtProvider));
                }

                foreach (var song in songs.Skip(2))
                {
                    Enqueue(song);
                }
            }
        }*/

        void RefreshMusicLibraryData()
        {
            // TODO: inform current shuffler of changed music library
        }

        #endregion

        #region Playback          

        /// <summary>
        /// Plays the next song (the coming up song)
        /// </summary>
        public void PlayNextSong(bool crossFade, bool startPlaying = false)
        {
            IAudioFile? oldSong = NowPlaying?.AudioFile;

            if (oldSong != null)
            {
                if (!startPlaying)
                {
                    startPlaying = oldSong.CurrentState == PlayState.Playing || oldSong.CurrentState == PlayState.Ended;
                }                

                if (crossFade)
                {
                    oldSong.FadeOut(CROSSFADE_TIME_MS);

                    // free the song after the fading time
                    Thread t = new Thread(() => {
                        Thread.Sleep(CROSSFADE_TIME_MS);
                        oldSong.Stop();
                        oldSong.Close();
                    });
                    t.Start();
                }
                else
                {
                    oldSong.Stop();
                }
            }

            PlaySong(Playlist.ComingUp, startPlaying, crossFade);

            // Nachrücken
            var newComingUp = Playlist.TakeNext();
            if (newComingUp == null)
            {
                Song s = RandomSong();
                if (s == null)
                {
                    // TODO: what now? we have no more songs to play                 
                    newComingUp = null;
                }
                else
                    newComingUp = new SongViewModel(s, PluginService.GetLibraryProvider(s.ProviderId), AlbumArtProvider);
            }
            else
            {
                Dispatcher.Invoke(() => Playlist.Titles.Remove(newComingUp));
            }

            Playlist.ReplaceComingUp(newComingUp, false);
        }

        void PlaySong(SongViewModel song, bool startPlaying, bool fadeIn)
        {
            NowPlaying = song;            
            markedAsPlayed = false;
            song.Volume = masterVolume;

            song.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "CurrentState")
                {
                    if (s == NowPlaying)
                    {
                        switch (NowPlaying.CurrentState)
                        {
                            case PlayState.ReadyToPlay:
                                if (startPlaying)
                                {
                                    NowPlaying?.AudioFile?.Play();

                                    if (fadeIn)
                                        NowPlaying?.AudioFile?.FadeIn(CROSSFADE_TIME_MS);
                                }
                                break;
                            case PlayState.Ended:
                                Dispatcher.BeginInvoke(new Action(() => { PlayNextSong(false); }));
                                break;
                            case PlayState.Error:
                                // in case of error go to the next song
                                // TODO: inform user
                                PlayNextSong(false);
                                return;

                            // message bus notifications
                            case PlayState.Playing:
                                // mark the song as already played, when it actually starts playing
                                if (!markedAsPlayed)
                                {
                                    markedAsPlayed = true;
                                    NowPlaying.PlayedAlready = true;
                                    PlayHistory.Add(NowPlaying.Model);
                                }

                                MessageBus.PublishAsync(new PlayerStateEventMessage(PlayerStateEvent.Playing, (SongViewModel)s));
                                break;

                            case PlayState.Paused:
                            case PlayState.Stopped:
                            case PlayState.Closed:
                                MessageBus.PublishAsync(new PlayerStateEventMessage(PlayerStateEvent.Paused, (SongViewModel)s));
                                break;
                        }
                    }
                }
            };

            // has the song been preloaded?
            if (song.CurrentState == PlayState.Closed)
                song.PrepareAsync();
            else
            {
                if (song.CurrentState == PlayState.ReadyToPlay)
                {
                    if (startPlaying)
                    {
                        NowPlaying?.AudioFile?.Play();

                        if (fadeIn)
                            NowPlaying?.AudioFile?.FadeIn(CROSSFADE_TIME_MS);
                    }
                }
            }

            // OnPropertyChanged(nameof(PlaybackLength));

            // Inform plugins that a new song started playing
            MessageBus.PublishAsync(new PlayerStateEventMessage(PlayerStateEvent.NowPlayingSongChanged, NowPlaying));
        }

        /// <summary>
        /// Get non-blocked music library songs without currently playing or enqueued songs and which have'nt been played already
        /// </summary>
        /// <returns></returns>
        IList<Song> GetSongsNotInQueue()
        {
            var np = NowPlaying != null ? NowPlaying.Model : null;
            var cu = Playlist.ComingUp != null ? Playlist.ComingUp.Model : null;
            return MusicLibrary.GetUnplayedSongs().Except(new List<Song>() { np, cu }).Except(Playlist.Titles.Select(x => x.Model)).Where(x => !x.IsBlocked).ToList();
        }

        Song RandomSong()
        {
            var enqueuedSongs = new List<Song>();
            if (NowPlaying?.Model != null)
            {
                enqueuedSongs.Add(NowPlaying.Model);
            }
            if (Playlist.ComingUp?.Model != null)
            {
                enqueuedSongs.Add(Playlist.ComingUp.Model);
            }
            enqueuedSongs.AddRange(Playlist.Titles.Select(x => x.Model));
            return shuffler.Next(enqueuedSongs);            
         }

         #endregion

         #region IPlaylistService

         public Song GetNowPlaying()
         {
             return NowPlaying?.Model;
         }

         public Song GetComingUp()
         {
             return Playlist.ComingUp?.Model;
         }

         /// <summary>
         /// Return the contents of the playlist
         /// </summary>
         public IReadOnlyList<Song> GetCurrentlyEnqueued()
         {
             return new ReadOnlyCollection<Song>(Playlist.Titles.Select(x => x.Model).ToList());
         }

         public void Enqueue(Song song)
         {
             Dispatcher.Invoke(() => {
                 var vm = new SongViewModel(song, PluginService.GetLibraryProvider(song.ProviderId), AlbumArtProvider);
                 Playlist.Add(vm);
             });
         }

         public void Quickplay(Song song)
         {
             Dispatcher.Invoke(() => {
                 var vm = new SongViewModel(song, PluginService.GetLibraryProvider(song.ProviderId), AlbumArtProvider);

                 Playlist.PushComingUp(vm);
             });
         }

         #endregion

         #region External Files

         private Song ReadExternalFile(string filename)
         {

            throw new NotImplementedException();
        }

        public void SetExternalFileAsComingUp(String filename)
        {
            var s = ReadExternalFile(filename);
            if (s != null)
            {
                Playlist.PushComingUp(new SongViewModel(s, PluginService.GetLibraryProvider(s.ProviderId), AlbumArtProvider));
            }
        }

        public void AddExternalFileToPlaylist(String filename, int? position = null)
        {
            var s = ReadExternalFile(filename);
            if (s != null)
            {
                var svm = new SongViewModel(s, PluginService.GetLibraryProvider(s.ProviderId), AlbumArtProvider);

                if (NowPlaying == null)
                    NowPlaying = svm;
                else if (Playlist.ComingUp == null)
                    Playlist.ReplaceComingUp(svm);
                else
                {
                    if (!position.HasValue || position.Value >= Playlist.Count)
                        Playlist.Titles.Add(svm);
                    else
                        Playlist.Titles.Insert(position.Value, svm);
                }
            }
        }

        #endregion

        #region Command Implementations

        #region ShowLibraryCommand

        protected void ShowLibraryCommand_Execute()
        {
            DialogService.ShowMusicLibrary();
        }

        #endregion

        #region NextSongCommand

        protected bool NextSongCommand_CanExecute()
        {
            return Playlist.ComingUp != null;
        }

        protected void NextSongCommand_Execute()
        {
            PlayNextSong(true);
        }

        #endregion       

        #region MuteCommand

        int rememberedVolume = 0;

        protected void MuteCommand_Execute()
        {
            if (Volume > 0)
            {
                rememberedVolume = Volume;
                Volume = 0;
            }
            else
                Volume = rememberedVolume;
        }

        #endregion

        #region AddExternalFilesCommand

        protected void AddExternalFilesCommand_Execute()
        {
            string[] files;
            if (DialogService.ShowOpenFileDialog(out files))
            {
                foreach (var f in files)
                    AddExternalFileToPlaylist(f);
            }
        }

        #endregion

        #region ShowSettingsCommand

        protected void ShowSettingsCommand_Execute()
        {
            DialogService.ShowSettingsDialog();
        }

        #endregion

        #endregion

        #region MessageBus message handlers

        #region PlayerCommandMessage
        /// <summary>
        /// Handles the PlayerCommandMessage
        /// </summary>
        /// <param name="message"></param>
        protected void PlayerCommandMessage_Handler(IPluginMessage message)
        {
            var m = message as PlayerCommandMessage;
            if (m != null)
            {
                switch (m.Command)
                {
                    case PlayerCommand.Play:
                        if (NowPlaying != null && (NowPlaying.CurrentState == PlayState.ReadyToPlay || NowPlaying.CurrentState == PlayState.Paused || NowPlaying.CurrentState == PlayState.Stopped))
                            NowPlaying.PlayPauseCommand.Execute(null);
                        break;
                    case PlayerCommand.Pause:
                        if (NowPlaying != null && (NowPlaying.CurrentState == PlayState.Playing))
                            NowPlaying.PlayPauseCommand.Execute(null);
                        break;
                    case PlayerCommand.TogglePlayPause:
                        if (NowPlaying != null)
                            NowPlaying.PlayPauseCommand.Execute(null);
                        break;
                    case PlayerCommand.Next:
                        NextSongCommand.Execute(null);
                        break;
                }
            }
        }

        public void SaveCurrentSession()
        {
            // save the current playlist (and playback position of the currently playing song)
            // TODO: re-implement
            /*
            IPlaylistSerializer pSerializer = new M3UPlaylistSerializer();
            var plist = this;
            var songs = new List<Song>();
            var np = NowPlaying;
            if (np != null)
                songs.Add(np.Model);

            if (plist.GetComingUp() != null)
                songs.Add(plist.GetComingUp());
            songs.AddRange(plist.GetCurrentlyEnqueued());
            ExtendedPlaylistInformation extInfo = new ExtendedPlaylistInformation();
            extInfo.FirstSongPlaybackPosition = np?.PlaybackPosition;
            pSerializer.Write(Settings.SessionPlaylistFile, songs, extInfo);*/
        }

        #endregion

        #endregion

    }
}
