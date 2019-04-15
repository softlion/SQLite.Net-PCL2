//
// Copyright (c) 2012 Krueger Systems, Inc.
// Copyright (c) 2013 Øystein Krog (oystein.krog@gmail.com)
// Copyright (c) 2014 Benjamin Mayrargue (softlion@softlion.com)
//   Fix for support of multiple primary keys
//   Support new types: TimeSpan, DateTimeOffset, XElement
//   (breaking change) Fix i18n string support: stored as i18n string (nvarchar) instead of language dependant string (varchar)
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
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SQLite.Net.Attributes;
using NotNullAttribute = SQLite.Net.Attributes.NotNullAttribute;

namespace SQLite.Net
{
    internal static class Orm
    {
        public const string ImplicitPkName = "Id";
        public const string ImplicitIndexSuffix = "Id";

		private static IColumnInformationProvider _columnInformationProvider = new DefaultColumnInformationProvider();
		public static IColumnInformationProvider ColumnInformationProvider 
		{
			get { return _columnInformationProvider; }
			set { _columnInformationProvider = value; }
		}

        internal static string SqlDecl(TableMapping.Column p, bool storeDateTimeAsTicks, IBlobSerializer serializer,
            IDictionary<Type, string> extraTypeMappings)
        {
            //http://www.sqlite.org/lang_createtable.html
            return String.Format("\"{0}\" {1} {2} {3} {4} {5} ",
                p.Name,
                SqlType(p, storeDateTimeAsTicks, serializer, extraTypeMappings),
                p.IsAutoInc ? "primary key autoincrement" : null, //autoincrement can not be set with a multiple primary key
                !p.IsNullable ? "not null" : null,
                !string.IsNullOrEmpty(p.Collation) ? "collate " + p.Collation : null,
                p.DefaultValue != null ? "default('" + p.DefaultValue + "') " : null
                );
        }

        private static string SqlType(TableMapping.Column p, bool storeDateTimeAsTicks,
            IBlobSerializer serializer,
            IDictionary<Type, string> extraTypeMappings)
        {
            var clrType = p.ColumnType;
            var interfaces = clrType.GetTypeInfo().ImplementedInterfaces.ToList();

            string extraMapping;
            if (extraTypeMappings.TryGetValue(clrType, out extraMapping))
            {
                return extraMapping;
            }

            //http://www.sqlite.org/datatype3.html
            if (clrType == typeof (bool) || clrType == typeof (byte) || clrType == typeof (ushort) ||
                clrType == typeof (sbyte) || clrType == typeof (short) || clrType == typeof (int) ||
                clrType == typeof (uint) || clrType == typeof (long) ||
                interfaces.Contains(typeof (ISerializable<bool>)) ||
                interfaces.Contains(typeof (ISerializable<byte>)) ||
                interfaces.Contains(typeof (ISerializable<ushort>)) ||
                interfaces.Contains(typeof (ISerializable<sbyte>)) ||
                interfaces.Contains(typeof (ISerializable<short>)) ||
                interfaces.Contains(typeof (ISerializable<int>)) ||
                interfaces.Contains(typeof (ISerializable<uint>)) ||
                interfaces.Contains(typeof (ISerializable<long>)) ||
                interfaces.Contains(typeof (ISerializable<ulong>)))
            {
                return "integer";
            }
            if (clrType == typeof (float) || clrType == typeof (double) || clrType == typeof (decimal) ||
                interfaces.Contains(typeof (ISerializable<float>)) ||
                interfaces.Contains(typeof (ISerializable<double>)) ||
                interfaces.Contains(typeof (ISerializable<decimal>)))
            {
                return "real";
            }
            if (clrType == typeof (string) || interfaces.Contains(typeof (ISerializable<string>))
            || clrType == typeof(XElement) || interfaces.Contains(typeof (ISerializable<XElement>))
            )
            {
                return "text";
            }
            if (clrType == typeof (TimeSpan) || interfaces.Contains(typeof (ISerializable<TimeSpan>)))
            {
                return "integer";
            }
            if (clrType == typeof (DateTime) || interfaces.Contains(typeof (ISerializable<DateTime>)))
            {
                return storeDateTimeAsTicks ? "integer" : "numeric";
            }
            if (clrType == typeof (DateTimeOffset) || interfaces.Contains(typeof (ISerializable<DateTimeOffset>)))
            {
                return "integer";
            }
            if (clrType.GetTypeInfo().IsEnum)
            {
                return "integer";
            }
            if (clrType == typeof (byte[]) || interfaces.Contains(typeof (ISerializable<byte[]>)))
            {
                return "blob";
            }
            if (clrType == typeof (Guid) || interfaces.Contains(typeof (ISerializable<Guid>)))
            {
                return "text";
            }
            if (serializer != null && serializer.CanDeserialize(clrType))
            {
                return "blob";
            }
            throw new NotSupportedException("Don't know about " + clrType);
        }

        internal static bool IsPK(MemberInfo p)
        {
			return ColumnInformationProvider.IsPK (p);
        }

        internal static string Collation(MemberInfo p)
        {
			return ColumnInformationProvider.Collation (p);
        }

        internal static bool IsAutoInc(MemberInfo p)
        {
			return ColumnInformationProvider.IsAutoInc (p);
        }

        internal static IEnumerable<IndexedAttribute> GetIndices(MemberInfo p)
        {
			return ColumnInformationProvider.GetIndices (p);
        }

        internal static int? MaxStringLength(PropertyInfo p)
        {
			return ColumnInformationProvider.MaxStringLength (p);
        }

        internal static object GetDefaultValue(PropertyInfo p)
        {
			return ColumnInformationProvider.GetDefaultValue (p);
        }

        internal static bool IsMarkedNotNull(MemberInfo p)
        {
			return ColumnInformationProvider.IsMarkedNotNull (p);
        }
    }
}
