//
// Copyright (c) 2014-2022 Benjamin Mayrargue (benjamin@vapolia.fr)
//   - Support for multi-columns primary keys (create table / get / find / delete)
//   - ExecuteSimpleQuery
//   - Fix disposing issues
//   - Remove locks
//   - Fix transaction issues
// Copyright (c) 2013 Øystein Krog (oystein.krog@gmail.com)
// Copyright (c) 2012 Krueger Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SQLite.Net2
{
    /// <summary>
    /// Represents an open connection to a SQLite database.
    /// </summary>
    public class SQLiteConnection : IDisposable
    {
        private static ConfigOption firstConfigOption;

        private readonly SqliteApi sqlite = SqliteApi.Instance;
        /// <summary>
        ///     Used to list some code that we want the MonoTouch linker
        ///     to see, but that we never want to actually execute.
        /// </summary>
#pragma warning disable 649
        private static bool _preserveDuringLinkMagic;
#pragma warning restore 649
        private readonly Random _rand = new ();
        private readonly ConcurrentDictionary<string, TableMapping> _tableMappings;
        private readonly ConcurrentDictionary<(string MappedTypeFullName, string Extra), PreparedSqlLiteInsertCommand> _insertCommandMap = new ();
        
        private IColumnInformationProvider? _columnInformationProvider;
        private TimeSpan _busyTimeout;
        private long _elapsedMilliseconds;
        private bool _open;
        private Stopwatch? _sw;
        private readonly SQLiteOpenFlags databaseOpenFlags;
        private readonly string encryptionKey;

        public IBlobSerializer? Serializer { get; }
        public string DatabasePath { get;}
        public bool StoreDateTimeAsTicks { get; }
        public IDictionary<Type, string> ExtraTypeMappings { get; }
        public IContractResolver Resolver { get;  }
        public IDbHandle? Handle { get; private set; }
        public bool TimeExecution { get; set; }
        public ITraceListener? TraceListener { get; set; }
        
        static SQLiteConnection()
        {
            if (_preserveDuringLinkMagic)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                var ti = new ColumnInfo();
                ti.Name = "magic";
            }
        }

        /// <summary>
        /// Create a new connection to the same database.
        /// </summary>
        /// <remarks>
        /// This support scenarios where a code needs to create a transaction, while leaving the current connection transactionless (ie: sharable with other codes), as a transaction creates a state in this object.
        ///
        /// This is used by *All() methods.
        ///
        /// Override it if you have not called the constructor with an encryption key and you database is encrypted.
        /// </remarks>
        public virtual SQLiteConnection Clone() 
            => new (DatabasePath, databaseOpenFlags, StoreDateTimeAsTicks, Serializer, _tableMappings, ExtraTypeMappings, Resolver, encryptionKey);

        /// <summary>
        /// Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
        /// </summary>
        /// <param name="databasePath">Specifies the path to the database file. </param>
        /// <param name="openFlags"></param>
        /// <param name="storeDateTimeAsTicks">
        /// Specifies whether to store DateTime properties as ticks (true) or strings (false). You
        /// absolutely do want to store them as Ticks in all new projects. The option to set false is
        /// only here for backwards compatibility. There is a *significant* speed advantage, with no
        /// down sides, when setting storeDateTimeAsTicks = true.
        /// </param>
        /// <param name="serializer">Blob serializer to use for storing undefined and complex data structures. If left null these types will thrown an exception as usual.</param>
        /// <param name="tableMappings">
        /// Existing table mapping that the connection can use. If its null, it creates the mappings,
        /// if and when required. The mappings are also created when an unknown type is used for the first time.
        /// </param>
        /// <param name="extraTypeMappings">Any extra type mappings that you wish to use for overriding the default for creating column definitions for SQLite DDL in the class Orm (snake in Swedish).</param>
        /// <param name="resolver">A contract resovler for resolving interfaces to concreate types during object creation</param>
        /// <param name="encryptionKey">When using SQL CIPHER, automatically sets the key (you won't need to override Clone() in this case)</param>
        /// <param name="configOption">Mode in which to open the db. Default to Serialized</param>
        /// <param name="busyTimeout">
        /// Sets a busy handler to sleep the specified amount of time when a table is locked.
        /// The handler will sleep multiple times until a total time of busyTimeout has accumulated.
        /// Default to 1s
        /// </param>
        public SQLiteConnection(string databasePath, SQLiteOpenFlags openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, bool storeDateTimeAsTicks = true,  IBlobSerializer? serializer = null,  IDictionary<string, TableMapping>? tableMappings = null,
             IDictionary<Type, string>? extraTypeMappings = null, IContractResolver? resolver = null, string? encryptionKey = null, ConfigOption configOption = ConfigOption.Serialized, TimeSpan? busyTimeout = null)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Must be specified", nameof(databasePath));
            DatabasePath = databasePath;

            if (firstConfigOption == ConfigOption.Unknown)
            {
                firstConfigOption = configOption;

                if (configOption > ConfigOption.SingleThread && sqlite.Threadsafe() == 0)
                    throw new ArgumentException("SQlite is not compiled with multithread and config option is set to multithread", nameof(configOption));
                
                sqlite.Config(configOption);
                sqlite.Initialize();
            }
            
            var r = sqlite.Open(DatabasePath, out var handle, (int) openFlags, null);
            if (r != Result.OK)
                throw new SQLiteException(r, $"Could not open database file: {DatabasePath} ({r})");

            Handle = handle ?? throw new NullReferenceException("Database handle is null");
            _open = true;
            databaseOpenFlags = openFlags;
            
            if (!string.IsNullOrWhiteSpace(encryptionKey))
            {
                this.encryptionKey = encryptionKey!;
                var cipherVer = ExecuteScalar<string>("PRAGMA cipher_version");
                if (string.IsNullOrWhiteSpace(cipherVer))
                    throw new Exception("This build is not using SQL CIPHER. See https://github.com/softlion/SQLite.Net-PCL2 for doc on how to setup SQL CIPHER.");

                var o = ExecuteScalar<string>($"PRAGMA key = '{encryptionKey}';");
                if(o != "ok")
                    throw new Exception("Invalid cipher key");
            }

            BusyTimeout = busyTimeout ?? TimeSpan.FromSeconds(1);
            Serializer = serializer;
            StoreDateTimeAsTicks = storeDateTimeAsTicks;
            ExtraTypeMappings = extraTypeMappings ?? new Dictionary<Type, string>();
            Resolver = resolver ?? ContractResolver.Current;
            _tableMappings = new (tableMappings ?? new Dictionary<string, TableMapping>());
        }

		public IColumnInformationProvider ColumnInformationProvider 
		{
			get => _columnInformationProvider;
            set
			{
				_columnInformationProvider = value;
				Orm.ColumnInformationProvider = _columnInformationProvider ?? new DefaultColumnInformationProvider ();
			}
		}



        /// <summary>
        /// Sets a busy handler to sleep the specified amount of time when a table is locked.
        /// The handler will sleep multiple times until a total time of <see cref="BusyTimeout" /> has accumulated.
        /// </summary>
        public TimeSpan BusyTimeout
        {
            get => _busyTimeout;
            set
            {
                _busyTimeout = value;
                sqlite.BusyTimeout(Handle, (int) _busyTimeout.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Returns the mappings from types to tables that the connection currently understands.
        /// </summary>
        public IEnumerable<TableMapping> TableMappings => _tableMappings.Values.ToList();

        /// <summary>
        /// Whether <see cref="BeginTransaction" /> has been called and the database is waiting for a <see cref="Commit" />.
        /// </summary>
        public bool IsInTransaction => sqlite.GetAutoCommit(Handle) == 0;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void EnableLoadExtension(int onoff)
        {
            var r = sqlite.EnableLoadExtension(Handle, onoff);
            if (r != Result.OK)
            {
                var msg = sqlite.Errmsg16(Handle);
                throw new SQLiteException(r, msg);
            }
        }

        /// <summary>
        ///     Retrieves the mapping that is automatically generated for the given type.
        /// </summary>
        /// <param name="type">
        ///     The type whose mapping to the database is returned.
        /// </param>
        /// <param name="createFlags">
        ///     Optional flags allowing implicit PK and indexes based on naming conventions
        /// </param>
        /// <returns>
        ///     The mapping represents the schema of the columns of the database and contains
        ///     methods to set and get properties of objects.
        /// </returns>
        public TableMapping GetMapping(Type type, CreateFlags createFlags = CreateFlags.None) 
            => _tableMappings.TryGetValue(type.FullName, out var map) ? map : CreateAndSetMapping(type, createFlags, _tableMappings);

        private TableMapping CreateAndSetMapping(Type type, CreateFlags createFlags, IDictionary<string, TableMapping> mapTable)
        {
            var props = ReflectionService.GetPublicInstanceProperties(type);
			var map = new TableMapping(type, props, createFlags, _columnInformationProvider);
            mapTable[type.FullName] = map;
            return map;
        }

        /// <summary>
        ///     Retrieves the mapping that is automatically generated for the given type.
        /// </summary>
        /// <returns>
        ///     The mapping represents the schema of the columns of the database and contains
        ///     methods to set and get properties of objects.
        /// </returns>
        public TableMapping GetMapping<T>() 
            => GetMapping(typeof (T));

        /// <summary>
        ///     Executes a "drop table" on the database.  This is non-recoverable.
        /// </summary>
        public int DropTable<T>() 
            => DropTable(typeof (T));

        /// <summary>
        ///     Executes a "drop table" on the database.  This is non-recoverable.
        /// </summary>
        public int DropTable(Type t)
        {
            var map = GetMapping(t);
            var query = string.Format("drop table if exists \"{0}\"", map.TableName);
            return Execute(query);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     The number of entries added to the database schema.
        /// </returns>
        public int CreateTable<T>(CreateFlags createFlags = CreateFlags.None) 
            => CreateTable(typeof (T), createFlags);

        /// <summary>
        ///     Executes a "create table if not exists" on the database. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <param name="ty">Type to reflect to a database table.</param>
        /// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
        /// <returns>
        ///     The number of entries added to the database schema.
        /// </returns>
        public int CreateTable(Type ty, CreateFlags createFlags = CreateFlags.None)
        {
            var map = GetMapping(ty, createFlags);
            var mapColumns = map.Columns;

            if (!mapColumns.Any())
                throw new Exception("Table has no (public) columns");

            // Facilitate virtual tables a.k.a. full-text search.
            var fts3 = (createFlags & CreateFlags.FullTextSearch3) != 0;
            var fts4 = (createFlags & CreateFlags.FullTextSearch4) != 0;
            var fts = fts3 || fts4;
            var @virtual = fts ? "virtual " : string.Empty;
            var @using = fts3 ? "using fts3 " : fts4 ? "using fts4 " : string.Empty;

            var sbQuery = new StringBuilder("create ").Append(@virtual).Append("table if not exists \"").Append(map.TableName).Append("\" ").Append(@using).Append("( \n");
            mapColumns.Aggregate(sbQuery, (sb, column) => sb.Append(Orm.SqlDecl(column, StoreDateTimeAsTicks, Serializer, ExtraTypeMappings)).Append(",\n"));

            var pks = (from c in mapColumns where c.IsPK || c.IsAutoInc select c).ToList(); //autoincrement must be a primary key
            var autoincs = pks.Count(p => p.IsAutoInc);
            if (autoincs > 1)
                throw new Exception("Can not have a multiple primary key with a single autoincrement");
            if (pks.Count != 0 && autoincs == 0)
            {
                //If autoincs == 1, a 'primary key' constraint has already been created
                //, PRIMARY KEY (A_ID, B_ID)
                sbQuery.Append("primary key (");
                pks.Aggregate(sbQuery, (sb, c) => sb.Append(c.Name).Append(','));
                sbQuery.Remove(sbQuery.Length - 1, 1).Append(')');
            }
            else
            {
                sbQuery.Remove(sbQuery.Length - 2, 2);
            }

            sbQuery.Append(")");
            var count = Execute(sbQuery.ToString());

            if (count == 0)
            {
                //Possible bug: This always seems to return 0?
                // Table already exists, migrate it
                MigrateTable(map);
            }

            var indexes = new Dictionary<string, IndexInfo>();
            foreach (var c in mapColumns)
            {
                foreach (var i in c.Indices)
                {
                    var iname = i.Name ?? map.TableName + "_" + c.Name;
                    IndexInfo iinfo;
                    if (!indexes.TryGetValue(iname, out iinfo))
                    {
                        iinfo = new IndexInfo
                        {
                            IndexName = iname,
                            TableName = map.TableName,
                            Unique = i.Unique,
                            Columns = new List<IndexedColumn>()
                        };
                        indexes.Add(iname, iinfo);
                    }

                    if (i.Unique != iinfo.Unique)
                    {
                        throw new Exception(
                            "All the columns in an index must have the same value for their Unique property");
                    }

                    iinfo.Columns.Add(new IndexedColumn
                    {
                        Order = i.Order,
                        ColumnName = c.Name
                    });
                }
            }

            foreach (var indexName in indexes.Keys)
            {
                var index = indexes[indexName];
                var columns = index.Columns.OrderBy(i => i.Order).Select(i => i.ColumnName);
                count += CreateIndex(indexName, index.TableName, columns, index.Unique);
            }

            return count;
        }

        /// <summary>
        /// Creates an index for the specified table and columns.
        /// </summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        public int CreateIndex(string indexName, string tableName, IEnumerable<string> columnNames, bool unique = false)
        {
            const string sqlFormat = "create {2} index if not exists \"{3}\" on \"{0}\"(\"{1}\")";
            var sql = string.Format(sqlFormat, tableName, string.Join("\", \"", columnNames), unique ? "unique" : "", indexName);
            return Execute(sql);
        }

        /// <summary>
        ///     Creates an index for the specified table and column.
        /// </summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        public int CreateIndex(string indexName, string tableName, string columnName, bool unique = false) 
            => CreateIndex(indexName, tableName, new[] {columnName}, unique);

        /// <summary>
        ///     Creates an index for the specified table and column.
        /// </summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        public int CreateIndex(string tableName, string columnName, bool unique = false) 
            => CreateIndex(string.Concat(tableName, "_", columnName.Replace("\",\"", "_")), tableName, new[] { columnName }, unique);

        /// <summary>
        ///     Creates an index for the specified table and columns.
        /// </summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        
        public int CreateIndex(string tableName, string[] columnNames, bool unique = false) 
            => CreateIndex(tableName + "_" + string.Join("_", columnNames), tableName, columnNames, unique);

        /// <summary>
        ///     Creates an index for the specified object property.
        ///     e.g. CreateIndex{Client}(c => c.Name);
        /// </summary>
        /// <typeparam name="T">Type to reflect to a database table.</typeparam>
        /// <param name="property">Property to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        public void CreateIndex<T>(Expression<Func<T, object>> property, bool unique = false)
        {
            MemberExpression mx;
            if (property.Body.NodeType == ExpressionType.Convert)
                mx = ((UnaryExpression) property.Body).Operand as MemberExpression;
            else
                mx = (property.Body as MemberExpression);

            if (mx == null)
                throw new ArgumentException("Expression is not supported", "property");
            
            var propertyInfo = mx.Member as PropertyInfo;
            if (propertyInfo == null)
                throw new ArgumentException("The lambda expression 'property' should point to a valid Property");

            var propName = propertyInfo.Name;

            var map = GetMapping<T>();
            var colName = map.FindColumnWithPropertyName(propName).Name;

            CreateIndex(map.TableName, colName, unique);
        }

        public List<ColumnInfo> GetTableInfo(string tableName) 
            => Query<ColumnInfo>($"pragma table_info(\"{tableName}\")");

        public void MigrateTable<T>() => MigrateTable(GetMapping(typeof(T)));
		public void MigrateTable(Type t) => MigrateTable(GetMapping(t));

        private void MigrateTable(TableMapping map)
        {
            var existingCols = GetTableInfo(map.TableName);
            var toBeAdded = new List<TableMapping.Column>();

            foreach (var p in map.Columns)
            {
                var found = false;
                foreach (var c in existingCols)
                {
                    found = (string.Compare(p.Name, c.Name, StringComparison.OrdinalIgnoreCase) == 0);
                    if (found)
                        break;
                }
                if (!found)
                    toBeAdded.Add(p);
            }

            foreach (var p in toBeAdded)
            {
                var addCol = $"alter table \"{map.TableName}\" add column {Orm.SqlDecl(p, StoreDateTimeAsTicks, Serializer, ExtraTypeMappings)}";
                Execute(addCol);
            }
        }

        /// <summary>
        /// Creates a new SQLiteCommand.
        /// </summary>
        /// <seealso cref="SQLiteCommand.OnInstanceCreated" />
        protected SQLiteCommand NewCommand() => new (this);

        /// <summary>
        /// Creates a new SQLiteCommand given the command text with arguments. Place a '?' in the command text for each of the arguments.
        /// </summary>
        /// <param name="cmdText">
        /// The fully escaped SQL.
        /// </param>
        /// <param name="args">
        /// Arguments to substitute for the occurences of '?' in the command text.
        /// </param>
        /// <returns>
        /// A <see cref="SQLiteCommand" />
        /// </returns>
        public SQLiteCommand CreateCommand(string cmdText, params object[] args)
        {
            if (!_open)
                throw new SQLiteException(Result.Error, "Cannot create commands from unopened database");

            var cmd = NewCommand();
            cmd.CommandText = cmdText;
            foreach (var o in args)
                cmd.Bind(o);
            return cmd;
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     Use this method instead of Query when you don't expect rows back. Such cases include
        ///     INSERTs, UPDATEs, and DELETEs.
        ///     You can set the Trace or TimeExecution properties of the connection
        ///     to profile execution.
        /// </summary>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The number of rows modified in the database as a result of this execution.
        /// </returns>
        public int Execute(string query, params object[] args)
        {
            var cmd = CreateCommand(query, args);

            if (TimeExecution)
            {
                _sw ??= new Stopwatch();
                _sw.Reset();
                _sw.Start();
            }

            var r = cmd.ExecuteNonQuery();

            if (TimeExecution)
            {
                _sw!.Stop();
                _elapsedMilliseconds += _sw.ElapsedMilliseconds;
                TraceListener.WriteLine("Finished in {0} ms ({1:0.0} s total)", _sw.ElapsedMilliseconds, _elapsedMilliseconds/1000.0);
            }

            return r;
        }

        public T ExecuteScalar<T>(string query, params object[] args)
        {
            var cmd = CreateCommand(query, args);

            if (TimeExecution)
            {
                _sw ??= new Stopwatch();
                _sw.Reset();
                _sw.Start();
            }

            var r = cmd.ExecuteScalar<T>();

            if (TimeExecution)
            {
                _sw!.Stop();
                _elapsedMilliseconds += _sw.ElapsedMilliseconds;
                TraceListener.WriteLine("Finished in {0} ms ({1:0.0} s total)", _sw.ElapsedMilliseconds, _elapsedMilliseconds/1000.0);
            }

            return r;
        }

        public IEnumerable<T> ExecuteSimpleQuery<T>(string query, params object[] args)
        {
            var cmd = CreateCommand(query, args);
            return cmd.ExecuteSimpleQuery<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        /// </returns>
        public List<T> Query<T>(string query, params object[] args) where T : class
        {
            var cmd = CreateCommand(query, args);
            return cmd.ExecuteQuery<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        public IEnumerable<T> DeferredQuery<T>(string query, params object[] args) where T : class
        {
            var cmd = CreateCommand(query, args);
            return cmd.ExecuteDeferredQuery<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="map">
        ///     A <see cref="TableMapping" /> to use to convert the resulting rows
        ///     into objects.
        /// </param>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        /// </returns>
        public List<object> Query(TableMapping map, string query, params object[] args)
        {
            var cmd = CreateCommand(query, args);
            return cmd.ExecuteQuery<object>(map);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="map">
        ///     A <see cref="TableMapping" /> to use to convert the resulting rows
        ///     into objects.
        /// </param>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        public IEnumerable<object> DeferredQuery(TableMapping map, string query, params object[] args)
        {
            var cmd = CreateCommand(query, args);
            return cmd.ExecuteDeferredQuery<object>(map);
        }

        /// <summary>
        ///     Returns a queryable interface to the table represented by the given type.
        /// </summary>
        /// <returns>
        ///     A queryable object that is able to translate Where, OrderBy, and Take
        ///     queries into native SQL.
        /// </returns>
        public TableQuery<T> Table<T>() where T : class => new (this);

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">
        ///     The primary key.
        /// </param>
        /// <returns>
        ///     The object with the given primary key. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public T Get<T>(object pk) where T : class
        {
            var map = GetMapping(typeof (T));
            return Query<T>(map.GetByPrimaryKeySql, pk).First();
        }

         /// <summary>
        ///     Attempts to retrieve an object with the given primary keys from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pks">
        ///     The primary keys.
        /// </param>
        /// <returns>
        ///     The object with the given primary key. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public T Get<T>(params object[] pks) where T : class
        {
            if(pks == null || pks.Length == 0)
                throw new ArgumentNullException(nameof(pks));

            var map = GetMapping(typeof(T));
            return Query<T>(map.GetByPrimaryKeysSqlForPartialKeys(pks.Count()), pks).First();
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">
        ///     A predicate for which object to find.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public T Get<T>(Expression<Func<T, bool>> predicate) where T : class 
            => Table<T>().Where(predicate).First();

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">
        ///     The primary key.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public T? Find<T>(object pk) where T : class
        {
            var map = GetMapping(typeof (T));
            return Query<T>(map.GetByPrimaryKeySql, pk).FirstOrDefault();
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pks">
        ///     The primary keys.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public T? Find<T>(params object[] pks) where T : class
        {
            if(pks == null || pks.Length == 0)
                throw new ArgumentNullException(nameof(pks));

            var map = GetMapping(typeof(T));
            return Query<T>(map.GetByPrimaryKeysSqlForPartialKeys(pks.Length), pks).FirstOrDefault();
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the query from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="query">
        ///     The fully escaped SQL.
        /// </param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        public T? FindWithQuery<T>(string query, params object[] args) where T : class 
            => Query<T>(query, args).FirstOrDefault();

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">
        ///     The primary key.
        /// </param>
        /// <param name="map">
        ///     The TableMapping used to identify the object type.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public object? Find(object pk, TableMapping map) 
            => Query(map, map.GetByPrimaryKeySql, pk).FirstOrDefault();

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pks">
        ///     The primary keys.
        /// </param>
        /// <param name="map">
        ///     The TableMapping used to identify the object type.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public object? Find(TableMapping map, params object[] pks)
        {
            if(pks == null || pks.Length == 0)
                throw new ArgumentNullException(nameof(pks));
            return Query(map, map.GetByPrimaryKeysSqlForPartialKeys(pks.Length), pks).FirstOrDefault();
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">
        ///     A predicate for which object to find.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        public T Find<T>(Expression<Func<T, bool>> predicate) where T : class 
            => Table<T>().Where(predicate).FirstOrDefault();

            
        #region transactions
        private int transactionDepth;
        
        /// <summary>
        /// Begins a new transaction. Call <see cref="Commit" /> to end the transaction.
        /// </summary>
        /// <example cref="System.InvalidOperationException">Throws if a transaction has already begun.</example>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void BeginTransaction()
        {
            
            // The BEGIN command only works if the transaction stack is empty, or in other words if there are no pending transactions. 
            // If the transaction stack is not empty when the BEGIN command is invoked, then the command fails with an error.
            // Rather than crash with an error, we will just ignore calls to BeginTransaction that would result in an error.
            if (!IsInTransaction && Interlocked.Exchange(ref transactionDepth, 1) == 0)
            {
                try
                {
                    Execute("begin transaction");
                }
                catch (SQLiteException sqlExp) when(sqlExp.Result is Result.IOError or Result.Full or Result.Busy or Result.NoMem or Result.Interrupt)
                {
                    //RollbackTo(null, true);
                    transactionDepth = 0;
                    throw;
                }
            }
            else
            {
                // Calling BeginTransaction on an already open transaction is invalid
                RollbackTo(null, true);
                throw new InvalidOperationException("Cannot begin a transaction while already in a transaction. Use SaveTransactionPoint instead.");
            }
        }


        /// <summary>
        /// Commits the transaction that was begun by <see cref="BeginTransaction" />.
        /// </summary>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void Commit()
        {
            if (IsInTransaction)
            {
                try
                {
                    Execute("commit");
                    transactionDepth = 0;
                }
                catch (Exception e)
                {
                    RollbackTo(null, true);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Creates a savepoint in the database at the current point in the transaction timeline.
        /// Begins a new transaction if one is not in progress.
        /// 
        /// Call <see cref="RollbackTo" /> to undo transactions since the returned savepoint.
        /// Call <see cref="Release" /> to commit transactions after the savepoint returned here.
        /// Call <see cref="Commit" /> to end the transaction, committing all changes.
        /// </summary>
        /// <returns>A string naming the savepoint.</returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// Not thread safe.
        /// </remarks>
        public string SaveTransactionPoint()
        {
            var depth = ++transactionDepth;
            var retVal = $"S{_rand.Next(short.MaxValue)}D{depth}";

            try
            {
                Execute($"savepoint {retVal}");
            }
            catch (Exception)
            {
                RollbackTo(null, true);
                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Rolls back the transaction that was begun by <see cref="BeginTransaction" /> or <see cref="SaveTransactionPoint" />.
        /// </summary>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void Rollback() => RollbackTo(null, false);

        /// <summary>
        /// Rolls back the savepoint created by <see cref="BeginTransaction" /> or SaveTransactionPoint.
        /// </summary>
        /// <param name="savepoint">
        /// The name of the savepoint to roll back to, as returned by <see cref="SaveTransactionPoint" />.
        /// If savepoint is null or empty, this method is equivalent to a call to <see cref="Rollback" />
        /// </param>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void RollbackTo(string savepoint) => RollbackTo(savepoint, false);

        /// <summary>
        /// Rolls back the transaction that was begun by <see cref="BeginTransaction" />.
        /// </summary>
        /// <param name="savepoint">the savepoint name/key</param>
        /// <param name="noThrow">true to avoid throwing exceptions, false otherwise</param>
        private void RollbackTo(string? savepoint, bool noThrow)
        {
            // Rolling back without a TO clause rolls backs all transactions
            // and leaves the transaction stack empty.   
            try
            {
                if (string.IsNullOrEmpty(savepoint))
                {
                    // No need to rollback if there are no transactions open.
                    try
                    {
                        if (IsInTransaction)
                            Execute("rollback");
                    }
                    finally
                    {
                        transactionDepth = 0;
                    }
                }
                else
                {
                    if (IsInTransaction)
                        DoSavePointExecute(savepoint!, "rollback to ", true);
                    else
                        transactionDepth = 0;
                }
            }
            catch (SQLiteException)
            {
                if (!noThrow)
                    throw;
            }
        }

        /// <summary>
        /// Releases a savepoint returned from <see cref="SaveTransactionPoint" />.  Releasing a savepoint
        /// makes changes since that savepoint permanent if the savepoint began the transaction,
        /// or otherwise the changes are permanent pending a call to <see cref="Commit" />.
        /// The RELEASE command is like a COMMIT for a SAVEPOINT.
        /// </summary>
        /// <param name="savepoint">
        /// The name of the savepoint to release.  The string should be the result of a call to <see cref="SaveTransactionPoint" />
        /// </param>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void Release(string savepoint)
            => DoSavePointExecute(savepoint, "release ", false);

        private void DoSavePointExecute(string savePoint, string cmd, bool isRollback)
        {
            // Validate the savepoint
            var firstLen = savePoint?.IndexOf('D') ?? 0;
            if (firstLen < 2 || savePoint!.Length <= firstLen + 1) 
                throw new ArgumentException($"savePoint '{savePoint}' is not valid, and should be the result of a call to SaveTransactionPoint.", nameof(savePoint));

            if (!int.TryParse(savePoint.Substring(firstLen + 1), out var depth) || depth < 0) 
                throw new ArgumentException($"savePoint '{savePoint}' is not valid, and should be the result of a call to SaveTransactionPoint.", nameof(savePoint));

            // if (depth == 1)
            // {
            //     if (isRollback)
            //     {
            //         RollbackTo(null, true);
            //     }
            //     return;
            // }
            
            if (depth > transactionDepth)
                throw new ArgumentException($"savePoint '{savePoint}' is not valid: depth ({depth}) >= transactionDepth ({transactionDepth})", nameof(savePoint));

            try
            {
                Execute(cmd + savePoint);
                transactionDepth = depth-1;
            }
            catch
            {
                RollbackTo(null, true);
                throw;
            }
        }
        #endregion


        /// <summary>
        ///     Executes
        ///     <paramref name="writeAction" />
        ///     within a (possibly nested) transaction by wrapping it in a SAVEPOINT. If an
        ///     exception occurs the whole transaction is rolled back, not just the current savepoint. The exception
        ///     is rethrown.
        /// </summary>
        /// <param name="writeAction">
        /// The <see cref="Action" /> to perform within a transaction.
        /// <paramref name="writeAction" />
        /// can contain any number of operations on the connection but should never call <see cref="BeginTransaction" /> or <see cref="Commit" />.
        /// </param>
        /// <param name="cloneConnection">clone the connection if not already in a transaction, to prevent sharing a transaction with other simultaneous calls that do not use a transaction.</param>
        /// <remarks>
        /// When not in a transaction, create a new connection to execute the write action so it is not shared with other calls.
        /// 
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public void RunInTransaction(Action<SQLiteConnection> writeAction, bool cloneConnection = true)
        {
            //When not in a transaction, create a new connection to execute the write action 
            if (cloneConnection && !IsInTransaction)
            {
                using var db = Clone();
                db.RunInTransaction(writeAction, false);
                return;
            }
            
            string? savePoint = null;
            try
            {
                savePoint = SaveTransactionPoint();
                writeAction(this);
                Release(savePoint);
            }
            catch (Exception e)
            {
                if(savePoint != null)
                    RollbackTo(savePoint, true);
                throw;
            }
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="runInTransaction">A boolean indicating if the inserts should be wrapped in a transaction.</param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int InsertAll(IEnumerable objects, bool runInTransaction = true)
        {
            var c = 0;
            if (runInTransaction)
            {
                RunInTransaction(db =>
                {
                    c = db.InsertAll(objects, false);
                });
            }
            else
            {
                foreach (var r in objects)
                    c += Insert(r);
            }
            return c;
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <param name="runInTransaction">A boolean indicating if the inserts should be wrapped in a transaction.</param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int InsertAll(IEnumerable objects, string extra, bool runInTransaction = true)
        {
            var c = 0;
            if (runInTransaction)
            {
                RunInTransaction(db =>
                {
                    foreach (var r in objects)
                        c += db.Insert(r, extra);
                });
            }
            else
            {
                foreach (var r in objects)
                    c += Insert(r, extra);
            }
            return c;
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <param name="runInTransaction">A boolean indicating if the inserts should be wrapped in a transaction.</param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int InsertAll(IEnumerable objects, Type objType, bool runInTransaction = true)
        {
            var c = 0;
            if (runInTransaction)
            {
                RunInTransaction(db =>
                {
                    foreach (var r in objects)
                        c += db.Insert(r, objType);
                });
            }
            else
            {
                foreach (var r in objects)
                    c += Insert(r, objType);
            }
            return c;
        }

        public int InsertOrIgnoreAll (IEnumerable objects) 
            => InsertAll (objects, "OR IGNORE");

        public int InsertOrIgnore (object? obj) 
            => obj == null ? 0 : Insert (obj, "OR IGNORE", Orm.GetObjType(obj));

        public int InsertOrIgnore (object obj, Type objType) 
            => Insert (obj, "OR IGNORE", objType);

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object? obj) 
            => obj == null ? 0 : Insert(obj, "", Orm.GetObjType(obj));

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows modified.
        /// </returns>
        public int InsertOrReplace(object? obj) 
            => obj == null ? 0 : Insert(obj, "OR REPLACE", Orm.GetObjType(obj));

        /// <summary>
        ///     Inserts all specified objects.
        ///     For each insertion, if a UNIQUE
        ///     constraint violation occurs with
        ///     some pre-existing object, this function
        ///     deletes the old object.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert or replace.
        /// </param>
        /// <returns>
        ///     The total number of rows modified.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int InsertOrReplaceAll(IEnumerable objects)
        {
            var c = 0;
            RunInTransaction(db =>
            {
                foreach (var r in objects) 
                    c += db.InsertOrReplace(r);
            });
            return c;
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object obj, Type objType) 
            => Insert(obj, "", objType);

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows modified.
        /// </returns>
        public int InsertOrReplace(object obj, Type objType) 
            => Insert(obj, "OR REPLACE", objType);

        /// <summary>
        ///     Inserts all specified objects.
        ///     For each insertion, if a UNIQUE
        ///     constraint violation occurs with
        ///     some pre-existing object, this function
        ///     deletes the old object.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert or replace.
        /// </param>
        /// <param name="objType">
        ///     The type of objects to insert or replace.
        /// </param>
        /// <returns>
        ///     The total number of rows modified.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int InsertOrReplaceAll(IEnumerable objects, Type objType)
        {
            var c = 0;
            RunInTransaction(db =>
            {
                foreach (var r in objects) 
                    c += db.InsertOrReplace(r, objType);
            });
            return c;
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>

        public int Insert(object? obj, string extra) 
            => obj == null ? 0 : Insert(obj, extra, Orm.GetObjType(obj));

        /// <summary>
        /// Inserts the given object and retrieves its
        /// auto incremented primary key if it has one.
        /// The return value is the number of rows added to the table.
        /// </summary>
        /// <param name="obj">
        /// The object to insert.
        /// </param>
        /// <param name="extra">
        /// Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <param name="objType">
        /// The type of object to insert.
        /// </param>
        /// <returns>
        /// The number of rows added to the table.
        /// </returns>
        public int Insert(object? obj, string extra, Type? objType)
        {
            if (obj == null || objType == null)
                return 0;

            var map = GetMapping(objType);

            foreach (var pk in map.PKs.Where(pk => pk.IsAutoGuid))
            {
                var prop = objType.GetRuntimeProperty(pk.PropertyName);
                if (prop != null && prop.GetValue(obj, null).Equals(Guid.Empty))
                    prop.SetValue(obj, Guid.NewGuid(), null);
            }

            var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

            var cols = replacing ? map.Columns : map.InsertColumns;
            var vals = new object[cols.Length];
            for (var i = 0; i < vals.Length; i++)
                vals[i] = cols[i].GetValue(obj);

            int count;

            try
            {
                // We lock here to protect the prepared statement returned via GetInsertCommand.
                // A SQLite prepared statement can be bound for only one operation at a time.
                var insertCmd = GetInsertCommand(map, extra);
                lock (insertCmd)
                {
                    count = insertCmd.ExecuteNonQuery(vals);
                }
            }
            catch (SQLiteException ex)
            {
                if (sqlite.ExtendedErrCode(Handle) == ExtendedResult.ConstraintNotNull)
                    throw new NotNullConstraintViolationException(ex.Result, ex.Message, map, obj);
                throw;
            }

            if (map.HasAutoIncPK)
            {
                var id = sqlite.LastInsertRowid(Handle);
                map.SetAutoIncPK(obj, id);
            }

            //if (count > 0)
            //    OnTableChanged (map, NotifyTableChangedAction.Insert);

            return count;
        }

        private PreparedSqlLiteInsertCommand GetInsertCommand(TableMapping map, string extra)
        {
            var key = (map.MappedType.FullName, extra);

            if (!_insertCommandMap.TryGetValue(key, out var prepCmd))
            {
                prepCmd = CreateInsertCommand(map, extra);
                if (!_insertCommandMap.TryAdd(key, prepCmd))
                {
                    prepCmd.Dispose();
                    prepCmd = _insertCommandMap[key];
                }
            }

            return prepCmd;
        }

        PreparedSqlLiteInsertCommand CreateInsertCommand(TableMapping map, string extra)
        {
            var cols = map.InsertColumns;
            string insertSql;
            if (cols.Length == 0 && map.Columns.Length == 1 && map.Columns[0].IsAutoInc)
                insertSql = string.Format("insert {1} into \"{0}\" default values", map.TableName, extra);
            else
            {
                var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

                if (replacing)
                    cols = map.InsertOrReplaceColumns;

                insertSql = string.Format("insert {3} into \"{0}\"({1}) values ({2})", map.TableName,
                    string.Join(",", (from c in cols select "\"" + c.Name + "\"").ToArray()),
                    string.Join(",", (from c in cols select "?").ToArray()), extra);
            }

            return new PreparedSqlLiteInsertCommand(this, insertSql);
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>
        ///     The number of rows updated.
        /// </returns>

        public int Update(object obj)
        {
            if (obj == null)
            {
                return 0;
            }
            return Update(obj, Orm.GetObjType(obj));
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows updated.
        /// </returns>

        public int Update(object obj, Type objType)
        {
            int rowsAffected;
            if (obj == null || objType == null)
            {
                return 0;
            }

            var map = GetMapping(objType);

            var pk = map.PK;

            if (pk == null)
            {
                throw new NotSupportedException("Cannot update " + map.TableName + ": it has no PK");
            }

            var cols = (from p in map.Columns
                       where !(from pkey in map.PKs
                               select pkey).Contains(p)
                       select p).ToList();

            if (cols.Count == 0)
                return 0;

            var vals = from c in cols
                       select c.GetValue(obj);

            var pkeys = from pkey in map.PKs
                        select pkey.GetValue(obj);

            var ps = vals.Concat(pkeys).ToList();

            var q = "";

            if (map.PKs.Count > 1)
            {
                q = string.Format("update \"{0}\" set {1} where {2} ", map.TableName,
                                                                       string.Join(",", (from c in cols select "\"" + c.Name + "\" = ? ").ToArray()),
                                                                       map.PkWhereSql);
            }
            else
            {
                q = string.Format("update \"{0}\" set {1} where {2} ", map.TableName,
                                                                       string.Join(",", (from c in cols select "\"" + c.Name + "\" = ? ").ToArray()),
                                                                       map.PkWhereSql);
            }

            try
            {
                rowsAffected = Execute(q, ps.ToArray());
            }
            catch (SQLiteException ex)
            {

                if (ex.Result == Result.Constraint && sqlite.ExtendedErrCode(Handle) == ExtendedResult.ConstraintNotNull)
                {
                    throw new NotNullConstraintViolationException(ex, map, obj);
                }

                throw;
            }

            return rowsAffected;
        }

        /// <summary>
        ///     Updates all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="runInTransaction">A boolean indicating if the inserts should be wrapped in a transaction.</param>
        /// <returns>
        ///     The number of rows modified.
        /// </returns>
        /// <remarks>
        /// All transactions methods creates a state in this connection. Be sure to not share it with other calls.
        /// </remarks>
        public int UpdateAll(IEnumerable objects, bool runInTransaction = true)
        {
            var c = 0;
            if (runInTransaction)
            {
                RunInTransaction(db =>
                {
                    foreach (var r in objects)
                        c += db.Update(r);
                });
            }
            else
            {
                foreach (var r in objects)
                    c += Update(r);
            }
            return c;
        }

        /// <summary>
        ///     Deletes the given object from the database using its primary key.
        /// </summary>
        /// <param name="objectToDelete">
        ///     The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>
        ///     The number of rows deleted.
        /// </returns>
        public int Delete(object objectToDelete)
        {
            var map = GetMapping(objectToDelete.GetType());
            if (map.PK == null)
            {
                throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
            }
            var q = string.Format("delete from \"{0}\" where {1}", map.TableName, map.PkWhereSql);
            return Execute(q, map.PKs.Select(pk => pk.GetValue(objectToDelete)).ToArray());
        }

        /// <summary>
        ///     Deletes the given objects from the database using their primary keys.
        /// </summary>
        /// <param name="objects">
        ///     The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>
        ///     The number of rows deleted.
        /// </returns>

        public int Delete<T>(IEnumerable<T> objects)
        {
            if (objects == null)
                return 0;

            var map = GetMapping(typeof (T));
            if (map.PK == null)
            {
                throw new NotSupportedException("Cannot delete from " + map.TableName + ": it has no PK");
            }
            var obj = objects.ToList();

            if (map.PKs.Count > 1)
            {
                string q = string.Format("delete from \"{0}\" where {1}", map.TableName, map.PkWhereSql);
                return obj.Sum(objectToDelete => Execute(q, map.PKs.Select(pk => pk.GetValue(objectToDelete))));
            }
            else
            {
                //Optimization: delete all objects in one request.
                //Note: won't work if there are too much objects (command string length will be over max command size)
                var pk = map.PK;
                var keyObjects = obj.Select(o => pk.GetValue(o)).ToList();
                if (keyObjects.Count > 0)
                {
                    var keyListParams = String.Join(",", Enumerable.Repeat('?', obj.Count));
                    var q = string.Format("delete from \"{0}\" where \"{1}\" in ({2})", map.TableName, pk.Name, keyListParams);
                    return Execute(q, keyObjects);
                }
            }

            return 0;
        }

        /// <summary>
        ///     Deletes the object with the specified primary key.
        /// </summary>
        /// <param name="primaryKey">
        ///     The primary key of the object to delete.
        ///     In case of a multiple primary key, you can set all primary key values in table order
        /// </param>
        /// <returns>
        ///     The number of objects deleted.
        /// </returns>
        /// <typeparam name='T'>
        ///     The type of object.
        /// </typeparam>

        public int Delete<T>(IEnumerable primaryKey)
        {
            if (primaryKey == null)
                return 0;
            var pks = primaryKey.Cast<object>().ToArray();
            if (pks.Length == 0)
                return 0;
            var map = GetMapping(typeof(T));
            if (map.PK == null)
                throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
            if (pks.Length > map.PKs.Count)
                throw new ArgumentException("primaryKeys array length can not be greater than the number of primary keys");

            var q = string.Format("delete from \"{0}\" where {1}", map.TableName, map.PkWhereSqlForPartialKeys(pks.Length));
            return Execute(q, pks);
        }

        /// <summary>
        /// Delete objects by primary key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys">primary keys of items to delete</param>
        /// <returns>The number of objects deleted</returns>

        public int DeleteIn<T>(IEnumerable keys)
        {
            if (keys == null)
                return 0;
            var theKeys = keys.Cast<object>().ToArray();
            if (theKeys.Length == 0)
                return 0;
            var map = GetMapping(typeof(T));
            if (map.PK == null)
                throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
            if (map.PKs.Count != 1)
                throw new ArgumentException("only single primary keys are supported by this method");

            var keyListParams = String.Join(",", Enumerable.Repeat('?', theKeys.Length));
            var q = string.Format("delete from \"{0}\" where \"{1}\" in ({2})", map.TableName, map.PKs[0].Name, keyListParams);
            return Execute(q, theKeys);
        }

        /// <summary>
        ///     Deletes all the objects from the specified table.
        ///     WARNING WARNING: Let me repeat. It deletes ALL the objects from the
        ///     specified table. Do you really want to do that?
        /// </summary>
        /// <returns>
        ///     The number of objects deleted.
        /// </returns>
        /// <typeparam name='T'>
        ///     The type of objects to delete.
        /// </typeparam>

        public int DeleteAll<T>()
        {
            return DeleteAll(typeof (T));
        }

        /// <summary>
        ///     Deletes all the objects from the specified table.
        ///     WARNING WARNING: Let me repeat. It deletes ALL the objects from the
        ///     specified table. Do you really want to do that?
        /// </summary>
        /// <returns>
        ///     The number of objects deleted.
        /// </returns>
        /// <typeparam name='T'>
        ///     The type of objects to delete.
        /// </typeparam>

        public int DeleteAll(Type t)
        {
            var map = GetMapping(t);
            var query = string.Format("delete from \"{0}\"", map.TableName);
            return Execute(query);
        }

        #region Backup

        public async Task<string> CreateDatabaseBackup()
        {
            var destDBPath = this.DatabasePath + "." + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff");

            var r = sqlite.Open(destDBPath, out var destDB,
                (int) (SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite), null);

            if (r != Result.OK)
                throw new SQLiteException(r, String.Format("Could not open backup database file: {0} ({1})", destDBPath, r));

            /* Open the backup object used to accomplish the transfer */
            IDbBackupHandle bHandle = sqlite.BackupInit(destDB, "main", this.Handle, "main");

            if (bHandle == null)
            {
                // Close the database connection 
                sqlite.Close(destDB);

                throw new SQLiteException(r, String.Format("Could not initiate backup process: {0}", destDBPath));
            }

            // Each iteration of this loop copies 5 database pages from database
            // pDb to the backup database. If the return value of backup_step()
            // indicates that there are still further pages to copy, sleep for
            // 250 ms before repeating.
            do
            {
                r = sqlite.BackupStep(bHandle, 5);

                if (r == Result.OK || r == Result.Busy || r == Result.Locked)
                {
                    await Task.Delay(250);
                }
            } while (r == Result.OK || r == Result.Busy || r == Result.Locked);

            // Release resources allocated by backup_init().
            r = sqlite.BackupFinish(bHandle);

            if (r != Result.OK)
            {
                // Close the database connection 
                sqlite.Close(destDB);

                throw new SQLiteException(r, String.Format("Could not finish backup process: {0} ({1})", destDBPath, r));
            }

            // Close the database connection 
            sqlite.Close(destDB);

            return destDBPath;
        }

        #endregion

        ~SQLiteConnection()
        {
            Dispose(false);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Close();
        }


        public void Close()
        {
            if (_open && Handle != null)
            {
                try
                {
                    foreach (var sqlInsertCommand in _insertCommandMap.Values)
                        sqlInsertCommand.Dispose();
                    _insertCommandMap.Clear();

                    var r = sqlite.Close(Handle);
                    if (r != Result.OK)
                    {
                        var msg = sqlite.Errmsg16(Handle);
                        throw new SQLiteException(r, msg);
                    }
                }
                finally
                {
                    Handle = null;
                    _open = false;
                }
            }
        }

        public class ColumnInfo
        {
            //			public int cid { get; set; }

    
            [Column("name")]
            public string Name { get; set; }

            //			[Column ("type")]
            //			public string ColumnType { get; set; }

    
            public int notnull { get; set; }

            //			public string dflt_value { get; set; }

            //			public int pk { get; set; }

    
            public override string ToString()
            {
                return Name;
            }
        }

        private struct IndexInfo
        {
            public List<IndexedColumn> Columns;
            public string IndexName;
            public string TableName;
            public bool Unique;
        }

        private struct IndexedColumn
        {
            public string ColumnName;
            public int Order;
        }
    }
}
