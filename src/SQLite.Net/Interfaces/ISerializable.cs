
namespace SQLite.Net2
{
    public interface ISerializable<T>
    {
        T Serialize();
    }
}