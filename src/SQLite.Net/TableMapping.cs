//
// Copyright (c) 2012 Krueger Systems, Inc.
// Copyright (c) 2013 Øystein Krog (oystein.krog@gmail.com)
// Copyright (c) 2014 Benjamin Mayrargue
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
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SQLite.Net2
{
    public class TableMapping
    {
        private readonly Column _autoPk;
        private Column[] _insertColumns, _insertOrReplaceColumns;


        public TableMapping(
            Type type,
            IEnumerable<MemberInfo> properties,
            CreateFlags createFlags = CreateFlags.None,
            IColumnInformationProvider? infoProvider = null)
        {
            infoProvider ??= new DefaultColumnInformationProvider();

            MappedType = type;

            var tableAttr = type.GetTypeInfo().GetCustomAttributes<TableAttribute>().FirstOrDefault();

            TableName = tableAttr != null ? tableAttr.Name : MappedType.Name;

            var props = properties;

            var cols = new List<Column>();
            foreach (var p in props)
            {
                var ignore = infoProvider.IsIgnored(p);
                if (ignore) continue;

                var memberType = infoProvider.GetMemberType(p);
                // Check if this is a ValueTuple<...>
                if (memberType.GetInterface(nameof(ITuple)) != null && memberType.IsValueType)
                {
                    // If it is, create a column per member of the value tuple.
                    var args = memberType.GetGenericArguments();
                    for (var i = 0; i < args.Length; ++i)
                    {
                        var tupleElementType = args[i];
                        if (tupleElementType.GetInterface(nameof(ITuple)) != null)
                        {
                            throw new NotSupportedException("Nested tuple types are not supported.");
                        }
                        
                        cols.Add(new Column(
                            type,
                            p,
                            createFlags,
                            infoProvider,
                            i,
                            tupleElementType));
                    }
                }
                else
                {
                    cols.Add(new Column(
                        type,
                        p,
                        createFlags,
                        infoProvider));
                }
            }

            Columns = cols.ToArray();
            foreach (var c in Columns)
            {
                if (c.IsAutoInc && c.IsPK)
                    _autoPk = c;
                if (c.IsPK)
                    PKs.Add(c);
            }

            HasAutoIncPK = _autoPk != null;

            if (PK != null)
            {
                GetByPrimaryKeySql = $"select * from \"{TableName}\" where \"{PK.Name}\" = ?";
                PkWhereSql = PKs.Aggregate(new StringBuilder(),
                    (sb, pk) => sb.AppendFormat(" \"{0}\" = ? and", pk.Name),
                    sb => sb.Remove(sb.Length - 3, 3).ToString());
                GetByPrimaryKeysSql = $"select * from \"{TableName}\" where {PkWhereSql}";
            }
            else
            {
                // People should not be calling Get/Find without a PK
                GetByPrimaryKeysSql = GetByPrimaryKeySql = $"select * from \"{TableName}\" limit 1";
            }
        }


        public string PkWhereSqlForPartialKeys(int numberOfKeys)
        {
            if (numberOfKeys == PKs.Count)
                return PkWhereSql;

            return PKs.Take(numberOfKeys).Aggregate(new StringBuilder(),
                (sb, pk) => sb.AppendFormat(" \"{0}\" = ? and", pk.Name), sb => sb.Remove(sb.Length - 3, 3).ToString());
        }


        public string GetByPrimaryKeysSqlForPartialKeys(int numberOfKeys)
            => $"select * from \"{TableName}\" where {PkWhereSqlForPartialKeys(numberOfKeys)}";

        public readonly List<Column> PKs = new();


        public Type MappedType { get; }
        public string TableName { get; }
        public Column[] Columns { get; }
        public Column PK => PKs.FirstOrDefault();
        public string GetByPrimaryKeySql { get; }
        public string GetByPrimaryKeysSql { get; }
        public string PkWhereSql { get; }
        public bool HasAutoIncPK { get; }

        public Column[] InsertColumns => _insertColumns ??= Columns.Where(c => !c.IsAutoInc).ToArray();
        public Column[] InsertOrReplaceColumns => _insertOrReplaceColumns ??= Columns.ToArray();


        public void SetAutoIncPK(object obj, long id)
            => _autoPk?.SetValue(obj, Convert.ChangeType(id, _autoPk.ColumnType, null));


        public Column FindColumnWithPropertyName(string propertyName)
            => Columns.FirstOrDefault(c => c.PropertyName == propertyName);


        public Column FindColumn(string columnName)
            => Columns.FirstOrDefault(c => c.Name == columnName);

        public Column CreateColumn(Type columnType)
            => new Column { ColumnType = columnType };

        public class Column
        {
            private readonly MemberInfo _prop;
            private readonly IColumnInformationProvider _infoProvider;
            
            public Column()
            {
            }

            public Column(
                Type containedType,
                MemberInfo prop,
                CreateFlags createFlags,
                IColumnInformationProvider? infoProvider,
                int tupleElement = -1,
                Type? tupleElementType = null)
            {
                _infoProvider = infoProvider ?? new DefaultColumnInformationProvider();

                _prop = prop;
                Name = _infoProvider.GetColumnName(containedType, prop, tupleElement);

                var columnType = tupleElementType ?? _infoProvider.GetMemberType(prop);
                //If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
                ColumnType = Nullable.GetUnderlyingType(columnType) ?? columnType;
                Collation = Orm.Collation(prop);

                IsPK = Orm.IsPK(prop) ||
                       (((createFlags & CreateFlags.ImplicitPK) == CreateFlags.ImplicitPK) &&
                        string.Compare(prop.Name, Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase) == 0);

                var isAuto = Orm.IsAutoInc(prop) ||
                             (IsPK && ((createFlags & CreateFlags.AutoIncPK) == CreateFlags.AutoIncPK));
                IsAutoGuid = isAuto && ColumnType == typeof(Guid);
                IsAutoInc = isAuto && !IsAutoGuid;

                DefaultValue = Orm.GetDefaultValue(prop);

                Indices = Orm.GetIndices(prop);
                if (!Indices.Any()
                    && !IsPK
                    && ((createFlags & CreateFlags.ImplicitIndex) == CreateFlags.ImplicitIndex)
                    && Name.EndsWith(Orm.ImplicitIndexSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    Indices = new[] { new IndexedAttribute() };
                }

                IsNullable = !(IsPK || Orm.IsMarkedNotNull(prop));
                MaxStringLength = Orm.MaxStringLength(prop);
                TupleElement = tupleElement;
            }


            public string Name { get; }
            public string PropertyName => _prop.Name;
            public Type ColumnType { get; internal set; }
            public string Collation { get; }
            public bool IsAutoInc { get; }
            public bool IsAutoGuid { get; }
            public bool IsPK { get; }
            public IEnumerable<IndexedAttribute> Indices { get; set; }
            public bool IsNullable { get; }
            public int? MaxStringLength { get; }
            public object DefaultValue { get; }
            public int TupleElement { get; }

            /// <summary>
            ///     Set column value.
            /// </summary>
            /// <param name="obj"></param>
            /// <param name="val"></param>
            public void SetValue(object obj, object val)
            {
                var propType = _infoProvider.GetMemberType(_prop);
                var typeInfo = propType.GetTypeInfo();
                object valueToSet;

                if (typeInfo.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var typeCol = propType.GetTypeInfo().GenericTypeArguments;
                    var nullableType = typeCol[0];
                    var baseType = nullableType.GetTypeInfo().BaseType;
                    if (baseType == typeof(Enum))
                    {
                        valueToSet = AsEnumValue(obj, nullableType, val);
                    }
                    else
                    {
                        valueToSet = val;
                    }
                }
                else if (typeInfo.BaseType == typeof(Enum))
                {
                    valueToSet = AsEnumValue(obj, propType, val);
                }
                else
                {
                    valueToSet = val;
                }

                // If we're a value tuple then we need to recreate the tuple with the new value and set that
                // on the property.
                if (TupleElement != -1)
                {
                    var tupleObj = (ITuple)_infoProvider.GetValue(_prop, obj);
                    var args = new object[tupleObj.Length];
                    for (var i = 0; i < tupleObj.Length; ++i)
                    {
                        args[i] = i == TupleElement ? valueToSet : tupleObj[i];
                    }

                    valueToSet = Activator.CreateInstance(_infoProvider.GetMemberType(_prop), args);
                }

                switch (_prop)
                {
                    case PropertyInfo pi:
                        pi.SetValue(obj, valueToSet);
                        break;
                    case FieldInfo fi:
                        fi.SetValue(obj, valueToSet);
                        break;
                }
            }

            private object AsEnumValue(object obj, Type type, object? value)
            {
                var result = value ?? 0;
                return Enum.ToObject(type, result);
            }

            public object GetValue(object obj)
            {
                var result = _infoProvider.GetValue(_prop, obj);
                
                // If we're a value tuple then we need to get the nested value in the tuple.
                if (TupleElement != -1 && result is ITuple tuple)
                {
                    return tuple[TupleElement];
                }

                return result;
            }
        }
    }
}
