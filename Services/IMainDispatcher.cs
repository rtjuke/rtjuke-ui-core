using System;
using System.Collections.Generic;
using System.Text;

namespace RTJuke.UICore.Services
{
    public interface IMainDispatcher
    {
        public void Invoke(Action action);

        public void BeginInvoke(Action action);

        public IDispatcherTimer GetTimer(TimeSpan interval, Action callback);
    }

    public interface IDispatcherTimer : IDisposable
    {
        
    }
}
