using System.Diagnostics;
using SQLite.Net.Interop;

namespace SQLite.Net.Platform.NetCore
{
    public class StopwatchFactory : IStopwatchFactory
    {
        public IStopwatch Create()
        {
            return new StopwatchWin32();
        }

        private class StopwatchWin32 : IStopwatch
        {
            private readonly Stopwatch _stopWatch;

            public StopwatchWin32()
            {
                _stopWatch = new Stopwatch();
            }

            public void Stop()
            {
                _stopWatch.Stop();
            }

            public void Reset()
            {
                _stopWatch.Reset();
            }

            public void Start()
            {
                _stopWatch.Start();
            }

            public long ElapsedMilliseconds
            {
                get { return _stopWatch.ElapsedMilliseconds; }
            }
        }
    }
}