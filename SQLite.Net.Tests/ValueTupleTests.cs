using System;
using System.Linq;
using NUnit.Framework;
using SQLite.Net2.Tests.Deps;

namespace SQLite.Net2.Tests
{
    public class TestModelWithValueTuple
    {
        public ValueTuple<int, string> Value { get; set; }
        
        public bool HasReadEula { get; set; }
    }

    public class TestDerivedWithGenericBase : Model<(int userId, string userName)>
    {
    }
    
    public class TestModelWithNamedValueTuple
    {
        public (int userId, string userName) Value { get; set; }
        
        public bool HasReadEula { get; set; }
    }

    public class BadTupleModel
    {
        public (int userId, (string firstName, string lastName)) key;
    }
    
    [TestFixture]
    public class ValueTupleTests : BaseTest
    {
        [Test]
        public void CanGetTableMappingForValueTuple()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            var mapping1 = db.GetMapping<TestModelWithValueTuple>();
            Assert.That(mapping1.Columns.Length, Is.EqualTo(3));
            Assert.That(mapping1.Columns[0].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.HasReadEula)}"));
            Assert.That(mapping1.Columns[1].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.Value)}_{nameof(TestModelWithValueTuple.Value.Item1)}"));
            Assert.That(mapping1.Columns[2].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.Value)}_{nameof(TestModelWithValueTuple.Value.Item2)}"));
            
            
            var mapping2 = db.GetMapping<TestModelWithNamedValueTuple>();
            Assert.That(mapping2.Columns.Length, Is.EqualTo(3));
            Assert.That(mapping2.Columns[0].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.HasReadEula)}"));
            Assert.That(mapping2.Columns[1].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.Value)}_{nameof(TestModelWithNamedValueTuple.Value.userId)}"));
            Assert.That(mapping2.Columns[2].Name, Is.EqualTo($"{nameof(TestModelWithValueTuple.Value)}_{nameof(TestModelWithNamedValueTuple.Value.userName)}"));

            var mapping3 = db.GetMapping<TestDerivedWithGenericBase>();
            Assert.That(mapping3.Columns.Length, Is.EqualTo(2));
            Assert.That(mapping3.Columns[0].Name, Is.EqualTo($"{nameof(TestDerivedWithGenericBase.Key)}_{nameof(TestDerivedWithGenericBase.Key.userId)}"));
            Assert.That(mapping3.Columns[1].Name, Is.EqualTo($"{nameof(TestDerivedWithGenericBase.Key)}_{nameof(TestDerivedWithGenericBase.Key.userName)}"));
        }

        [Test]
        public void CannotCreateTablesWithNestedTuples()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            Assert.Throws<NotSupportedException>(() => db.GetMapping<BadTupleModel>());
            Assert.Throws<NotSupportedException >(() => db.CreateTable<BadTupleModel>());
        }

        [Test]
        public void CanOrderByTupleMember()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            db.CreateTable<TestModelWithValueTuple>();
            db.InsertAll(new[]
            {
                new TestModelWithValueTuple
                {
                    Value = (1, "hello"),
                    HasReadEula = true
                },
                new TestModelWithValueTuple
                {
                    Value = (2, "world"),
                    HasReadEula = false
                }
            });

            var allEntriesDescending = db
                .Table<TestModelWithValueTuple>().OrderByDescending(x => x.Value.Item1)
                .ToList();
            
            Assert.That(allEntriesDescending.Count, Is.EqualTo(2));
            Assert.That(allEntriesDescending[0].Value.Item1, Is.EqualTo(2));
            Assert.That(allEntriesDescending[1].Value.Item1, Is.EqualTo(1));
        }

        [Test]
        public void CanAccessNamedTupleElementsNotInLocalAssembly()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            db.CreateTable<TestDerivedWithGenericBase>();

            db.InsertAll(new[]
            {
                new TestDerivedWithGenericBase
                {
                    Key = (1, "test.1")
                },
                new TestDerivedWithGenericBase
                {
                    Key = (2, "test.2")
                },
            });

            var items = db.Table<TestDerivedWithGenericBase>().Where(x => x.Key.userName == "test.1" && x.Key.userId == 1).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Key, Is.EqualTo((1, "test.1")));
        }
        
        [Test]
        public void CanOperateOnSingleLevelValueTuples()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            db.CreateTable<TestModelWithValueTuple>();
            db.CreateTable<TestModelWithNamedValueTuple>();

            db.InsertAll(new[]
            {
                new TestModelWithValueTuple
                {
                    Value = (1, "hello"),
                    HasReadEula = true
                },
                new TestModelWithValueTuple
                {
                    Value = (2, "world"),
                    HasReadEula = false
                }
            });
            
            db.InsertAll(new[]
            {
                new TestModelWithNamedValueTuple
                {
                    Value = (1, "hello"),
                    HasReadEula = true
                },
                new TestModelWithNamedValueTuple
                {
                    Value = (2, "world"),
                    HasReadEula = false
                }
            });

            var valueTupleUser1 = db
                .Table<TestModelWithValueTuple>()
                .First(x => x.Value.Item1 == 1);
            Assert.That(valueTupleUser1.Value.Item2, Is.EqualTo("hello"));
            Assert.That(valueTupleUser1.HasReadEula, Is.True);
            
            var namedValueUser1 = db
                .Table<TestModelWithNamedValueTuple>()
                .First(x => x.Value.userId == 2);
            
            Assert.That(namedValueUser1.Value.userName, Is.EqualTo("world"));
            Assert.That(namedValueUser1.HasReadEula, Is.False);
        }
    }
}
