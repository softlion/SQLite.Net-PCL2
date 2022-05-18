//
// Copyright (c) 2012 Krueger Systems, Inc.
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

namespace SQLite.Net2
{
    /// <summary>
    /// https://www.sqlite.org/c3ref/open.html
    /// </summary>
    [Flags]
    public enum SQLiteOpenFlags
    {
        ReadOnly = 1,
        ReadWrite = 2,
        Create = 4,
        
        /// <summary>
        /// The filename can be interpreted as a URI
        /// See https://www.sqlite.org/c3ref/open.html
        /// </summary>
        OpenUri = 0x40,
        /// <summary>
        /// The database will be opened as an in-memory database. The database is named by the "filename" argument for the purposes of cache-sharing, if shared cache mode is enabled, but the "filename" is otherwise ignored.
        /// </summary>
        OpenMemory = 0x80,
        
        /// <summary>
        /// The new database connection will use the "multi-thread" threading mode.
        /// This means that separate threads are allowed to use SQLite at the same time, as long as each thread is using a different database connection.
        /// </summary>
        NoMutex = 0x8000,
        /// <summary>
        /// The new database connection will use the "serialized" threading mode.
        /// This means the multiple threads can safely attempt to use the same database connection at the same time.
        /// (Mutexes will block any actual concurrency, but in this mode there is no harm in trying.)
        /// </summary>
        FullMutex = 0x10000,
        
        SharedCache = 0x20000,
        PrivateCache = 0x40000,

        /// <summary>
        /// The database filename is not allowed to be a symbolic link
        /// </summary>
        NoFollow = 0x100000,
        /// <summary>
        /// The database connection comes up in "extended result code mode". In other words, the database behaves has if sqlite3_extended_result_codes(db,1) where called on the database connection as soon as the connection is created. In addition to setting the extended result code mode, this flag also causes sqlite3_open_v2() to return an extended result code
        /// </summary>
        ExResCode = 0x02000000,
        
        [Obsolete]
        ProtectionComplete = 0x100000,
        [Obsolete]
        ProtectionCompleteUnlessOpen = 0x200000,
        [Obsolete]
        ProtectionCompleteUntilFirstUserAuthentication = 0x300000,
        [Obsolete]
        ProtectionNone = 0x400000
    }
}