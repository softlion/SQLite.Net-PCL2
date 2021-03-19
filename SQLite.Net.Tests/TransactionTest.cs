using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;



namespace SQLite.Net2.Tests
{
    [TestFixture]
    public class TransactionTest : BaseTest
    {
        [SetUp]
        public void Setup()
        {
            testObjects = Enumerable.Range(1, 20).Select(i => new TestObj()).ToList();

            db = new TestDb(TestPath.CreateTemporaryDatabase());
            db.InsertAll(testObjects);
        }

        [TearDown]
        public void TearDown()
        {
            if (db != null)
            {
                db.Close();
            }
        }

        private TestDb db;
        private List<TestObj> testObjects;

        public class TestObj
        {
            [AutoIncrement, PrimaryKey]
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("[TestObj: Id={0}]", Id);
            }
        }

        public class TransactionTestException : Exception
        {
        }

        public class TestDb : SQLiteConnection
        {
            public TestDb(String path)
                : base(path)
            {
                CreateTable<TestObj>();
            }
        }

        [Test]
        public void FailNestedSavepointTransaction()
        {
            try
            {
                db.RunInTransaction(() =>
                {
                    db.Delete(testObjects[0]);

                    db.RunInTransaction(() =>
                    {
                        db.Delete(testObjects[1]);

                        throw new TransactionTestException();
                    });
                });
            }
            catch (TransactionTestException)
            {
                // ignore
            }

            Assert.AreEqual(testObjects.Count, db.Table<TestObj>().Count());
        }

        [Test]
        public void FailSavepointTransaction()
        {
            try
            {
                db.RunInTransaction(() =>
                {
                    db.Delete(testObjects[0]);

                    throw new TransactionTestException();
                });
            }
            catch (TransactionTestException)
            {
                // ignore
            }

            Assert.AreEqual(testObjects.Count, db.Table<TestObj>().Count());
        }

        [Test]
        public void SuccessfulNestedSavepointTransaction()
        {
            db.RunInTransaction(() =>
            {
                db.Delete(testObjects[0]);

                db.RunInTransaction(() => { db.Delete(testObjects[1]); });
            });

            Assert.AreEqual(testObjects.Count - 2, db.Table<TestObj>().Count());
        }

        [Test]
        public void SuccessfulSavepointTransaction()
        {
            db.RunInTransaction(() =>
            {
                db.Delete(testObjects[0]);
                db.Delete(testObjects[1]);
                db.Insert(new TestObj());
            });

            Assert.AreEqual(testObjects.Count - 1, db.Table<TestObj>().Count());
        }

        
        [Test]
        public void FailSavepointTransactionException()
        {
            try
            {
                db.RunInTransaction(() =>
                {
                    throw new TransactionTestException();
                });
            }
            catch(TransactionTestException)
            {
                return;
            }

            Assert.Fail("Incorrect exception thrown");
        }

                
        [Test]
        public void SuccesfulSavepointTransaction()
        {
            db.RunInTransaction(() =>
            {
                db.InsertOrReplaceAll(testObjects);
            });
        }
    }
}