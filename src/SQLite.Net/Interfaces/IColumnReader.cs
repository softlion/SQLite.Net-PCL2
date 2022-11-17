namespace SQLite.Net2
{
    /// <summary>
    /// Represents a single row of results from a SQL query. Used to read each column's value.
    /// </summary>
    public interface IColumnReader
    {
        /// <summary>
        /// Columns in this result.
        /// </summary>
        TableMapping.Column[] Columns { get; }

        /// <summary>
        /// Returns the name of the column at <see cref="col"/>.
        /// </summary>
        string GetColumnName(int col);
        
        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and checks if it equals 1.
        /// </summary>
        bool ReadBoolean(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as a byte.
        /// </summary>
        byte ReadByte(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as a sbyte.
        /// </summary>
        sbyte ReadSByte(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as a short.
        /// </summary>
        short ReadInt16(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as an ushort.
        /// </summary>
        ushort ReadUInt16(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as an int.
        /// </summary>
        int ReadInt32(int col);

        /// <summary>
        /// Reads the integer value of the column <see cref="col"/> and returns as an uint.
        /// </summary>
        uint ReadUInt32(int col);

        /// <summary>
        /// Reads the long value of the column <see cref="col"/> and returns as a long.
        /// </summary>
        long ReadInt64(int col);

        /// <summary>
        /// Reads the long value of the column <see cref="col"/> and returns as a ulong.
        /// </summary>
        ulong ReadUInt64(int col);

        /// <summary>
        /// Reads the double value of the column <see cref="col"/> and returns as a float.
        /// </summary>
        float ReadSingle(int col);
        
        /// <summary>
        /// Reads the double value of the column <see cref="col"/> and returns as a double.
        /// </summary>
        double ReadDouble(int col);

        /// <summary>
        /// Reads the string value of the column <see cref="col"/> and returns as a string.
        /// </summary>
        string ReadString(int col);
    }
}
