using System;
using SQLite.Net.Attributes;
using NUnit.Framework;

namespace SQLite.Net.Tests
{
    [TestFixture]
    public class DateTimeOffsetTest : BaseTest
    {
        class TestObj
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public string Name { get; set; }
            public DateTimeOffset ModifiedTime { get; set; }
        }

        [Test]
        public void AsTicks ()
        {
            var db = new TestDb ();
            TestDateTimeOffset (db);
        }

        [Test]
        public void AsyncAsTicks ()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            TestAsyncDateTimeOffset (db);
        }

        void TestAsyncDateTimeOffset (SQLiteConnection db)
        {
            db.CreateTable<TestObj> ();

            TestObj o, o2;

            //
            // Ticks
            //
            o = new TestObj {
                ModifiedTime = new DateTimeOffset (2012, 1, 14, 3, 2, 1, TimeSpan.Zero),
            };
            db.Insert(o);
            o2 = db.Get<TestObj> (o.Id);
            Assert.AreEqual (o.ModifiedTime, o2.ModifiedTime);
        }

        void TestDateTimeOffset (TestDb db)
        {
            db.CreateTable<TestObj> ();

            TestObj o, o2;

            //
            // Ticks
            //
            o = new TestObj {
                ModifiedTime = new DateTimeOffset (2012, 1, 14, 3, 2, 1, TimeSpan.Zero),
            };
            db.Insert (o);
            o2 = db.Get<TestObj> (o.Id);
            Assert.AreEqual (o.ModifiedTime, o2.ModifiedTime);
        }

    }
}

