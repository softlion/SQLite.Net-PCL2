namespace SQLite.Net2.Tests.Deps
{
    public class Model<TKey>
    {
        [PrimaryKey]
        public TKey Key { get; set; }
    }
}
