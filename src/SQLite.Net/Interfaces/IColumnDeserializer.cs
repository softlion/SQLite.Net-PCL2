namespace SQLite.Net2
{
    /// <summary>
    /// If an object implements this interface then the <see cref="Deserialize"/> method will be called instead of the
    /// automatic population of fields and properties through TableMapping.
    /// </summary>
    public interface IColumnDeserializer
    {
        /// <summary>
        /// Called when an object should be populated with values from the passed reader.
        /// </summary>
        void Deserialize(IColumnReader reader);
    }
}
