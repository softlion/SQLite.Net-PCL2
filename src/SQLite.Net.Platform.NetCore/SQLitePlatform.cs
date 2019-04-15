using SQLite.Net.Interop;

namespace SQLite.Net.Platform.NetCore
{
    public class SQLitePlatform : ISQLitePlatform
    {
        public SQLitePlatform()
        {
            SQLiteApi = new SQLiteApi();
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}