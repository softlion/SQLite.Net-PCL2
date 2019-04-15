using SQLite.Net.Interop;

namespace SQLite.Net.Platform.XamarinAndroid
{
    public class SQLitePlatformAndroid : ISQLitePlatform
    {
        public SQLitePlatformAndroid()
        {
            SQLiteApi = new SQLiteApiAndroid();
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}