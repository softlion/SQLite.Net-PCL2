//
// Copyright (c) 2013 Øystein Krog (oystein.krog@gmail.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLite.Net2
{
    public static class ReflectionService
    {
        /// <summary>
        /// Returns the set of public non-static non-initonly fields followed by the public non-static properties with
        /// public get and set methods.
        ///
        /// The ordering of the returned members is as follows:
        /// First by if it's a primary key or not, then by if it's a field or a property, and finally alphabetically.
        /// </summary>
        public static IEnumerable<MemberInfo> GetPublicInstanceProperties(Type mappedType, IColumnInformationProvider provider)
        {
            var properties = mappedType.GetTypeInfo().GetRuntimeProperties()
                .Where(p => p.CanRead && p.CanWrite && p.GetMethod.IsPublic && p.SetMethod.IsPublic &&
                            !p.GetMethod.IsStatic && !p.SetMethod.IsStatic);
            var fields = mappedType.GetTypeInfo().GetRuntimeFields()
                .Where(f => f.IsPublic && !f.IsStatic && !f.IsInitOnly);

            var members = fields
                .Union<MemberInfo>(properties).ToList();
            members.Sort((l, r) =>
            {
                var lPk = provider.IsPK(l);
                var rPk = provider.IsPK(r);

                switch (lPk)
                {
                    case true when !rPk:
                        return -1;
                    case false when rPk:
                        return 1;
                    default:
                    {
                        if (l is FieldInfo && r is not FieldInfo)
                        {
                            return -1;
                        }

                        if (r is FieldInfo && l is not FieldInfo)
                        {
                            return 1;
                        }

                        return StringComparer.InvariantCulture.Compare(l.Name, r.Name);
                    }
                }
            });
            
            return members;
        }

        public static object GetMemberValue(object obj, Expression expr, MemberInfo member)
        {
            if (member is PropertyInfo pi)
                return pi.GetValue(obj, null);
            if (member is FieldInfo fi)
                return fi.GetValue(obj);
            throw new NotSupportedException("MemberExpr: " + member.GetType().Name);
        }
    }
}
