namespace Baksteen.Extensions.DeepCopy;

using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.Linq;
using System.Numerics;

public static class DeepCopyObjectExtensions
{
    public static T? DeepCopy<T>(this T? original)
    {
        return (T?)new DeepCopyContext().InternalCopy(original, true);
    }

    private class DeepCopyContext
    {
        private static readonly Func<object, object> _shallowClone;

        // Set of deeply immutable types. This includes all primitives, some known immutable
        // valuetypes, and a few sealed immutable reference types such as 'string', 'DBNull' and
        // 'Version'. Nullable<T> of an immutable valuetype T is itself immutable as well but rather
        // than duplicating all those entries here, they are added programmatically in the static
        // constructor below.
        private static readonly HashSet<Type> _immutableTypes = new()
        {
            typeof(nint),
            typeof(nuint),
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(char),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(Half),
            typeof(decimal),
            typeof(BigInteger),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Matrix3x2),
            typeof(Matrix4x4),
            typeof(Complex),
            typeof(Quaternion),
            typeof(Plane),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateOnly),
            typeof(TimeOnly),
            typeof(TimeSpan),
            typeof(DateTimeOffset),
            typeof(Range),
            typeof(Index),
            typeof(string),
            typeof(DBNull),
            typeof(Version),
            typeof(Uri),
        };

        // to handle object graphs containing cycles, _visited keeps track of instances we've already cloned
        private readonly Dictionary<object, object> _visited = new(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<Type, FieldInfo[]> _nonShallowFieldCache = new();

        static DeepCopyContext()
        {
            var cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var p1 = Expression.Parameter(typeof(object));
            var body = Expression.Call(p1, cloneMethod);
            _shallowClone = Expression.Lambda<Func<object, object>>(body, p1).Compile();

            // Nullable<T> of deeply immutable valuetypes are themselves deeply immutable
            foreach(var type in _immutableTypes.Where(t => t.IsValueType).ToList())
            {
                _immutableTypes.Add(typeof(Nullable<>).MakeGenericType(type));
            }
        }

        private static bool IsDeeplyImmutable(Type type)
        {
            // now that all primitives are included in the _immutableTypes set, the 'type.IsPrimitive' test is not really
            // necessary, but I'll leave it in because it's a tiny bit faster than looking up items in the hashset.
            return type.IsPrimitive
                   || type.IsEnum
                   || _immutableTypes.Contains(type);
        }

        public object? InternalCopy(object? originalObject, bool includeInObjectGraph)
        {
            if(originalObject == null) return null;

            var typeToReflect = originalObject.GetType();
            if(IsDeeplyImmutable(typeToReflect) || originalObject is Type) return originalObject;

            if(typeof(XElement).IsAssignableFrom(typeToReflect)) return new XElement((XElement)originalObject);
            if(typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;

            if(includeInObjectGraph)
            {
                if(_visited.TryGetValue(originalObject, out var result)) return result;
            }

            var cloneObject = _shallowClone(originalObject);

            if(includeInObjectGraph)
            {
                _visited.Add(originalObject, cloneObject);
            }

            if(typeToReflect.IsArray)
            {
                var arrayElementType = typeToReflect.GetElementType()!;

                if(IsDeeplyImmutable(arrayElementType))
                {
                    // for an array of primitives, do nothing. The shallow clone is enough.
                }
                else if(arrayElementType.IsValueType)
                {
                    // if its an array of structs, there's no need to check and add the individual elements to 'visited', because in .NET it's impossible to create
                    // references to individual array elements.
                    ReplaceArrayElements((Array)cloneObject, x => InternalCopy(x, false));
                }
                else
                {
                    // it's an array of ref types
                    ReplaceArrayElements((Array)cloneObject, x => InternalCopy(x, true));
                }
            }
            else
            {
                foreach(var fieldInfo in CachedNonShallowFields(typeToReflect))
                {
                    var originalFieldValue = fieldInfo.GetValue(originalObject);
                    // a valuetype field can never have a reference pointing to it, so don't check the object graph in that case
                    var clonedFieldValue = InternalCopy(originalFieldValue, !fieldInfo.FieldType.IsValueType);
                    fieldInfo.SetValue(cloneObject, clonedFieldValue);
                }
            }

            return cloneObject;
        }

        private static void ReplaceArrayElements(Array array, Func<object?, object?> func, int dimension, int[] counts, int[] indices)
        {
            int len = counts[dimension];

            if(dimension < (counts.Length - 1))
            {
                // not the final dimension, loop the range, and recursively handle one dimension higher
                for(int t = 0; t < len; t++)
                {
                    indices[dimension] = t;
                    ReplaceArrayElements(array, func, dimension + 1, counts, indices);
                }
            }
            else
            {
                // we've reached the final dimension where the elements are closest together in memory. Do a final loop.
                for(int t = 0; t < len; t++)
                {
                    indices[dimension] = t;
                    array.SetValue(func(array.GetValue(indices)), indices);
                }
            }
        }

        private static void ReplaceArrayElements(Array array, Func<object?, object?> func)
        {
            if(array.Rank == 1)
            {
                // do a fast loop for the common case, a one dimensional array
                int len = array.GetLength(0);
                for(int t = 0; t < len; t++)
                {
                    array.SetValue(func(array.GetValue(t)), t);
                }
            }
            else
            {
                // multidimensional array: recursively loop through all dimensions, starting with dimension zero.
                var counts = Enumerable.Range(0, array.Rank).Select(array.GetLength).ToArray();
                var indices = new int[array.Rank];
                ReplaceArrayElements(array, func, 0, counts, indices);
            }
        }

        private FieldInfo[] CachedNonShallowFields(Type typeToReflect)
        {
            if(!_nonShallowFieldCache.TryGetValue(typeToReflect, out var result))
            {
                result = NonShallowFields(typeToReflect).ToArray();
                _nonShallowFieldCache[typeToReflect] = result;
            }
            return result;
        }

        /// <summary>
        /// From the given type hierarchy (i.e. including all base types), return all fields that should be deep-copied
        /// </summary>
        /// <param name="typeToReflect"></param>
        /// <returns></returns>
        private static IEnumerable<FieldInfo> NonShallowFields(Type typeToReflect)
        {
            while(typeToReflect.BaseType != null)
            {
                foreach(var fieldInfo in typeToReflect.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if(IsDeeplyImmutable(fieldInfo.FieldType)) continue; // this is 5% faster than a where clause..
                    yield return fieldInfo;
                }
                typeToReflect = typeToReflect.BaseType;
            }
        }
    }
}
