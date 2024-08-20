using CommunityToolkit.Mvvm.Input;
using RTJuke.Core.Audio;
using RTJuke.Core.Plugins;
using RTJuke.Core.Types;
using RTJuke.Core.ViewModels;
using RTJuke.UICore.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;

namespace RTJuke.UICore.ViewModels
{
    /// <summary>
    /// ViewModel für die Song Klasse
    /// </summary>
    public class SongViewModel : ViewModelBase, ISongViewModel, IDisposable
    {
        #region Services

        IAlbumArtProvider AlbumArtProvider { get; set; }
        ILibraryProvider LibraryProvider { get; set; }

        #endregion

        #region Variables

        public Song Model { get; private set; }

        /// <summary>
        /// The actual audio file
        /// </summary>
        public IAudioFile? AudioFile { get; private set; }

        #endregion

        #region Properties

        #region Song infos (read-only)
        public string Title => Model.Title;

        public string Album => Model.Album;

        public string Artist => Model.Artist;

        public string Track => Model.Track;

        public HashSet<Keyword> Keywords => Model.Keywords;        

        public string KeywordDisplayText => Model.KeywordDisplayText;        

        #endregion

        #region Audio stream infos

        public bool IsBuffering => CurrentState == PlayState.Opening || (AudioFile?.IsBuffering ?? false);

        public bool IsPlaying => CurrentState == PlayState.Playing;

        // Needed when underlying audio file has not yet been loaded
        int _volume = 255;

        public int Volume
        {
            get
            {
                if (AudioFile == null)
                    return _volume;
                else
                    return AudioFile.Volume;
            }
            set
            {
                if (_volume != value)
                {
                    if (AudioFile != null)
                        AudioFile.Volume = value;

                    _volume = value;

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The current position of playback
        /// </summary>
        public TimeSpan PlaybackPosition
        {
            get
            {
                if (AudioFile != null)
                    return AudioFile.Position;
                else
                    return TimeSpan.Zero;
            }
            set
            {
                if (AudioFile != null)
                {
                    if (!IsPlaying || Math.Abs(AudioFile.Position.TotalSeconds - value.TotalSeconds) > 2)
                    {
                        AudioFile.Position = value;
                        OnPropertyChanged();
                    }                    
                }
            }
        }

        /// <summary>
        /// String representation of the current playback position in format 0:00 / 0:00
        /// </summary>
        public string PlaybackPositionText
        {
            get
            {
                // keine offene Datei
                if (CurrentState == PlayState.Closed)
                    return "--:-- / --:--";

                TimeSpan pos = PlaybackPosition;
                TimeSpan? len = Length;

                if (!len.HasValue || len.Value.TotalSeconds < 0)
                {
                    return String.Format("{0:00}:{1:00}", pos.Minutes, pos.Seconds);
                }
                else
                {
                    if (len.Value.TotalHours >= 1)
                    {
                        return String.Format("{0:00}:{1:00}:{2:00} / {3:00}:{4:00}:{5:00}", pos.Hours, pos.Minutes, pos.Seconds, len.Value.Hours, len.Value.Minutes, len.Value.Seconds);
                    } else
                    {
                        return String.Format("{0:00}:{1:00} / {2:00}:{3:00}", pos.Minutes, pos.Seconds, len.Value.Minutes, len.Value.Seconds);
                    }
                }                    
            }
        }

        /// <summary>
        /// Return the buffer progress of the audio file (if it is buffering)
        /// </summary>
        public double BufferProgress => AudioFile?.BufferProgress ?? 0.0;

        /// <summary>
        /// The length of the audio stream
        /// If null the length is unknown
        /// </summary>
        public TimeSpan? Length => AudioFile?.Length ?? Model.Length;

        public PlayState CurrentState => AudioFile?.CurrentState ?? PlayState.Closed;

        #endregion

        public bool PlayedAlready
        {
            get
            {
                return Model.PlayedAlready;
            }
            set
            {
                Model.PlayedAlready = value;
                OnPropertyChanged();
            }
        }

        public bool IsBlocked
        {
            get
            {
                return Model.IsBlocked;
            }
            set
            {
                Model.IsBlocked = value;
                OnPropertyChanged();
            }
        }

        private Uri? cachedCover = null;

        public Uri? Cover
        {
            get
            {
                // lazy load the image
                if (!fetchedCover && !isGettingAlbumArt)
                    UpdateAlbumArt();

                return cachedCover;
            }
        }
        #endregion

        #region Commands

        public ICommand PlayPauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }

        #endregion

        #region Constructor

        public SongViewModel()
        {
            // only for Avalonia Designer
        }

        public SongViewModel(Song model, ILibraryProvider libraryProvider, IAlbumArtProvider albumArtProvider)
        {
            Model = model;

            if (Model == null)
                Model = new Song();


            AlbumArtProvider = albumArtProvider;
            LibraryProvider = libraryProvider;

            // commands
            PlayPauseCommand = new RelayCommand(PlayPauseCommand_Execute, PlayPauseCommand_CanExecute);
            StopCommand = new RelayCommand(StopCommand_Execute, StopCommand_CanExecute);
        }
        #endregion        

        #region Audio Methods

        /// <summary>
        /// Prepare this file to be played (asynchronously)
        /// </summary>
        public void PrepareAsync()
        {
            if (AudioFile == null && LibraryProvider != null)
            {
                // preload the audio file
                AudioFile = LibraryProvider.RetrieveAudioStream(Model);

                if (AudioFile != null)
                {
                    AudioFile.PlayStateChanged += AudioFile_PlayStateChanged;
                    AudioFile.PositionChanged += AudioFile_PositionChanged;
                    AudioFile.LengthChanged += AudioFile_LengthChanged;
                    AudioFile.BufferStateChanged += AudioFile_BufferStateChanged;

                    // update the volume
                    AudioFile.Volume = Volume;

                    AudioFile.LoadAsync();
                }
            }
        }

        void AudioFile_PlayStateChanged(object sender, EventArgs e)
        {
            if (AudioFile != null && CurrentState == PlayState.ReadyToPlay)
            {
                // write the actual length to the library
                if (AudioFile.Length.HasValue)
                    Model.Length = AudioFile.Length.Value;

                // now the actual audio file length should be available
                OnPropertyChanged(nameof(Length));

                // update commands
                // CommandManager.InvalidateRequerySuggested();
            }

            OnPropertyChanged(nameof(IsBuffering));
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(CurrentState));
        }

        void AudioFile_PositionChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(PlaybackPosition));
            OnPropertyChanged(nameof(PlaybackPositionText));
        }

