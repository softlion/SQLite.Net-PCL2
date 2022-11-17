using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

using System;
using System.Reflection;

namespace SQLite.Net2.Tests 
{
    [TestFixture]
    public class IgnoredTest : BaseTest
    {
        public class DummyClass
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public string Foo { get; set; }
            public string Bar { get; set; }

            [Ignore]
            public List<string> Ignored { get; set; }
        }

		private class TestIgnoreAttribute : Attribute
		{
		}

		public class TestColumnInformationProvider : IColumnInformationProvider
		{
			public string GetColumnName(MemberInfo p)
			{
				var colAttr = p.GetCustomAttributes<ColumnAttribute>(true).FirstOrDefault();
				return colAttr == null ? p.Name : colAttr.Name;
			}

			public Type GetMemberType(MemberInfo m)
			{
				return m switch
				{
					PropertyInfo p => p.PropertyType,
					FieldInfo f => f.FieldType,
					_ => throw new NotSupportedException($"{m.GetType()} is not supported.")
				};
			}

			public object GetValue(MemberInfo m, object obj)
			{
				return m switch
				{
					PropertyInfo p => p.GetValue(obj),
					FieldInfo f => f.GetValue(obj),
					_ => throw new NotSupportedException($"{m.GetType()} is not supported.")
				};
			}

			public bool IsIgnored(MemberInfo p)
			{
				return p.IsDefined(typeof (TestIgnoreAttribute), true);
			}

			public IEnumerable<IndexedAttribute> GetIndices(MemberInfo p)
			{
				return p.GetCustomAttributes<IndexedAttribute>();
			}

			public bool IsPK(MemberInfo m)
			{
				return m.GetCustomAttributes<PrimaryKeyAttribute>().Any();
			}
			public string Collation(MemberInfo m)
			{
				return string.Empty;
			}
			public bool IsAutoInc(MemberInfo m)
			{
				return false;
			}
			public int? MaxStringLength(MemberInfo p)
			{
				return null;
			}
			public object GetDefaultValue(MemberInfo p)
			{
				return null;
			}
			public bool IsMarkedNotNull(MemberInfo p)
			{
				return false;
			}
		}

		public abstract class TestObjBase<T>
		{
			[AutoIncrement, PrimaryKey]
			public int Id { get; set; }

			public T Data { get; set; }

		}

		public class TestObjIntWithIgnore : TestObjBase<int>
		{
			[TestIgnore]
			public List<string> Ignored { get; set; }
		}

        [Test]
        public void NullableFloat()
        {
            var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
            // if the Ignored property is not ignore this will cause an exception
            db.CreateTable<DummyClass>();
        }

		[Test]
		public void CustomIgnoreAttributeTest()
		{
			var db = new SQLiteConnection(TestPath.CreateTemporaryDatabase());
			db.ColumnInformationProvider = new TestColumnInformationProvider();
			// if the Ignored property is not ignore this will cause an exception
			db.CreateTable<TestObjIntWithIgnore>();
			db.ColumnInformationProvider = null;
		}
    }
}
