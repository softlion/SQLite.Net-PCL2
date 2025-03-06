# About

SQLite client and ORM. Simple, powerful, cross-platform - Simplified and fixed version

# Setup

Add this package to a netstandard compatible project:

[![NuGet](https://img.shields.io/nuget/v/sqlite-net2.svg)](https://www.nuget.org/packages/sqlite-net2/)  
![Nuget](https://img.shields.io/nuget/dt/sqlite-net2)

**Required**: add ONLY ONE of the following packages to your common project:
- [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) for a normal database file
- [SQLitePCLRaw.bundle_e_sqlcipher](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher) for an encrypted database file

Then call the init method once. That can be in your App.cs:

 ```c#		
SQLitePCL.Batteries_V2.Init()
```

# Features

* Netstandard 2+
* Uses SQLitePCLRaw for sqlite raw communication
* Compatible with SQLitePCLRaw standard and cypher
* Stable and used in tons of apps

For a key/value store based on sqlite, or a drop-in replacement (alternative) to the unstable Akavache, check [KeyValueLite](https://github.com/softlion/KeyValueLite)

# Other Features (compared to oysteinkrog)

* Multiple primary key support		
 Ex: 		
 ```c#		
 public class PrivacyGroupItem		
 {		
    [PrimaryKey]		
    public int GroupId {get;set;}		
    
    [PrimaryKey]		
    public int ContactId {get;set;}		
 }		
    
 db.Delete<PrivacyGroupItem>(groupId, contactId);		
 ```
 		
* Projections now have the expected result type		
 Ex: `IEnumerable<int> ids = from pgi in db.Table<PrivacyGroupItem>() where pgi.PrivacyGroupId == groupId select pgi.ContactId;`		
 		
* New method to query simple types (ie: string, ...) as Query<T> can query only complex types (ie: T must be a class/stuct with a default constructor)		
 Signature: `IEnumerable<T> ExecuteSimpleQuery<T>(string query, params object[] args)`		
 Usage: `ExecuteSimpleQuery<string>("select 'drop table ' || name || ';' from sqlite_master where type = 'table'")`		

* No asynchronous API. Use Task.Run() if you want asynchronous calls.  
Note that while SQLitePCLRaw states that the database can be accessed by multiple threads simultaneously, experience proves that you should always prevent multithreaded access, otherwise rare random crash occur. You can use `SemaphoreSlim` to serialize calls.

* Another trick  
Use transactions! In sqlite they speed up all queries a lot.

# Original Fork

https://github.com/praeclarum/sqlite-net

# Usage

Note: see unit tests for more examples.

## Define the database schema using a code first approach.

```c#
    public class DbStock
    {
    	[PrimaryKey, AutoIncrement]
    	public int Id { get; set; }

    	[MaxLength(8)]
    	public string Symbol { get; set; }
    }

    public class DbValuation
    {
    	[PrimaryKey, AutoIncrement]
    	public int Id { get; set; }

    	[Indexed()]
    	public int StockId { get; set; }

    	[Indexed("Stock",1)] //This defines an index with multiple keys
    	public DateTime Time { get; set; }

    	[Indexed("Stock",2)]
    	public decimal Price { get; set; }
    }
```

## Create the schema

```c#
    var dbFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    dbFilePath = Path.Combine(dbFilePath, "store.db3");
    var exists = File.Exists(dbFilePath);
    var isFirstInit = !exists;

    if (isFirstInit)
    {
        //Make sure folder exists
        var folderPath = Path.GetDirectoryName(dbFilePath);
        Directory.CreateDirectory(folderPath);
        File.CreateText(dbFilePath).Dispose();
    }

    db = new SQLiteConnection(dbFilePath);

    if (isFirstInit)
    {
        //Create schema
        db.CreateTable<DbStock>();
        db.CreateTable<DbValuation>();
        //You may store your schema version using Xamarin Essentials
        //Xamarin.Essentials.Preferences.Set("DbVersion", 1); 
    }
```

## Read, Add, Update, Delete rows in tables

Simple add, update and delete:

```c#
    var stock = new DbStock() { Symbol = "EUR" };
    db.Insert(stock);
    stock.Symbol = "USD";
    db.Update(stock);
    db.Delete(stock);
    
    db.DeleteAll(allStocks);

    //Delete by id
    db.DeleteIn<DbDay>(new[] {1, 2, 3});

```

After the Insert call, stock.Id will be set, because Id has the AutoIncrement attribute.

Simple query using LINQ. Most linq operators work:

```c#
    var stocksStartingWithA = db.Table<DbStock>()
                .Where(stock => stock.Symbol.StartsWith("A"))
                .OrderBy(stock => stock.Symbol)
                .ToList();

    var allStocks = db.Table<DbStock>().ToList();
```

Advanced queries using SQL:

```c#
    var dbValuation = db.Query<DbValuation> ("select * from DbValuation where StockId = ?", stock.Id);
    db.Execute("delete * from DbValuation where StockId = ?", stock.Id);
```

The T in `db.Query<T>` specifies the object to create for each row. It can be a table class, or any other class whose public properties match the query columns.

```c#
    public class Val {
    	public decimal Money { get; set; }
    	public DateTime Date { get; set; }
    }
    public static IEnumerable<Val> QueryVals (SQLiteConnection db, Stock stock)
    {
    	return db.Query<Val> ("select 'Price' as 'Money', 'Time' as 'Date' from Valuation where StockId = ?", stock.Id);
    }
```

## Using an encrypted database file

Add the nuget [SQLitePCLRaw.bundle_e_sqlcipher](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher) to your common project.

### Option 1 (preferred)

Use the `encryptionKey` parameter in the constructor of `SQLiteConnection`.

Then use the db as usual.

With this option, the encryption key is kept in memory. That should not be an issue.


### Option 2 (roots)
Add this code right after opening or creating the db, and before any other db call:

```c#
string key = "yourCryptingKey";
var db = new SQLiteConnection(filePath);
var ok = db.ExecuteScalar<string>($"PRAGMA key = '{key}';");
if(ok != "ok")
   throw new Exception("Bad key");
```

If you will use `InsertAll(), InsertOrUpdateAll(), ReplaceAll()` you must create a new class deriving from `SQLiteConnection` and override `Clone()` so it sets the encryption key after cloning.

Then use the db as usual.

### Final Thoughts

You can read the version of the cypher lib using the code. Check the [Zenetik](//https://www.zetetic.net/sqlcipher/sqlcipher-api/#cipher_version) website for more information.

```c#
var cipherVer = db.ExecuteScalar<string>("PRAGMA cipher_version");
if (String.IsNullOrWhiteSpace(cipherVer))
    throw new Exception("This build is not using SQL CIPHER");
```
 
## Using transactions

Warning: all transactions methods create a state in this connection (the transaction depth).      
Be sure to not share the connection with other simultaneous threads.  
You can use `using var tempConnection = connection.Clone()` to prevent this issue.

The following methods use `Clone` to clone the connection and prevent any interaction of the transaction they create with your code:  
`InsertAll(), InsertOrUpdateAll(), ReplaceAll()`    
They all have a boolean parameter to disable this behavior (beware of performances), which will also prevent a correct rollback in case an exception occurs.

Standard transaction:
```c#
BeginTransaction();
...
Commit(); 
//or 
Rollback();
```

Nested transactions:
```c#
var savepoint=SaveTransactionPoint();
...
Release(savepoint);
//or
RollbackTo(savepoint);
```

## Limitations

Most databases (except SQLServer) store DateTimeOffset as UTC, forgetting the offset part. SQlite is not an exception.
