using System;
using SQLite.Net.Interop;
using SQLitePCL;

namespace SQLite.Net
{
    public class SqliteApi : ISQLiteApi
    {
        public static SqliteApi Instance { get;} = new SqliteApi();

        public Result Open(string filename, out IDbHandle db, int flags, string zvfs)
        {
            var r = raw.sqlite3_open_v2(filename, out var db3, flags, zvfs);
            db = new DbHandle(db3);
            return (Result)r;
        }

        public ExtendedResult ExtendedErrCode(IDbHandle db)
        {
            var internalDbHandle = (DbHandle)db;
            return (ExtendedResult)raw.sqlite3_extended_errcode(internalDbHandle.DbPtr);
        }

        public int LibVersionNumber()
        {
            return raw.sqlite3_libversion_number();
        }

        public int Threadsafe()
        {
            return raw.sqlite3_threadsafe();
        }

        public string SourceID()
        {
            return raw.sqlite3_sourceid();
        }

        public Result EnableLoadExtension(IDbHandle db, int onoff)
        {
            throw new NotImplementedException("");
            //var internalDbHandle = (DbHandle) db;
            //return (Result)p.sqlite3_enable_load_extension(internalDbHandle.DbPtr, onoff);
        }

        public Result Close(IDbHandle db)
        {
            var internalDbHandle = (DbHandle)db;
            return (Result)raw.sqlite3_close(internalDbHandle.DbPtr);
        }

        public Result Initialize()
        {
            throw new NotImplementedException("");
            //return (Result)raw.sqlite3_initialize();
        }

        public Result Shutdown()
        {
            throw new NotImplementedException("");
            //return (Result)raw.sqlite3_shutdown();
        }

        public Result Config(ConfigOption option)
        {
            throw new NotImplementedException("");
            //return (Result)raw.sqlite3_config(option);
        }

        public Result BusyTimeout(IDbHandle db, int milliseconds)
        {
            var internalDbHandle = (DbHandle)db;
            return (Result)raw.sqlite3_busy_timeout(internalDbHandle.DbPtr, milliseconds);
        }

        public int Changes(IDbHandle db)
        {
            var internalDbHandle = (DbHandle)db;
            return raw.sqlite3_changes(internalDbHandle.DbPtr);
        }

        public IDbStatement Prepare2(IDbHandle db, string query)
        {
            var internalDbHandle = (DbHandle)db;
            sqlite3_stmt stmt;
            Result r = (Result)raw.sqlite3_prepare_v2(internalDbHandle.DbPtr, query, out stmt);
            if (r != Result.OK)
            {
                throw new SQLiteException(r, Errmsg16(internalDbHandle));
            }
            return new DbStatement(stmt);
        }

        public Result Step(IDbStatement stmt)
        {
            var internalStmt = (DbStatement)stmt;
            return (Result)raw.sqlite3_step(internalStmt.StmtPtr);
        }

        public Result Reset(IDbStatement stmt)
        {
            var internalStmt = (DbStatement)stmt;
            return (Result)raw.sqlite3_reset(internalStmt.StmtPtr);
        }

        public Result Finalize(IDbStatement stmt)
        {
            var internalStmt = (DbStatement)stmt;
            return (Result)raw.sqlite3_finalize(internalStmt.StmtPtr);
        }

        public long LastInsertRowid(IDbHandle db)
        {
            var internalDbHandle = (DbHandle)db;
            return raw.sqlite3_last_insert_rowid(internalDbHandle.DbPtr);
        }

        public string Errmsg16(IDbHandle db)
        {
            var internalDbHandle = (DbHandle)db;
            return raw.sqlite3_errmsg(internalDbHandle.DbPtr);
        }

        public int BindParameterIndex(IDbStatement stmt, string name)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_parameter_index(internalStmt.StmtPtr, name);
        }

        public int BindNull(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_null(internalStmt.StmtPtr, index);
        }

