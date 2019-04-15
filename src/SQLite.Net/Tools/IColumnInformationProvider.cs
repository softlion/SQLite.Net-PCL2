using System.Reflection;
using System.Collections.Generic;

namespace SQLite.Net2
{
	public interface IColumnInformationProvider
	{
		bool IsPK(MemberInfo m);
		string Collation(MemberInfo m);
		bool IsAutoInc(MemberInfo m);
		int? MaxStringLength(PropertyInfo p);
		IEnumerable<IndexedAttribute> GetIndices(MemberInfo p);
		object GetDefaultValue(PropertyInfo p);
		bool IsMarkedNotNull(MemberInfo p);
		bool IsIgnored(PropertyInfo p);
		string GetColumnName(PropertyInfo p);
	}
}

