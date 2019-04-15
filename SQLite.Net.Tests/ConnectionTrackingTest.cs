using System.Linq;
using NUnit.Framework;


namespace SQLite.Net2.Tests
{
    [TestFixture]
    public class ConnectionTrackingTest : BaseTest
    {
        public class Product
        {
            [AutoIncrement, PrimaryKey]
            public int Id { get; set; }

            public string Name { get; set; }
            public decimal Price { get; set; }

            public OrderLine[] GetOrderLines(TestDb db)
            {
                return db.Table<OrderLine>().Where(o => o.ProductId == Id).ToArray();
            }
        }

        public class OrderLine
        {
            [AutoIncrement, PrimaryKey]
            public int Id { get; set; }

            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        public class TestDb : SQLiteConnection
        {
            public TestDb()
                : base(TestPath.CreateTemporaryDatabase())
            {
                CreateTable<Product>();
                CreateTable<OrderLine>();
                TraceListener = DebugTraceListener.Instance;
            }
        }

        [Test]
        public void CreateThem()
        {
            var db = new TestDb();

            var foo = new Product
            {
                Name = "Foo",
                Price = 10.0m
            };
            var bar = new Product
            {
                Name = "Bar",
                Price = 0.10m
            };
            db.Insert(foo);
            db.Insert(bar);
            db.Insert(new OrderLine
            {
                ProductId = foo.Id,
                Quantity = 6,
                UnitPrice = 10.01m
            });
            db.Insert(new OrderLine
            {
                ProductId = foo.Id,
                Quantity = 3,
                UnitPrice = 0.02m
            });
            db.Insert(new OrderLine
            {
                ProductId = bar.Id,
                Quantity = 9,
                UnitPrice = 100.01m
            });

            OrderLine[] lines = foo.GetOrderLines(db);

            Assert.AreEqual(lines.Length, 2, "Has 2 order lines");
        }
    }
}