using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite.Net.Attributes;

namespace SQLite.Net.Tests
{
    [TestFixture]
    public class DateTimeTest : BaseTest
    {
        private class TestObj
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public string Name { get; set; }
            public DateTime Time1 { get; set; }
            public DateTime Time2 { get; set; }
        }


        private void TestAsyncDateTime(SQLiteConnection db, bool storeDateTimeAsTicks)
        {
            db.CreateTable<TestObj>();

            var org = new TestObj
            {
                Time1 = DateTime.UtcNow,
                Time2 = DateTime.Now,
            };
            db.Insert(org);
            var fromDb = db.Get<TestObj>(org.Id);
            Assert.AreEqual(fromDb.Time1.ToUniversalTime(), org.Time1.ToUniversalTime());
            Assert.AreEqual(fromDb.Time2.ToUniversalTime(), org.Time2.ToUniversalTime());

            Assert.AreEqual(fromDb.Time1.ToLocalTime(), org.Time1.ToLocalTime());
            Assert.AreEqual(fromDb.Time2.ToLocalTime(), org.Time2.ToLocalTime());
        }

        private void TestDateTime(TestDb db)
        {
            db.CreateTable<TestObj>();

            //
            // Ticks
            //
            var org = new TestObj
            {
                Time1 = DateTime.UtcNow,
                Time2 = DateTime.Now,
            };
            db.Insert(org);
            var fromDb = db.Get<TestObj>(org.Id);
            Assert.AreEqual(fromDb.Time1.ToUniversalTime(), org.Time1.ToUniversalTime());
            Assert.AreEqual(fromDb.Time2.ToUniversalTime(), org.Time2.ToUniversalTime());

            Assert.AreEqual(fromDb.Time1.ToLocalTime(), org.Time1.ToLocalTime());
            Assert.AreEqual(fromDb.Time2.ToLocalTime(), org.Time2.ToLocalTime());
        }

        [Test]
        public void AsStrings()
        {
            var db = new TestDb(storeDateTimeAsTicks: false);
            TestDateTime(db);
        }

        [Test]
        public void AsTicks()
        {
            var db = new TestDb(true);
            TestDateTime(db);
        }

        [Test]
        public void AsyncAsString()
        {
            var db = new SQLiteConnection(new SQLitePlatformTest(), TestPath.CreateTemporaryDatabase());
            TestAsyncDateTime(db, true);
        }

        [Test]
        public void  AsyncAsTicks()
        {
            var db = new SQLiteConnection(new SQLitePlatformTest(), TestPath.CreateTemporaryDatabase());
            TestAsyncDateTime(db, true);
        }
    }
}