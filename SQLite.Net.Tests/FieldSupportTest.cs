using System.Linq;
using NUnit.Framework;

namespace SQLite.Net2.Tests
{
    public class FieldTestModel
    {
        [PrimaryKey, AutoIncrement]
        public int id;

        public string name;

        [Ignore]
        public int shouldNotBeSet0 = -3;
        private int shouldNotBeSet1 = -1;
        protected internal int shouldNotBeSet2 = -2;
        
        public string Role { get; set; }
        
        public int IgnoredProp1 { get; }
        public int IgnoredProp2 { private get; set; }

        public void AssertNotSet()
        {
            Assert.That(shouldNotBeSet0, Is.EqualTo(-3));
            Assert.That(shouldNotBeSet1, Is.EqualTo(-1));
            Assert.That(shouldNotBeSet2, Is.EqualTo(-2));
        }
    }

    public class FieldTestModelWithPkProperty
    {
        public int value;
        public string Name { get; set; }
        public string quote;
        [PrimaryKey]
        public int Id { get; set; }
    }

    [TestFixture]
    public class FieldSupportTest : BaseTest
    {
        [Test]
        public void FieldBeforePropertyOrdering()
        {
            string[] fieldNames = {
                nameof(FieldTestModel.id),
                nameof(FieldTestModel.name),
                nameof(FieldTestModel.shouldNotBeSet0),
                nameof(FieldTestModel.Role)
            };
            
            var members = ReflectionService.GetPublicInstanceProperties(typeof(FieldTestModel), new DefaultColumnInformationProvider()).ToList();
            
            Assert.That(members.Count, Is.EqualTo(fieldNames.Length));
            for (var i = 0; i < fieldNames.Length; ++i)
            {
                Assert.That(members[i].Name, Is.EqualTo(fieldNames[i]));
            }
        }
        
        [Test]
        public void PkPropertyBeforeFieldBeforePropertyOrdering()
        {
            string[] fieldNames = {
                nameof(FieldTestModelWithPkProperty.Id),
                nameof(FieldTestModelWithPkProperty.quote),
                nameof(FieldTestModelWithPkProperty.value),
                nameof(FieldTestModelWithPkProperty.Name),
            };

            var columnInformationProvider = new DefaultColumnInformationProvider();
            
            var members = ReflectionService.GetPublicInstanceProperties(typeof(FieldTestModelWithPkProperty), columnInformationProvider).ToList();
            
            Assert.That(members.Count, Is.EqualTo(fieldNames.Length));
            for (var i = 0; i < fieldNames.Length; ++i)
            {
                Assert.That(members[i].Name, Is.EqualTo(fieldNames[i]));
            }
        }
        
        [Test]
        public void CanCreateModelWithFields()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            var mapping = db.GetMapping<FieldTestModel>();
            
            Assert.That(mapping.Columns.Length, Is.EqualTo(3));
            Assert.That(mapping.Columns[0].Name, Is.EqualTo(nameof(FieldTestModel.id)));
            Assert.That(mapping.Columns[1].Name, Is.EqualTo(nameof(FieldTestModel.name)));

            db.CreateTable<FieldTestModel>();

            db.InsertAll(new[]
            {
                new FieldTestModel
                {
                    id = -1,
                    name = "hello",
                    Role = "chef"
                },
                new FieldTestModel
                {
                    id = -1,
                    name = "world",
                    Role = "waiter"
                },
            });

            var model = db.Table<FieldTestModel>().First(x => x.name == "hello");
            Assert.That(model.id, Is.Not.EqualTo(-1));
            Assert.That(model.Role, Is.EqualTo("chef"));
            
            model.AssertNotSet();
        }
    }
}
