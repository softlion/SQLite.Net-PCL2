//
// Copyright (c) 2012 Krueger Systems, Inc.
// Copyright (c) 2013 Øystein Krog (oystein.krog@gmail.com)
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
using SQLite.Net.Interop;

namespace SQLite.Net
{
    /// <summary>
    ///     Since the insert never changed, we only need to prepare once.
    /// </summary>
    public class PreparedSqlLiteInsertCommand : IDisposable
    {
        private static readonly IDbStatement NullStatement = default(IDbStatement);
        readonly SqliteApi sqlite = SqliteApi.Instance;

        internal PreparedSqlLiteInsertCommand(SQLiteConnection conn)
        {
            Connection = conn;
        }


        public bool Initialized { get; set; }


        public string CommandText { get; set; }


        protected SQLiteConnection Connection { get; set; }


        protected IDbStatement Statement { get; set; }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PreparedSqlLiteInsertCommand()
        {
            Dispose(false);
        }

        static readonly object _locker = new object();


        public int ExecuteNonQuery(object[] source)
        {
            Connection.TraceListener.WriteLine("Executing: {0}", CommandText);
            if (!Initialized)
            {
                Statement = Prepare();
                Initialized = true;
            }

            //bind the values.
            if (source != null)
            {
                for (var i = 0; i < source.Length; i++)
                    SQLiteCommand.BindParameter(sqlite, Statement, i + 1, source[i], Connection.StoreDateTimeAsTicks, Connection.Serializer);
            }

            Result r;
            lock (_locker)
            {
                r = sqlite.Step(Statement);
            }

            if (r == Result.Done)
            {
                var rowsAffected = sqlite.Changes(Connection.Handle);
                sqlite.Reset(Statement);
                return rowsAffected;
            }
            if (r == Result.Error)
            {
                var msg = sqlite.Errmsg16(Connection.Handle);
                sqlite.Reset(Statement);
                throw new SQLiteException(r, msg);
            }
            if (r == Result.Constraint && sqlite.ExtendedErrCode(Connection.Handle) == ExtendedResult.ConstraintNotNull)
            {
                sqlite.Reset(Statement);
                throw new NotNullConstraintViolationException(r, sqlite.Errmsg16(Connection.Handle));
            }
            sqlite.Reset(Statement);

            throw new SQLiteException(r, r.ToString());
        }


        protected virtual IDbStatement Prepare()
        {
            try
            {
                var stmt = sqlite.Prepare2(Connection.Handle, CommandText);
                return stmt;
            }
            catch (Exception e)
            {
                throw new Exception($"Sqlite prepareinsert failed for sql: {CommandText}", e);
            }
        }

        private void Dispose(bool disposing)
        {
            if (Statement != NullStatement)
            {
                try
                {
                    sqlite.Finalize(Statement);
                }
                finally
                {
                    Statement = NullStatement;
                    Connection = null;
                }
            }
        }
    }
}