using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;


namespace SQLite.Net2.Tests
{
    [TestFixture]
    public class MigrationTest : BaseTest
    {
        [Table("Test")]
        private class LowerId
        {
            public int Id { get; set; }
        }

        [Table("Test")]
        private class UpperId
        {
            public int ID { get; set; }
        }

        [Test]
        public void UpperAndLowerColumnNames()
        {
            using (var db = new TestDb(true)
            {
                TraceListener = DebugTraceListener.Instance
            })
            {
                db.CreateTable<LowerId>();
                db.CreateTable<UpperId>();

                List<SQLiteConnection.ColumnInfo> cols = db.GetTableInfo("Test").ToList();
                Assert.That(cols.Count, Is.EqualTo(1));
                Assert.That(cols[0].Name, Is.EqualTo("Id"));
            }
        }
    }
}