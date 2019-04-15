using SQLite.Net.Interop;

namespace SQLite.Net.Platform.Generic
{
    public class SQLitePlatformGeneric : ISQLitePlatform
    {
        public SQLitePlatformGeneric()
        {
            SQLiteApi = new SQLiteApi();
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}
