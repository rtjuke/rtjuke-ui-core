using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace RTJuke.UICore.Utils
{
    public class TaskPool : INotifyPropertyChanged
    {

        int taskCount = 0;

        public int TaskCount
        {
            get
            {
                return taskCount;
            }
            private set
            {
                if (taskCount != value)
                {
                    taskCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsBusy");
                }
            }
        }

        public bool IsBusy
        {
            get
            {
                return TaskCount > 0;
            }
        }

        private async void EnqeueAsync<T>(Func<Task<T>> action, Action<T> onCompleted, Action<Exception> onFailed)
        {
            try
            {
                TaskCount++;

                var result = await action();

                if (onCompleted != null)
                    onCompleted(result);
            }
            catch (Exception e)
            {
                if (onFailed != null)
                    onFailed(e);
            }
            finally
            {
                TaskCount--;

                RaiseTaskCompleted();
            }
        }

        public void Enqeue<T>(Func<Task<T>> action, Action<T> onCompleted, Action<Exception> onFailed)
        {
            EnqeueAsync(action, onCompleted, onFailed);
        }

        public void Enqeue<T>(Func<Task<T>> action, Action<T> onCompleted)
        {
            EnqeueAsync(action, onCompleted, null);
        }

        public void Enqeue<T>(Func<Task<T>> action)
        {
            EnqeueAsync(action, null, null);
        }

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var pc = PropertyChanged;
            if (pc != null)
            {
                pc(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event EventHandler OnTaskCompleted;

        protected void RaiseTaskCompleted()
        {
            var tc = OnTaskCompleted;
            if (tc != null)
                tc(this, EventArgs.Empty);
        }

        #endregion

        public static async Task RunWithMaxDegreeOfConcurrency<T>(
         int maxDegreeOfConcurrency, CancellationToken cancelToken, IEnumerable<T> collection, Func<T, Task> taskFactory)
        {
            var activeTasks = new List<Task>(maxDegreeOfConcurrency);
            foreach (var task in collection.Select(taskFactory))
            {
                activeTasks.Add(task);
                if (activeTasks.Count == maxDegreeOfConcurrency)
                {
                    await Task.WhenAny(activeTasks.ToArray());
                    //observe exceptions here
                    activeTasks.RemoveAll(t => t.IsCompleted);
                }

                cancelToken.ThrowIfCancellationRequested();
            }
            await Task.WhenAll(activeTasks.ToArray()).ContinueWith(t =>
            {
                //observe exceptions in a manner consistent with the above   
            });
        }
    }
}