        public int BindInt(IDbStatement stmt, int index, int val)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_int(internalStmt.StmtPtr, index, val);
        }

        public int BindInt64(IDbStatement stmt, int index, long val)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_int64(internalStmt.StmtPtr, index, val);
        }

        public int BindDouble(IDbStatement stmt, int index, double val)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_double(internalStmt.StmtPtr, index, val);
        }

        public int BindText16(IDbStatement stmt, int index, string val, int n, IntPtr free)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_text(internalStmt.StmtPtr, index, val);
        }

        public int BindBlob(IDbStatement stmt, int index, byte[] val, int n, IntPtr free)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_bind_blob(internalStmt.StmtPtr, index, val);
        }

        public int ColumnCount(IDbStatement stmt)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_count(internalStmt.StmtPtr);
        }

        public string ColumnName16(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_name(internalStmt.StmtPtr, index);
        }

        public ColType ColumnType(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return (ColType)raw.sqlite3_column_type(internalStmt.StmtPtr, index);
        }

        public int ColumnInt(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_int(internalStmt.StmtPtr, index);
        }

        public long ColumnInt64(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_int64(internalStmt.StmtPtr, index);
        }

        public double ColumnDouble(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_double(internalStmt.StmtPtr, index);
        }

        public string ColumnText16(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_text(internalStmt.StmtPtr, index);
        }

        public byte[] ColumnBlob(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_blob(internalStmt.StmtPtr, index);
        }

        public int ColumnBytes(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_bytes(internalStmt.StmtPtr, index);
        }

        public byte[] ColumnByteArray(IDbStatement stmt, int index)
        {
            var internalStmt = (DbStatement)stmt;
            return raw.sqlite3_column_blob(internalStmt.StmtPtr, index);
        }

        #region Backup

        public IDbBackupHandle BackupInit(IDbHandle destHandle, string destName, IDbHandle srcHandle, string srcName)
        {
            var internalDestDb = (DbHandle)destHandle;
            var internalSrcDb = (DbHandle)srcHandle;

            var p = raw.sqlite3_backup_init(internalDestDb.DbPtr,
                                                                destName,
                                                                internalSrcDb.DbPtr,
                                                                srcName);

            if (p == null)
            {
                return null;
            }
            else
            {
                return new DbBackupHandle(p);
            }
        }

        public Result BackupStep(IDbBackupHandle handle, int pageCount)
        {
            var internalBackup = (DbBackupHandle)handle;
            return (Result)raw.sqlite3_backup_step(internalBackup.DbBackupPtr, pageCount);
        }

        public Result BackupFinish(IDbBackupHandle handle)
        {
            var internalBackup = (DbBackupHandle)handle;
            return (Result)raw.sqlite3_backup_finish(internalBackup.DbBackupPtr);
        }

        public int BackupRemaining(IDbBackupHandle handle)
        {
            var internalBackup = (DbBackupHandle)handle;
            return raw.sqlite3_backup_remaining(internalBackup.DbBackupPtr);
        }

        public int BackupPagecount(IDbBackupHandle handle)
        {
            var internalBackup = (DbBackupHandle)handle;
            return raw.sqlite3_backup_pagecount(internalBackup.DbBackupPtr);
        }

        public int Sleep(int millis)
        {
            throw new NotImplementedException("Sleep is not implemented");
            //return raw.sqlite3_sleep(millis);
        }

        private struct DbBackupHandle : IDbBackupHandle
        {
            public sqlite3_backup DbBackupPtr { get; private set; }

            public DbBackupHandle(sqlite3_backup dbBackupPtr) : this()
            {
                DbBackupPtr = dbBackupPtr;
            }

            public bool Equals(IDbBackupHandle other)
            {
                return other is DbBackupHandle && DbBackupPtr == ((DbBackupHandle)other).DbBackupPtr;
            }
        }

        #endregion

        private class DbHandle : IDbHandle
        {
            public sqlite3 DbPtr { get; private set; }

            public DbHandle(sqlite3 handle)
            {
                DbPtr = handle;
            }

            public bool Equals(IDbHandle other)
            {
                return other is DbHandle && DbPtr == ((DbHandle)other).DbPtr;
            }
        }

        private struct DbStatement : IDbStatement
        {
            public sqlite3_stmt StmtPtr { get; private set; }

            public DbStatement(sqlite3_stmt stmtPtr)
                : this()
            {
                StmtPtr = stmtPtr;
            }

            public bool Equals(IDbStatement other)
            {
                return other is DbStatement && StmtPtr == ((DbStatement)other).StmtPtr;
            }
        }
    }
}