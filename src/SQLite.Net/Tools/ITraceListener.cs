
namespace SQLite.Net2
{
    public interface ITraceListener
    {
        void Receive(string message);
    }
}