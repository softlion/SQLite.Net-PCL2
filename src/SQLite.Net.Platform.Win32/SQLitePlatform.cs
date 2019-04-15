using SQLite.Net.Interop;

namespace SQLite.Net.Platform.Win32
{
    public class SQLitePlatformWin32 : ISQLitePlatform
    {
        public SQLitePlatformWin32(string nativeInteropSearchPath = null)
        {
            SQLiteApi = new SQLiteApiWin32(nativeInteropSearchPath);
        }

        public ISQLiteApi SQLiteApi { get; private set; }
    }
}