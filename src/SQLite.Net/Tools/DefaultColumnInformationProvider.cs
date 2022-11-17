using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace SQLite.Net2
{
	public class DefaultColumnInformationProvider : IColumnInformationProvider
	{
		#region IColumnInformationProvider implementation

		public string GetColumnName(MemberInfo p)
		{
			var colAttr = p.GetCustomAttributes<ColumnAttribute>(true).FirstOrDefault();
			return colAttr == null ? p.Name : colAttr.Name;
		}

		public bool IsIgnored(MemberInfo p)
		{
			return p.IsDefined(typeof (IgnoreAttribute), true);
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
			foreach (var attribute in m.GetCustomAttributes<CollationAttribute>())
			{
				return attribute.Value;
			}
			return string.Empty;
		}

		public bool IsAutoInc(MemberInfo m)
		{
			return m.GetCustomAttributes<AutoIncrementAttribute>().Any();
		}

		public int? MaxStringLength(MemberInfo p)
		{
			foreach (var attribute in p.GetCustomAttributes<MaxLengthAttribute>())
			{
				return attribute.Value;
			}
			return null;
		}

		public object GetDefaultValue(MemberInfo p)
		{
			foreach (var attribute in p.GetCustomAttributes<DefaultAttribute>())
			{
				try
				{
					if (!attribute.UseProperty)
					{
						return Convert.ChangeType(attribute.Value, GetMemberType(p));
					}

					var obj = Activator.CreateInstance(p.DeclaringType);
					return GetValue(p, obj);
				}
				catch (Exception exception)
				{
					throw new Exception("Unable to convert " + attribute.Value + " to type " + GetMemberType(p), exception);
				}
			}
			return null;
		}

		public bool IsMarkedNotNull(MemberInfo p)
		{
			var attrs = p.GetCustomAttributes<NotNullAttribute>(true);
			return attrs.Any();
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
		#endregion
	}
}

