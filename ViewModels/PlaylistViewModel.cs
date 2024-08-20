using CommunityToolkit.Mvvm.Input;
using RTJuke.Core.Audio;
using RTJuke.Core.Plugins.Communication.CoreMessages;
using RTJuke.Core.Plugins.Communication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Linq;

namespace RTJuke.UICore.ViewModels
{
    /// <summary>
    /// Represents a Playlist
    /// </summary>
    public class PlaylistViewModel : ViewModelBase
    {
        #region Services
        IMessageBus MessageBus { get; set; }
        #endregion

        Func<SongViewModel> randomSongFunc;

        SongViewModel _comingUp;

        /// <summary>
        /// Der nächste zu spielende Song
        /// </summary>
        public SongViewModel ComingUp
        {
            get
            {
                return _comingUp;
            }
            private set
            {
                if (value != _comingUp)
                {
                    _comingUp = value;

                    if (_comingUp != null && _comingUp.CurrentState == PlayState.Closed)
                        _comingUp.PrepareAsync();

                    MessageBus.PublishAsync(new PlaylistChangedMessage());
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<SongViewModel> Titles { get; } = new ObservableCollection<SongViewModel>();

        public int Count => Titles.Count;

        /// <summary>
        /// Gibt die Länge der Playlist als Text zurück
        /// </summary>
        public String PlaylistLengthText
        {
            get
            {
                double listLen = Titles.Where(x => x.Length.HasValue && x.Length.Value.TotalSeconds > 0).Sum(x => Math.Truncate(x.Length.Value.TotalSeconds));
                TimeSpan ts = TimeSpan.FromSeconds(listLen);

                if (ts.TotalHours < 1)
                {
                    return String.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
                }
                else
                {
                    return String.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
                }
            }
        }

        #region Commands               

        public ICommand SwapComingUpCommand { get; }
        public ICommand BlockComingUpCommand { get; }
        public ICommand RemoveComingUpCommand { get; }
        public ICommand SwapPlaylistItemCommand { get; }
        public ICommand RemovePlaylistItemCommand { get; }

        public ICommand MovePlaylistItemUpCommand { get; }
        public ICommand MovePlaylistItemDownCommand { get; }

        public ICommand ClearPlaylistCommand { get; }

        #endregion

        public PlaylistViewModel(IMessageBus messageBus, Func<SongViewModel> randomSongFunc)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            Titles.CollectionChanged += Playlist_CollectionChanged;

            this.randomSongFunc = randomSongFunc;

            // init commands                        

            RemoveComingUpCommand = new RelayCommand(RemoveComingUpCommand_Execute, RemoveComingUpCommand_CanExecute);
            SwapComingUpCommand = new RelayCommand(SwapComingUpCommand_Execute, SwapComingUpCommand_CanExecute);
            BlockComingUpCommand = new RelayCommand(BlockComingUpCommand_Execute, BlockComingUpCommand_CanExecute);

            SwapPlaylistItemCommand = new RelayCommand<SongViewModel>(SwapPlaylistItemCommand_Execute, SwapPlaylistItemCommand_CanExecute);
            RemovePlaylistItemCommand = new RelayCommand<SongViewModel>(RemovePlaylistItemCommand_Execute, RemovePlaylistItemCommand_CanExecute);

            MovePlaylistItemUpCommand = new RelayCommand<SongViewModel>(MovePlaylistItemUpCommand_Execute, MovePlaylistItemUpCommand_CanExecute);
            MovePlaylistItemDownCommand = new RelayCommand<SongViewModel>(MovePlaylistItemDownCommand_Execute, MovePlaylistItemDownCommand_CanExecute);

            ClearPlaylistCommand = new RelayCommand(() => Clear(), () => Titles.Count > 0);
        }

        /// <summary>
        /// Replaces comingup with the given song and puts the current coming up title to the top of the playlist
        /// </summary>
        /// <param name="song"></param>
        public void PushComingUp(SongViewModel song)
        {
            var oldComingUp = ComingUp;
            ComingUp = song;

            if (oldComingUp != null)
            {
                oldComingUp.Close();
                Titles.Insert(0, oldComingUp);
            }
        }

        // <summary>
        /// Replaces comingup with the given song and discards the current coming up title
        /// </summary>
        /// <param name="song"></param>
        public void ReplaceComingUp(SongViewModel song, bool closePrevious = true)
        {
            var oldComingUp = ComingUp;
            ComingUp = song;

            if (oldComingUp != null && closePrevious)
            {
                oldComingUp.Close();
            }
        }

        /// <summary>
        /// Returns the top title in the list and removes it from the playlist
        /// </summary>
        /// <returns></returns>

        public SongViewModel TakeNext()
        {
            var svm = Titles.FirstOrDefault();
            if (svm != null)
            {
                Titles.RemoveAt(0);
            }
            return svm;
        }

        public void Add(SongViewModel song)
        {
            Titles.Add(song);
        }

        void Playlist_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(PlaylistLengthText));

            MessageBus.PublishAsync(new PlaylistChangedMessage());
        }

