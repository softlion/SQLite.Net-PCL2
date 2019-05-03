# Setup

Add this package to your netstandard project:

[![NuGet](https://img.shields.io/nuget/v/sqlite-net2.svg)](https://www.nuget.org/packages/sqlite-net2/)

And call this function in your executable projects (.net, ios, android, uwp, mac, ...):

```
SQLitePCL.Batteries_V2.Init()
```


See https://github.com/ericsink/SQLitePCL.raw/ for more information on how to use SQLitePCL.raw

If you search a simple key value store based on sqlite, alternative to Akavache, check https://github.com/softlion/KeyValueLite

# Changes

* Netstandard 2.0 only
* Uses SQLitePCLRaw for sqlite raw communication


# New Features compared to oysteinkrog

 Multiple primary key support		
 Ex: 		
 		
     public class PrivacyGroupItem		
     {		
 		[PrimaryKey]		
 		public int GroupId {get;set;}		
 		
 		[PrimaryKey]		
 		public int ContactId {get;set;}		
     }		
 		
     db.Delete<PrivacyGroupItem>(groupId, contactId);		
 		
 		
 Projections now have the expected result type		
 Ex: `IEnumerable<int> ids = from pgi in db.Table<PrivacyGroupItem>() where pgi.PrivacyGroupId == groupId select pgi.ContactId;`		
 		
 New method to query simple types (ie: string, ...) as Query<T> can query only complex types (ie: T must be a class/stuct with a default constructor)		
 Signature: `IEnumerable<T> ExecuteSimpleQuery<T>(string query, params object[] args)`		
 Usage: `ExecuteSimpleQuery<string>("select 'drop table ' || name || ';' from sqlite_master where type = 'table'")`		

# Original Fork

https://github.com/praeclarum/sqlite-net

# Examples

Please consult the source code (see unit tests) for more examples.

The library contains simple attributes that you can use to control the construction of tables. In a simple stock program, you might use:

    public class Stock
    {
    	[PrimaryKey, AutoIncrement]
    	public int Id { get; set; }
    	[MaxLength(8)]
    	public string Symbol { get; set; }
    }

    public class Valuation
    {
    	[PrimaryKey, AutoIncrement]
    	public int Id { get; set; }
    	[Indexed]
    	public int StockId { get; set; }
    	public DateTime Time { get; set; }
    	public decimal Price { get; set; }
    }

Once you've defined the objects in your model you have a choice of APIs. You can use the "synchronous API" where calls
block one at a time, or you can use the "asynchronous API" where calls do not block. You may care to use the asynchronous
API for mobile applications in order to increase reponsiveness.

Both APIs are explained in the two sections below.

## Synchronous API

Once you have defined your entity, you can automatically generate tables in your database by calling `CreateTable`:

    var db = new SQLiteConnection(sqlitePlatform, "foofoo");
    db.CreateTable<Stock>();
    db.CreateTable<Valuation>();

You can insert rows in the database using `Insert`. If the table contains an auto-incremented primary key, then the value for that key will be available to you after the insert:

    public static void AddStock(SQLiteConnection db, string symbol) {
    	var s = db.Insert(new Stock() {
    		Symbol = symbol
    	});
    	Console.WriteLine("{0} == {1}", s.Symbol, s.Id);
    }

Similar methods exist for `Update` and `Delete`.

The most straightforward way to query for data is using the `Table` method. This can take predicates for constraining via WHERE clauses and/or adding ORDER BY clauses:

		var conn = new SQLiteConnection(sqlitePlatform, "foofoo");
		var query = conn.Table<Stock>().Where(v => v.Symbol.StartsWith("A"));

		foreach (var stock in query)
			Debug.WriteLine("Stock: " + stock.Symbol);

You can also query the database at a low-level using the `Query` method:

    public static IEnumerable<Valuation> QueryValuations (SQLiteConnection db, Stock stock)
    {
    	return db.Query<Valuation> ("select * from Valuation where StockId = ?", stock.Id);
    }

The generic parameter to the `Query` method specifies the type of object to create for each row. It can be one of your table classes, or any other class whose public properties match the column returned by the query. For instance, we could rewrite the above query as:

    public class Val {
    	public decimal Money { get; set; }
    	public DateTime Date { get; set; }
    }
    public static IEnumerable<Val> QueryVals (SQLiteConnection db, Stock stock)
    {
    	return db.Query<Val> ("select 'Price' as 'Money', 'Time' as 'Date' from Valuation where StockId = ?", stock.Id);
    }

You can perform low-level updates of the database using the `Execute` method.

## Asynchronous API

The asynchronous API has been removed, as it was only wrapping synchronous methods in Task.Run(), which has nasty side effects as multiple Tasks are queued. Use your own Task.Run to achieve the same effect.
