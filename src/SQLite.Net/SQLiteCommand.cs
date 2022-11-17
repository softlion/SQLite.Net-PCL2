//
// Copyright (c) 2012 Krueger Systems, Inc.
// Copyright (c) 2013 Oystein Krog (oystein.krog@gmail.com)
// Copyright (c) 2014 Benjamin Mayrargue
//   - support for new types: XElement, TimeSpan
//   - ExecuteSimpleQuery
//   - ExecuteDeferredQuery: support primitive types in T
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace SQLite.Net2
{
    public class SQLiteCommand
    {
        private static readonly IntPtr NegativePointer = new IntPtr(-1);

        private readonly List<Binding> _bindings;

        private readonly SQLiteConnection _conn;
        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        readonly SqliteApi sqlite = SqliteApi.Instance;

        internal SQLiteCommand(SQLiteConnection conn)
        {
            _conn = conn;
            _bindings = new List<Binding>();
            CommandText = "";
        }


        public string CommandText { get; set; }


        public int ExecuteNonQuery()
        {
            _conn.TraceListener.WriteLine("Executing: {0}", this);

            var stmt = Prepare();
            try
            {
                var r = sqlite.Step(stmt);
                switch (r)
                {
                    case Result.Done or Result.Row:
                    {
                        var rowsAffected = sqlite.Changes(_conn.Handle);
                        return rowsAffected;
                    }
                    case Result.Error:
                    {
                        var msg = sqlite.Errmsg16(_conn.Handle);
                        throw new SQLiteException(r, msg);
                    }
                    case Result.Constraint when sqlite.ExtendedErrCode(_conn.Handle) == ExtendedResult.ConstraintNotNull:
                        throw new NotNullConstraintViolationException(r, sqlite.Errmsg16(_conn.Handle));
                    default:
                        throw new SQLiteException(r, r.ToString());
                }
            }
            finally
            {
                Finalize(stmt);
            }
        }


        public IEnumerable<T> ExecuteDeferredQuery<T>()
        {
            return ExecuteDeferredQuery<T>(_conn.GetMapping(typeof (T)));
        }


        public List<T> ExecuteQuery<T>()
        {
            return ExecuteDeferredQuery<T>(_conn.GetMapping(typeof (T))).ToList();
        }


        public List<T> ExecuteQuery<T>(TableMapping map)
        {
            return ExecuteDeferredQuery<T>(map).ToList();
        }

        /// <summary>
        ///     Invoked every time an instance is loaded from the database.
        /// </summary>
        /// <param name='obj'>
        ///     The newly created object.
        /// </param>
        /// <remarks>
        ///     This can be overridden in combination with the <see cref="SQLiteConnection.NewCommand" />
        ///     method to hook into the life-cycle of objects.
        ///     Type safety is not possible because MonoTouch does not support virtual generic methods.
        /// </remarks>

        protected virtual void OnInstanceCreated(object obj)
        {
            // Can be overridden.
        }


        public IEnumerable<T> ExecuteSimpleQuery<T>()
        {
            _conn.TraceListener.WriteLine("Executing simple query: {0}", this);
 
            var stmt = Prepare();
            try
            {
                while (sqlite.Step(stmt) == Result.Row)
                {
                    var colType = sqlite.ColumnType(stmt, 0);
                    var val = ReadCol(stmt, 0, colType, typeof(T));
                    yield return (T)val;
                }
            }
            finally
            {
                sqlite.Finalize(stmt);
            }
        }


        public IEnumerable<T> ExecuteDeferredQuery<T>(TableMapping map)
        {
            _conn.TraceListener.WriteLine("Executing Query: {0}", this);

            var stmt = Prepare();
            try
            {
                var cols = new TableMapping.Column[sqlite.ColumnCount(stmt)];
                var type = typeof (T);
                var isPrimitiveType = type.GetTypeInfo().IsPrimitive || type == typeof (string);

                for (var i = 0; i < cols.Length; i++)
                {
                    if (!isPrimitiveType)
                    {
                        var name = sqlite.ColumnName16(stmt, i);
                        cols[i] = map.FindColumn(name);
                    }
                    else
                    {
                        cols[i] = map.CreateColumn(type);
                    }
                }

                while (sqlite.Step(stmt) == Result.Row)
                {
                    var obj = isPrimitiveType ? null : _conn.Resolver.CreateObject(map.MappedType);
                    if (obj is IColumnDeserializer serializer)
                    {
                        serializer.Deserialize(new SqliteColumnReader(sqlite, stmt, cols));
                    }
                    else
                    {
                        for (var i = 0; i < cols.Length; i++)
                        {
                            ColType colType;
                            object val;

                            //Support of primitive types
                            if (isPrimitiveType)
                            {
                                //Assert(cols.Length == 1)
                                colType = sqlite.ColumnType(stmt, i);
                                val = ReadCol(stmt, i, colType, cols[i].ColumnType);
                                yield return (T)Convert.ChangeType(val, type, CultureInfo.CurrentCulture);
                                break;
                            }

                            if (cols[i] == null)
                            {
                                continue;
                            }

                            colType = sqlite.ColumnType(stmt, i);
                            val = ReadCol(stmt, i, colType, cols[i].ColumnType);
                            cols[i].SetValue(obj, val);
                        }
                    }

                    if (!isPrimitiveType)
                    {
                        OnInstanceCreated(obj);
                        yield return (T) obj;
                    }
                }
            }
            finally
            {
                sqlite.Finalize(stmt);
            }
        }


        public T ExecuteScalar<T>()
        {
            _conn.TraceListener.WriteLine("Executing Query: {0}", this);

            var val = default(T);

            var stmt = Prepare();

            try
            {
                var r = sqlite.Step(stmt);
                if (r == Result.Row)
                {
                    var colType = sqlite.ColumnType(stmt, 0);
                    if (colType != ColType.Null)
                    {
                        var clrType = Nullable.GetUnderlyingType(typeof (T)) ?? typeof (T);
                        val = (T) ReadCol(stmt, 0, colType, clrType);
                    }
                }
                else if (r == Result.Done)
                {
                }
                else
                {
                    throw new SQLiteException(r, sqlite.Errmsg16(_conn.Handle));
                }
            }
            finally
            {
                Finalize(stmt);
            }

            return val;
        }


        public void Bind(string name, object val)
        {
            _bindings.Add(new Binding
            {
                Name = name,
                Value = val
            });
        }


        public void Bind(object val)
        {
            Bind(null, val);
        }


        public override string ToString()
        {
            var parts = new string[1 + _bindings.Count];
            parts[0] = CommandText;
            var i = 1;
            foreach (var b in _bindings)
            {
                parts[i] = string.Format("  {0}: {1}", i - 1, b.Value);
                i++;
            }
            return string.Join(Environment.NewLine, parts);
        }

        private IDbStatement Prepare()
        {
            try
            {
                var stmt = sqlite.Prepare2(_conn.Handle, CommandText);
                BindAll(stmt);
                return stmt;
            }
            catch (Exception e)
            {
                throw new SQLiteException(Result.Error, $"Sqlite prepare failed for sql: {CommandText}", e);
            }
        }

        private void Finalize(IDbStatement stmt)
        {
            sqlite.Finalize(stmt);
        }

        private void BindAll(IDbStatement stmt)
        {
            var nextIdx = 1;
            foreach (var b in _bindings)
            {
                if (b.Name != null)
                {
                    b.Index = sqlite.BindParameterIndex(stmt, b.Name);
                }
                else
                {
                    b.Index = nextIdx++;
                }

                BindParameter(sqlite, stmt, b.Index, b.Value, _conn.StoreDateTimeAsTicks, _conn.Serializer);
            }
        }

        internal static void BindParameter(ISQLiteApi isqLite3Api, IDbStatement stmt, int index, object value, bool storeDateTimeAsTicks, IBlobSerializer serializer)
        {
            if (value == null)
            {
                isqLite3Api.BindNull(stmt, index);
            }
            else
            {
                if (value is int)
                {
                    isqLite3Api.BindInt(stmt, index, (int) value);
                }
                else if (value is ISerializable<int>)
                {
                    isqLite3Api.BindInt(stmt, index, ((ISerializable<int>) value).Serialize());
                }
                else if (value is string)
                {
                    isqLite3Api.BindText16(stmt, index, (string) value, -1, NegativePointer);
                }
                else if (value is ISerializable<string>)
                {
                    isqLite3Api.BindText16(stmt, index, ((ISerializable<string>) value).Serialize(), -1, NegativePointer);
                }
                else if (value is XElement)
                {
                    isqLite3Api.BindText16(stmt, index, (value as XElement).ToString(SaveOptions.DisableFormatting), -1, NegativePointer);
                }
                else if (value is byte || value is ushort || value is sbyte || value is short)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(value));
                }
                else if (value is ISerializable<byte>)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(((ISerializable<byte>) value).Serialize()));
                }
                else if (value is ISerializable<ushort>)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(((ISerializable<ushort>) value).Serialize()));
                }
                else if (value is ISerializable<sbyte>)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(((ISerializable<sbyte>) value).Serialize()));
                }
                else if (value is ISerializable<short>)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(((ISerializable<short>) value).Serialize()));
                }
                else if (value is bool)
                {
                    isqLite3Api.BindInt(stmt, index, (bool) value ? 1 : 0);
                }
                else if (value is ISerializable<bool>)
                {
                    isqLite3Api.BindInt(stmt, index, ((ISerializable<bool>) value).Serialize() ? 1 : 0);
                }
                else if (value is uint || value is long)
                {
                    isqLite3Api.BindInt64(stmt, index, Convert.ToInt64(value));
                }
                else if (value is ISerializable<uint>)
                {
                    isqLite3Api.BindInt64(stmt, index, Convert.ToInt64(((ISerializable<uint>) value).Serialize()));
                }
                else if (value is ISerializable<long>)
                {
                    isqLite3Api.BindInt64(stmt, index, Convert.ToInt64(((ISerializable<long>) value).Serialize()));
                }
                else if (value is float || value is double || value is decimal)
                {
                    isqLite3Api.BindDouble(stmt, index, Convert.ToDouble(value));
                }
                else if (value is ISerializable<float>)
                {
                    isqLite3Api.BindDouble(stmt, index, Convert.ToDouble(((ISerializable<float>) value).Serialize()));
                }
                else if (value is ISerializable<double>)
                {
                    isqLite3Api.BindDouble(stmt, index, Convert.ToDouble(((ISerializable<double>) value).Serialize()));
                }
                else if (value is ISerializable<decimal>)
                {
                    isqLite3Api.BindDouble(stmt, index, Convert.ToDouble(((ISerializable<decimal>) value).Serialize()));
                }
                else if (value is TimeSpan)
                {
                    isqLite3Api.BindInt64(stmt, index, ((TimeSpan) value).Ticks);
                }
                else if (value is ISerializable<TimeSpan>)
                {
                    isqLite3Api.BindInt64(stmt, index, ((ISerializable<TimeSpan>) value).Serialize().Ticks);
                }
                else if (value is DateTime)
                {
                    if (storeDateTimeAsTicks)
                    {
                        long ticks = ((DateTime) value).ToUniversalTime().Ticks;
                        isqLite3Api.BindInt64(stmt, index, ticks);
                    }
                    else
                    {
                        string val = ((DateTime) value).ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                        isqLite3Api.BindText16(stmt, index, val, -1, NegativePointer);
                                      }
                }
                else if (value is DateTimeOffset)
                {
                    isqLite3Api.BindInt64(stmt, index, ((DateTimeOffset) value).UtcTicks);
                }
                else if (value is ISerializable<DateTime>)
                {
                    if (storeDateTimeAsTicks)
                    {
                        long ticks = ((ISerializable<DateTime>) value).Serialize().ToUniversalTime().Ticks;
                        isqLite3Api.BindInt64(stmt, index, ticks);
                    }
                    else
                    {
                        string val = ((ISerializable<DateTime>) value).Serialize().ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                        isqLite3Api.BindText16(stmt, index, val, -1, NegativePointer);
                    }
                }
                else if (value.GetType().GetTypeInfo().IsEnum)
                {
                    isqLite3Api.BindInt(stmt, index, Convert.ToInt32(value));
                }
                else if (value is byte[])
                {
                    isqLite3Api.BindBlob(stmt, index, (byte[]) value, ((byte[]) value).Length, NegativePointer);
                }
                else if (value is ISerializable<byte[]>)
                {
                    isqLite3Api.BindBlob(stmt, index, ((ISerializable<byte[]>) value).Serialize(), ((ISerializable<byte[]>) value).Serialize().Length,
                        NegativePointer);
                }
                else if (value is Guid)
                {
                    isqLite3Api.BindText16(stmt, index, ((Guid) value).ToString(), 72, NegativePointer);
                }
                else if (value is ISerializable<Guid>)
                {
                    isqLite3Api.BindText16(stmt, index, ((ISerializable<Guid>) value).Serialize().ToString(), 72, NegativePointer);
                }
                else if (serializer != null && serializer.CanDeserialize(value.GetType()))
                {
                    var bytes = serializer.Serialize(value);
                    isqLite3Api.BindBlob(stmt, index, bytes, bytes.Length, NegativePointer);
                }
                else
                {
                    throw new NotSupportedException("Cannot store type: " + value.GetType());
                }
            }
        }

        private object ReadCol(IDbStatement stmt, int index, ColType type, Type clrType)
        {
            var interfaces = clrType.GetTypeInfo().ImplementedInterfaces.ToList();

            if (type == ColType.Null)
            {
                return null;
            }
            if (clrType == typeof (string))
            {
                return sqlite.ColumnText16(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<string>)))
            {
                var value = sqlite.ColumnText16(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (int))
            {
                return sqlite.ColumnInt(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<int>)))
            {
                var value = sqlite.ColumnInt(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (bool))
            {
                return sqlite.ColumnInt(stmt, index) == 1;
            }
            if (interfaces.Contains(typeof (ISerializable<bool>)))
            {
                var value = sqlite.ColumnInt(stmt, index) == 1;
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (double))
            {
                return sqlite.ColumnDouble(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<double>)))
            {
                var value = sqlite.ColumnDouble(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (float))
            {
                return (float) sqlite.ColumnDouble(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<float>)))
            {
                var value = (float) sqlite.ColumnDouble(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (TimeSpan))
            {
                return new TimeSpan(sqlite.ColumnInt64(stmt, index));
            }
            if (interfaces.Contains(typeof (ISerializable<TimeSpan>)))
            {
                var value = new TimeSpan(sqlite.ColumnInt64(stmt, index));
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (DateTime))
            {
                if (_conn.StoreDateTimeAsTicks)
                {
                    return new DateTime(sqlite.ColumnInt64(stmt, index), DateTimeKind.Utc);
                }
                return DateTime.Parse(sqlite.ColumnText16(stmt, index), CultureInfo.InvariantCulture);
            }
            if (clrType == typeof (DateTimeOffset))
            {
                return new DateTimeOffset(sqlite.ColumnInt64(stmt, index), TimeSpan.Zero);
            }
            if (interfaces.Contains(typeof (ISerializable<DateTime>)))
            {
                DateTime value;
                if (_conn.StoreDateTimeAsTicks)
                {
                    value = new DateTime(sqlite.ColumnInt64(stmt, index), DateTimeKind.Utc);
                }
                else
                {
                    value = DateTime.Parse(sqlite.ColumnText16(stmt, index), CultureInfo.InvariantCulture);
                }
                return Activator.CreateInstance(clrType, value);
            }
            if (clrType.GetTypeInfo().IsEnum)
            {
                return sqlite.ColumnInt(stmt, index);
            }
            if (clrType == typeof (long))
            {
                return sqlite.ColumnInt64(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<long>)))
            {
                var value = sqlite.ColumnInt64(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (uint))
            {
                return (uint) sqlite.ColumnInt64(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<long>)))
            {
                var value = (uint) sqlite.ColumnInt64(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (decimal))
            {
                return (decimal) sqlite.ColumnDouble(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<decimal>)))
            {
                var value = (decimal) sqlite.ColumnDouble(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (byte))
            {
                return (byte) sqlite.ColumnInt(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<byte>)))
            {
                var value = (byte) sqlite.ColumnInt(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (ushort))
            {
                return (ushort) sqlite.ColumnInt(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<ushort>)))
            {
                var value = (ushort) sqlite.ColumnInt(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (short))
            {
                return (short) sqlite.ColumnInt(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<short>)))
            {
                var value = (short) sqlite.ColumnInt(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (sbyte))
            {
                return (sbyte) sqlite.ColumnInt(stmt, index);
            }
            if (interfaces.Contains(typeof (ISerializable<sbyte>)))
            {
                var value = (sbyte) sqlite.ColumnInt(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof (byte[]))
            {
                return sqlite.ColumnByteArray(stmt, index).ToArray();
            }
            if (interfaces.Contains(typeof (ISerializable<byte[]>)))
            {
                var value = sqlite.ColumnByteArray(stmt, index);
                return _conn.Resolver.CreateObject(clrType, new object[] {value.ToArray()});
            }
            if (clrType == typeof (Guid))
            {
                return new Guid(sqlite.ColumnText16(stmt, index));
            }
            if (interfaces.Contains(typeof (ISerializable<Guid>)))
            {
                var value = new Guid(sqlite.ColumnText16(stmt, index));
                return _conn.Resolver.CreateObject(clrType, new object[] {value});
            }
            if (clrType == typeof(XElement))
            {
                var text = sqlite.ColumnText16(stmt, index);
                return text==null ? null : XElement.Parse(text);
            }
            if (_conn.Serializer != null && _conn.Serializer.CanDeserialize(clrType))
            {
                var bytes = sqlite.ColumnByteArray(stmt, index);
                return _conn.Serializer.Deserialize(bytes, clrType);
            }
            throw new NotSupportedException("Don't know how to read " + clrType);
        }

        private class SqliteColumnReader : IColumnReader
        {
            private IDbStatement _stmt;
            private SqliteApi _sqlite;
                
            public SqliteColumnReader(SqliteApi sqlite, IDbStatement stmt, TableMapping.Column[] columns)
            {
                _sqlite = sqlite;
                _stmt = stmt;
                Columns = columns;
            }

            public TableMapping.Column[] Columns { get; }

            public string GetColumnName(int col) => _sqlite.ColumnName16(_stmt, col);
            
            public bool ReadBoolean(int col)
            {
                return _sqlite.ColumnInt(_stmt, col) == 1;
            }

            public byte ReadByte(int col)
            {
                return (byte)_sqlite.ColumnInt(_stmt, col);
            }

            public sbyte ReadSByte(int col)
            {
                return (sbyte)_sqlite.ColumnInt(_stmt, col);
            }

            public short ReadInt16(int col)
            {
                return (short)_sqlite.ColumnInt(_stmt, col);
            }

            public ushort ReadUInt16(int col)
            {
                return (ushort)_sqlite.ColumnInt(_stmt, col);
            }

            public int ReadInt32(int col)
            {
                return _sqlite.ColumnInt(_stmt, col);
            }

            public uint ReadUInt32(int col)
            {
                return (uint)_sqlite.ColumnInt(_stmt, col);
            }

            public long ReadInt64(int col)
            {
                return _sqlite.ColumnInt64(_stmt, col);
            }

            public ulong ReadUInt64(int col)
            {
                return (ulong)_sqlite.ColumnInt64(_stmt, col);
            }

            public float ReadSingle(int col)
            {
                return (float)_sqlite.ColumnDouble(_stmt, col);
            }

            public double ReadDouble(int col)
            {
                return _sqlite.ColumnDouble(_stmt, col);
            }

            public string ReadString(int col)
            {
                return _sqlite.ColumnText16(_stmt, col);
            }
        }
        
        private class Binding
        {
            public string Name { get; set; }

            public object Value { get; set; }

            public int Index { get; set; }
        }
    }
}
