using System.Globalization;

namespace SQLite.Net2
{
    public static class TraceListenerExtensions
    {
        public static void WriteLine(this ITraceListener traceListener, string format, params object[] arg1)
        {
            traceListener?.Receive(string.Format(CultureInfo.InvariantCulture, format, arg1));
        }
    }
}