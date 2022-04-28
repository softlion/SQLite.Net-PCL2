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
            
            public int Toto { get; set; }

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
        public void SuccesfulNestedSavepointTransaction()
        {
            db.RunInTransaction(dbb =>
            {
                dbb.Delete(testObjects[0]);
                dbb.RunInTransaction(dbbb => { dbbb.InsertAll(new TestObj[] { new(), new() }); });
            });

            //catch (SQLiteException e) when(e.Message == "database is locked")
            Assert.AreEqual(testObjects.Count+1, db.Table<TestObj>().Count());
        }

        [Test]
        public void FailNestedSavepointTransaction()
        {
            var hasCatch = false;
            try
            {
                db.RunInTransaction(dbb =>
                {
                    dbb.Delete(testObjects[0]);

                    dbb.RunInTransaction(dbbb =>
                    {
                        dbbb.Delete(testObjects[1]);
                        throw new TransactionTestException();
                    });
                });
            }
            catch (TransactionTestException)
            {
                hasCatch = true;
            }

            Assert.IsTrue(hasCatch);
            Assert.AreEqual(testObjects.Count, db.Table<TestObj>().Count());
        }

        [Test]
        public void FailSavepointTransaction()
        {
            try
            {
                db.RunInTransaction(dbb =>
                {
                    dbb.Delete(testObjects[0]);

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
            db.RunInTransaction(dbb =>
            {
                dbb.Delete(testObjects[0]);

                dbb.RunInTransaction(dbbb => { dbbb.Delete(testObjects[1]); });
            });

            Assert.AreEqual(testObjects.Count - 2, db.Table<TestObj>().Count());
        }

        [Test]
        public void SuccessfulSavepointTransaction()
        {
            db.RunInTransaction(dbb =>
            {
                dbb.Delete(testObjects[0]);
                dbb.Delete(testObjects[1]);
                dbb.Insert(new TestObj());
            });

            Assert.AreEqual(testObjects.Count - 1, db.Table<TestObj>().Count());
        }

        
        [Test]
        public void FailSavepointTransactionException()
        {
            try
            {
                db.RunInTransaction(dbb =>
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
            db.RunInTransaction(dbb =>
            {
                dbb.InsertOrReplaceAll(testObjects);
            });
        }
    }
}