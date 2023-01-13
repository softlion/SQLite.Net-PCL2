using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;


namespace SQLite.Net2.Tests
{
    [TestFixture]
    public class ScalarTest : BaseTest
    {
        private class TestTable
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public int Two { get; set; }
        }

        private const int Count = 100;

        private SQLiteConnection CreateDb()
        {
            var db = new TestDb();
            db.CreateTable<TestTable>();
            IEnumerable<TestTable> items = from i in Enumerable.Range(0, Count)
                select new TestTable
                {
                    Two = 2
                };
            db.InsertAll(items);
            Assert.AreEqual(Count, db.Table<TestTable>().Count());
            return db;
        }


        [Test]
        public void Int32()
        {
            SQLiteConnection db = CreateDb();

            var r = db.ExecuteScalar<int>("SELECT SUM(Two) FROM TestTable");

            Assert.AreEqual(Count*2, r);
        }

        [Test]
        public void CanUseIntegerSumMinAndMax()
        {
            SQLiteConnection db = CreateDb();

            var table = db.Table<TestTable>();
            var r = table.Sum(x => x.Two);
            Assert.That(r, Is.EqualTo(Count * 2));

            var min = table.Min(x => x.Id);
            Assert.That(min, Is.EqualTo(1));
            var max = table.Max(x => x.Id);
            Assert.That(max, Is.EqualTo(Count));
        }
    }
}