        void AudioFile_LengthChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Length));
            OnPropertyChanged(nameof(PlaybackPositionText));
        }

        void AudioFile_BufferStateChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsBuffering));
            OnPropertyChanged(nameof(BufferProgress));
        }

        #endregion

        #region Cover art

        bool isGettingAlbumArt = false;
        bool fetchedCover = false;

        async void UpdateAlbumArt()
        {
            if (AlbumArtProvider == null)
            {
                fetchedCover = true;
                return;
            }                

            isGettingAlbumArt = true;
            
            cachedCover = await AlbumArtProvider.GetAlbumArtAsync(Model);
            fetchedCover = true;
            OnPropertyChanged(nameof(Cover));

            isGettingAlbumArt = false;
        }

        #endregion

        #region Model

        public void ReplaceModel(Song model)
        {
            Model = model;
            fetchedCover = false;
            cachedCover = null;

            // update all bindings
            OnPropertyChanged("");
        }

        #endregion

        #region Command implementations

        #region PlayPauseCommand

        protected bool PlayPauseCommand_CanExecute()
        {
            return CurrentState != PlayState.Closed;
        }

        protected void PlayPauseCommand_Execute()
        {
            switch (CurrentState)
            {
                case PlayState.Playing:
                    AudioFile?.FadeOut(1000);

                    Thread t = new Thread(() =>
                    {
                        Thread.Sleep(1000);
                        AudioFile?.Pause();
                    });
                    t.Start();                                
                    break;

                case PlayState.ReadyToPlay:
                case PlayState.Paused:
                    AudioFile?.Play();
                    AudioFile?.FadeIn(1000);
                    break;
            }
        }

        #endregion

        #region StopCommand

        protected bool StopCommand_CanExecute()
        {
            return CurrentState != PlayState.Closed;
        }

        protected void StopCommand_Execute()
        {
            AudioFile?.Stop();
        }

        #endregion

        #endregion

        #region IDisposible

        /// <summary>
        /// Releases the currently loaded audio file (if any)
        /// </summary>
        public void Close()
        {
            if (AudioFile != null)
            {
                AudioFile.PlayStateChanged -= AudioFile_PlayStateChanged;
                AudioFile.PositionChanged -= AudioFile_PositionChanged;
                AudioFile.LengthChanged -= AudioFile_LengthChanged;
                AudioFile.BufferStateChanged -= AudioFile_BufferStateChanged;

                AudioFile.Close();
                AudioFile = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
        #endregion
    }
}
