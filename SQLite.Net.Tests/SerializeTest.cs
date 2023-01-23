using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

using System.Threading.Tasks;

namespace SQLite.Net2.Tests
{
    [TestFixture]
    public class SerializeTest : BaseTest
    {
        public class SerializeTestObj
        {
            [AutoIncrement, PrimaryKey]
            public int Id { get; set; }

            public String Text { get; set; }

            public override string ToString()
            {
                return string.Format("[TestObj: Id={0}, Text={1}]", Id, Text);
            }
        }

        public class SerializeTestDb : SQLiteConnection
        {
            public SerializeTestDb(String path) : base(path)
            {
                CreateTable<SerializeTestObj>();
            }
        }

        [Test]
        public async Task SerializeRoundTrip()
        {
            var obj1 = new SerializeTestObj
            {
                Text = "GLaDOS loves testing!"
            };

            SQLiteConnection srcDb = new SerializeTestDb(":memory:");

            int numIn1 = srcDb.Insert(obj1);
            Assert.AreEqual(1, numIn1);

            List<SerializeTestObj> result1 = srcDb.Query<SerializeTestObj>("select * from SerializeTestObj").ToList();
            Assert.AreEqual(numIn1, result1.Count);
            Assert.AreEqual(obj1.Text, result1.First().Text);


            byte[] serialized = srcDb.Serialize();
            srcDb.Close();

            SQLiteConnection destDb = new SerializeTestDb(":memory");
            destDb.Deserialize(serialized);

            result1 = destDb.Query<SerializeTestObj>("select * from SerializeTestObj").ToList();
            Assert.AreEqual(numIn1, result1.Count);
            Assert.AreEqual(obj1.Text, result1.First().Text);

            destDb.Close();
        }
    }
}
