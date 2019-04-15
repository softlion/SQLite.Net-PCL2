using SQLite.Net.Interop;

namespace SQLite.Net.Platform.XamarinIOS
{
    public class SQLitePlatformIOS : ISQLitePlatform
    {
        public SQLitePlatformIOS()
        {
            SQLiteApi = new SQLiteApiIOS();
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}