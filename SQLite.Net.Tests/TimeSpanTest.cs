using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite.Net.Attributes;


namespace SQLite.Net.Tests
{
    [TestFixture]
    public class TimeSpanTest
    {
        private class TestDb
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public TimeSpan Time { get; set; }
        }


        private void TestAsyncDateTime(SQLiteConnection db)
        {
            db.CreateTable<TestDb>();

            var val1 = new TestDb
            {
                Time = new TimeSpan(1000),
            };
            db.Insert(val1);
            TestDb val2 = db.Get<TestDb>(val1.Id);
            Assert.AreEqual(val1.Time, val2.Time);
        }

        [Test]
        public void TestTimeSpan()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            TestAsyncDateTime(db);
        }
    }
}