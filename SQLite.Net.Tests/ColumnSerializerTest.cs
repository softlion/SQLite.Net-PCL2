using NUnit.Framework;

namespace SQLite.Net2.Tests
{
    public class TestColumnDeserializerModel : IColumnDeserializer
    {
        [PrimaryKey]
        public int Id;
        
        public int ShouldBeSet { get; set; }

        public (int x, int y) Position;
        
        public int ShouldNotBeSet { get; set; }
        
        public void Deserialize(IColumnReader reader)
        {
            // Fields
            Id = reader.ReadInt32(0);
            Position = (
                reader.ReadInt32(1),
                reader.ReadInt32(2));

            // Properties
            ShouldBeSet = reader.ReadInt32(3);
        }
    }
    
    [TestFixture]
    public class ColumnSerializerTest : BaseTest
    {
        [Test]
        public void CanHandleCustomDeserialization()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            db.CreateTable<TestColumnDeserializerModel>();

            db.Insert(new TestColumnDeserializerModel
            {
                Id = 1,
                ShouldBeSet = 2,
                Position = (3, 4),
                ShouldNotBeSet = 5
            });

            var entry = db.Table<TestColumnDeserializerModel>().First();
            
            Assert.That(entry.Id, Is.EqualTo(1));
            Assert.That(entry.Position, Is.EqualTo((3, 4)));
            Assert.That(entry.ShouldBeSet, Is.EqualTo(2));
            Assert.That(entry.ShouldNotBeSet, Is.EqualTo(0));
        }
    }
}