        public void Clear()
        {
            Titles.Clear();
        }

        #region Command implementations

        #region SwapComingUpCommand

        protected bool SwapComingUpCommand_CanExecute()
        {
            return ComingUp != null;
        }

        protected void SwapComingUpCommand_Execute()
        {
            var oldComingUp = ComingUp;

            // anderes Lied gleicher Kategorie
            SongViewModel s = randomSongFunc();
            if (s != null)
            {
                ComingUp = s;
                oldComingUp.AudioFile.Close();
            }
        }

        #endregion

        #region BlockComingUpCommand

        protected bool BlockComingUpCommand_CanExecute()
        {
            return ComingUp != null;
        }

        protected void BlockComingUpCommand_Execute()
        {
            ComingUp.IsBlocked = true;
            RemoveComingUpCommand.Execute(null);
        }

        #endregion

        #region RemoveComingUpCommand

        protected bool RemoveComingUpCommand_CanExecute()
        {
            return ComingUp != null;
        }

        protected void RemoveComingUpCommand_Execute()
        {
            var oldComingUp = ComingUp;

            try
            {
                var s = TakeNext();
                if (s == null)
                {
                    var nextSong = randomSongFunc();
                    if (nextSong != null)
                    {
                        ComingUp = nextSong;
                        return;
                    }
                }

                // pull first title of the playlist
                ComingUp = s;
            }
            finally
            {
                oldComingUp?.Close();
            }
        }

        #endregion

        #region SwapPlaylistItemCommand

        protected bool SwapPlaylistItemCommand_CanExecute(SongViewModel item)
        {
            return item != null;
        }

        protected void SwapPlaylistItemCommand_Execute(SongViewModel item)
        {
            // TODO
            /*
            Song s = RandomSong();
            if (s != null)
            {
                item.ReplaceModel(s);

                // Playlist-Länge aktualisieren
                OnPropertyChanged("PlaylistLengthText");

                MessageBus.PublishAsync(new PlaylistChangedMessage());
            }*/
        }

        #endregion       

        #region RemovePlaylistItemCommand

        protected bool RemovePlaylistItemCommand_CanExecute(SongViewModel item)
        {
            return item != null;
        }

        protected void RemovePlaylistItemCommand_Execute(SongViewModel item)
        {
            Titles.Remove(item);
        }

        #endregion

        #region MovePlaylistItemUpCommand

        protected bool MovePlaylistItemUpCommand_CanExecute(SongViewModel item)
        {
            return item != null;
        }

        protected void MovePlaylistItemUpCommand_Execute(SongViewModel item)
        {
            int oldIndex = Titles.IndexOf(item);

            if (oldIndex > 0)
            {
                Titles.RemoveAt(oldIndex);
                Titles.Insert(oldIndex - 1, item);
            }
            else
                if (oldIndex == 0)
            {
                // this is the first item, swap it with the coming up song
                var oldCompingUp = ComingUp;
                ComingUp = item;
                Titles.RemoveAt(oldIndex);
                if (oldCompingUp != null)
                    Titles.Insert(oldIndex, oldCompingUp);
            }
        }

        #endregion

        #region MovePlaylistItemDownCommand

        protected bool MovePlaylistItemDownCommand_CanExecute(SongViewModel item)
        {
            return item != null;
        }

        protected void MovePlaylistItemDownCommand_Execute(SongViewModel item)
        {
            int oldIndex = Titles.IndexOf(item);

            if (oldIndex < Titles.Count - 1)
            {
                Titles.RemoveAt(oldIndex);
                Titles.Insert(oldIndex + 1, item);
            }
            else
                if (oldIndex == Titles.Count - 1)
            {
                // this is the last item move it to the top of the list
                Titles.RemoveAt(oldIndex);
                Titles.Insert(0, item);
            }
        }

        #endregion

        #endregion
    }
}
