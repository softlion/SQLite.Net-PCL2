using System;
using System.Reflection;
using System.Collections.Generic;

namespace SQLite.Net2
{
	public interface IColumnInformationProvider
	{
		bool IsPK(MemberInfo m);
		string Collation(MemberInfo m);
		bool IsAutoInc(MemberInfo m);
		int? MaxStringLength(MemberInfo p);
		IEnumerable<IndexedAttribute> GetIndices(MemberInfo p);
		object GetDefaultValue(MemberInfo p);
		bool IsMarkedNotNull(MemberInfo p);
		bool IsIgnored(MemberInfo p);
		string GetColumnName(Type containedType, MemberInfo p, int tupleElementIndex);
		Type GetMemberType(MemberInfo m);
		object GetValue(MemberInfo m, object obj);
	}
}

