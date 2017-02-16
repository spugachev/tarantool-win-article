using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ProGaudi.Tarantool.Client;
using ProGaudi.Tarantool.Client.Model;
using ProGaudi.Tarantool.Client.Model.Enums;
using ProGaudi.Tarantool.Client.Model.Responses;

namespace ConsoleApp.NonProduction
{
    public static class TarantoolExtensions
    {
        public static BoxWrapper Wrap(this Box box)
        {
            return new BoxWrapper(box);
        }
    }

    public class BoxWrapper
    {
        private Box _box;
        private ISchema _schema;
        private Dictionary<string, SpaceWrapper> _spaces;

        public BoxWrapper(Box box)
        {
            _box = box;
            _spaces = new Dictionary<string, SpaceWrapper>();
        }

        internal ISchema GetSchema()
        {
            if (_schema != null) return _schema;

            _schema = _box.GetSchema();
            return _schema;
        }

        public SpaceWrapper Space(string name)
        {
            SpaceWrapper spaceWrapper = null;

            if (!_spaces.TryGetValue(name, out spaceWrapper))
            {
                spaceWrapper = new SpaceWrapper(this, name);
                _spaces.Add(name, spaceWrapper);
            }

            return spaceWrapper;
        }
    }

    public class SpaceWrapper
    {
        private ISpace _space;
        private BoxWrapper _boxWrapper;
        private Dictionary<string, IndexWrapper> _indexes;

        public string Name { get; private set; }

        public SpaceWrapper(BoxWrapper boxWrapper, string name)
        {
            _boxWrapper = boxWrapper;
            _indexes = new Dictionary<string, IndexWrapper>();
            Name = name;
        }

        internal async Task<ISpace> GetSpace()
        {
            if (_space != null) return _space;

            _space = await _boxWrapper.GetSchema().GetSpace(Name);
            return _space;
        }

        public IndexWrapper Index(string name)
        {
            IndexWrapper indexWrapper = null;

            if (!_indexes.TryGetValue(name, out indexWrapper))
            {
                indexWrapper = new IndexWrapper(this, name);
                _indexes.Add(name, indexWrapper);
            }

            return indexWrapper;
        }
    }

    public class IndexWrapper
    {
        private IIndex _index;

        private SpaceWrapper _spaceWrapper;

        public string Name { get; private set; }

        public IndexWrapper(SpaceWrapper spaceWrapper, string name)
        {
            _spaceWrapper = spaceWrapper;
            Name = name;
        }

        internal async Task<IIndex> GetIndex()
        {
            if (_index != null) return _index;

            var space = await _spaceWrapper.GetSpace();
            _index = await space.GetIndex(Name);

            return _index;
        }

        public async Task<T[]> Select<T>(object key = null, Iterator iterator = Iterator.Eq, uint limit = uint.MaxValue, uint offset = 0)
           where T : class
        {
            var selectOptions = new SelectOptions
            {
                Iterator = iterator,
                Limit = limit,
                Offset = offset
            };

            return await Select<T>(key, selectOptions);
        }

        public async Task<T[]> Select<T>(object key, SelectOptions selectOptions)
           where T : class
        {
            selectOptions = selectOptions ?? new SelectOptions();

            if (key == null)
            {
                var props = TarantoolHelper.GetProperties(typeof(T));
                var indexType = props[0].PropertyType;
                if (TarantoolHelper.IsNumericType(indexType))
                {
                    key = -1L;
                }
                else
                {
                    key = String.Empty;
                }

                selectOptions.Iterator = Iterator.All;
            }

            var keyTuple = key as ITarantoolTuple;
            if (keyTuple == null)
            {
                var keyType = key.GetType();
                var tupleType = typeof(TarantoolTuple<>).MakeGenericType(new[] { keyType });
                keyTuple = (ITarantoolTuple)Activator.CreateInstance(tupleType, new object[] { key });
            }

            var index = await GetIndex();
            var retValue = await TarantoolHelper.Select<T>(index, keyTuple, selectOptions);

            return retValue;
        }
    }

    public static class TarantoolHelper
    {
        public static async Task<T[]> Select<T>(IIndex index,
           object key, SelectOptions selectOptions = null)
           where T : class
        {
            if (selectOptions == null)
            {
                selectOptions = new SelectOptions { Iterator = Iterator.All };
            }

            var props = GetProperties(typeof(T));
            var tupleType = GetTarantoolTupleType(props);

            var method = index.GetType().GetMethod("Select");
            var genericMethod = method.MakeGenericMethod(new Type[] {
                key.GetType(), tupleType });

            var task = genericMethod.Invoke(index, new object[] { key, selectOptions }) as Task;
            var retValue = await ConvertResult<T>(task, tupleType, props);

            return retValue.ToArray();
        }

        public static PropertyInfo[] GetProperties(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            var props = typeInfo.GetProperties();

            return props;
        }

        public static Type GetTarantoolTupleType(PropertyInfo[] props)
        {
            Type type = null;

            switch (props.Length)
            {
                case 1: type = typeof(TarantoolTuple<>); break;
                case 2: type = typeof(TarantoolTuple<,>); break;
                case 3: type = typeof(TarantoolTuple<,,>); break;
                case 4: type = typeof(TarantoolTuple<,,,>); break;
                case 5: type = typeof(TarantoolTuple<,,,,>); break;
                case 6: type = typeof(TarantoolTuple<,,,,,>); break;
                case 7: type = typeof(TarantoolTuple<,,,,,,>); break;
                case 8: type = typeof(TarantoolTuple<,,,,,,,>); break;
            }

            var typeArgs = props.Select(c => c.PropertyType).ToArray();
            var makeType = type.MakeGenericType(typeArgs);

            return makeType;
        }

        public static async Task<List<T>> ConvertResult<T>(Task task, Type tupleType, PropertyInfo[] props)
        {
            await task;

            var type = typeof(T);
            var arrType = Array.CreateInstance(tupleType, 0).GetType();
            var respType = typeof(DataResponse<>).MakeGenericType(new[] { arrType });
            var taskType = typeof(Task<>).MakeGenericType(new[] { respType });
            var result = taskType.GetProperty("Result").GetValue(task);
            var data = respType.GetProperty("Data").GetValue(result);

            List<T> retValue = new List<T>();
            var arr = data as Array;

            foreach (var elem in arr)
            {
                T obj = (T)Activator.CreateInstance(type);

                for (var i = 0; i < props.Length; i++)
                {
                    var prop = props[i];
                    var val = tupleType.GetProperty("Item" + (i + 1)).GetValue(elem);
                    prop.SetValue(obj, Convert.ChangeType(val, prop.PropertyType), null);
                }

                retValue.Add(obj);
            }

            return retValue;
        }

        public static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}