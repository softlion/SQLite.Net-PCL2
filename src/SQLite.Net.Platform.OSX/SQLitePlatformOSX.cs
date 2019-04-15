using SQLite.Net.Interop;

namespace SQLite.Net.Platform.OSX
{
    public class SQLitePlatformOSX : ISQLitePlatform
    {
        public SQLitePlatformOSX()
        {
            SQLiteApi = new SQLiteApi();
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}
