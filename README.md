# Setup

Add this package to your netstandard project:

[![NuGet](https://img.shields.io/nuget/v/sqlite-net2.svg)](https://www.nuget.org/packages/sqlite-net2/)  
![Nuget](https://img.shields.io/nuget/dt/sqlite-net2)

Also add one of the https://github.com/ericsink/SQLitePCL.raw package of your choice to the netstandard project:
- SQLitePCLRaw.bundle_e_sqlite3 for a normal database file
- SQLitePCLRaw.bundle_e_sqlcipher for a crypted database file

And call this statup function in each of your platform projects:

```
SQLitePCL.Batteries_V2.Init()
```

For a simple key/value store based on sqlite, or a drop-in replacement (alternative) to the unstable Akavache, check https://github.com/softlion/KeyValueLite

# Features

* Netstandard 2+
* Uses SQLitePCLRaw for sqlite raw communication
* Compatible with SQLitePCLRaw standard and cypher
* Stable and used in tons of apps

# Other Features (compared to oysteinkrog)

* Multiple primary key support		
 Ex: 		
 ```		
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
Note that while SQLitePCLRaw states that the database can be accessed by mutiple threads simultaneously, experience proves that you should always prevent multithread access, otherwise rare random crash occur. You can use `SemaphoreSlim` to serialize calls.

* Another trick  
Use transactions! In sqlite they speed up all queries a lot.

# Original Fork

https://github.com/praeclarum/sqlite-net

# Usage

Note: see unit tests for more examples.

## Define the database schema using a code first approach.

```csharp
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

```csharp
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

```csharp
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

```csharp
    var stocksStartingWithA = db.Table<DbStock>()
                .Where(stock => stock.Symbol.StartsWith("A"))
                .OrderBy(stock => stock.Symbol)
                .ToList();

    var allStocks = db.Table<DbStock>().ToList();
```

Advanced queries using SQL:

```csharp
    var dbValuation = db.Query<DbValuation> ("select * from DbValuation where StockId = ?", stock.Id);
    db.Execute("delete * from DbValuation where StockId = ?", stock.Id);
```

The T in `db.Query<T>` specifies the object to create for each row. It can be a table class, or any other class whose public properties match the query columns.

```csharp
    public class Val {
    	public decimal Money { get; set; }
    	public DateTime Date { get; set; }
    }
    public static IEnumerable<Val> QueryVals (SQLiteConnection db, Stock stock)
    {
    	return db.Query<Val> ("select 'Price' as 'Money', 'Time' as 'Date' from Valuation where StockId = ?", stock.Id);
    }
```

## Encrypting the database file

Add the nuget `SQLitePCLRaw.bundle_e_sqlcipher` to your project containing `sqlite-net2`.

Call this right after opening or creating the db, as the 1st instruction.

```
var db = new SQLiteConnection(filePath);
db.Execute($"PRAGMA key = '{key}';");
```

And use the db as usual.
