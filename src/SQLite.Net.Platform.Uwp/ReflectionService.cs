using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using SQLite.Net.Interop;

namespace SQLite.Net.Platform.NetCore
{
    public class ReflectionService : IReflectionService
    {
        public IEnumerable<PropertyInfo> GetPublicInstanceProperties(Type mappedType)
        {
            return mappedType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }

        public object GetMemberValue(object obj, Expression expr, MemberInfo member)
        {
            if (member is PropertyInfo pi)
            {
                return pi.GetValue(obj, null);
            }
            if (member is FieldInfo fi)
            {
                return fi.GetValue(obj);
            }

            throw new NotSupportedException("MemberExpr: " + member.GetType().Name);
        }
    }
}