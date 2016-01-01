using System.Collections.Generic;
using System.Reflection;
using System.ArrayExtensions;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Linq;

namespace System
{
    public static class ObjectExtensions
    {
        public static T Copy<T>(this T original)
        {
            return (T)new DeepCopyContext().InternalCopy(original);
        }

        private class DeepCopyContext
        {
            private static readonly Func<object, object> CloneMethod;
            private readonly Dictionary<Object, Object> m_Visited;
            private readonly Dictionary<Type, FieldInfo[]> m_NonShallowFieldCache;

            static DeepCopyContext()
            {
                MethodInfo cloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
                var p1 = Expression.Parameter(typeof(object));
                var body = Expression.Call(p1, cloneMethod);
                CloneMethod = Expression.Lambda<Func<object, object>>(body, p1).Compile();
                //Console.WriteLine("typeof(object) contains {0} nonshallow fields", NonShallowFields(typeof(object)).Count());
            }

            public DeepCopyContext()
            {
                m_Visited = new Dictionary<object, object>(new ReferenceEqualityComparer());
                m_NonShallowFieldCache = new Dictionary<Type, FieldInfo[]>();
            }

            private static bool IsPrimitive(Type type)
            {
                if (type.IsValueType && type.IsPrimitive) return true;
                if (type == typeof(String)) return true;
                if (type == typeof(Decimal)) return true;
                if (type == typeof(DateTime)) return true;
                return false;
            }

            public Object InternalCopy(Object originalObject)
            {
                if (originalObject == null) return null;
                var typeToReflect = originalObject.GetType();
                if (IsPrimitive(typeToReflect)) return originalObject;
                if (typeof(XElement).IsAssignableFrom(typeToReflect)) return new XElement(originalObject as XElement);
                if (m_Visited.ContainsKey(originalObject)) return m_Visited[originalObject];
                if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;

                var cloneObject = CloneMethod(originalObject);
                m_Visited.Add(originalObject, cloneObject);

                if (typeToReflect.IsArray)
                {
                    var arrayType = typeToReflect.GetElementType();
                    if (IsPrimitive(arrayType) == false)
                    {
                        Array clonedArray = (Array)cloneObject;
                        clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices)), indices));
                    }
                }

                foreach (var fieldInfo in CachedNonShallowFields(typeToReflect))
                {
                    var originalFieldValue = fieldInfo.GetValue(originalObject);
                    var clonedFieldValue = InternalCopy(originalFieldValue);
                    fieldInfo.SetValue(cloneObject, clonedFieldValue);
                }

                return cloneObject;
            }

            private FieldInfo[] CachedNonShallowFields(Type typeToReflect)
            {
                FieldInfo[] result;

                if(!m_NonShallowFieldCache.TryGetValue(typeToReflect,out result))
                {
                    result = NonShallowFields(typeToReflect).ToArray();
                    m_NonShallowFieldCache[typeToReflect] = result;
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
                if (typeToReflect != typeof(object))
                {
                    // this loop will yield all protected and public fields of the flattened type hierarchy, and the private fields of the type itself.
                    foreach (FieldInfo fieldInfo in typeToReflect.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
                    {
                        if (IsPrimitive(fieldInfo.FieldType)) continue; // this is 5% faster than a where clause..
                        yield return fieldInfo;
                    }

                    // so now what's left to yield: the private fields of the base types
                    while (typeToReflect.BaseType != typeof(object))          // this is 10% faster than checking against null
                    {
                        typeToReflect = typeToReflect.BaseType;

                        foreach (FieldInfo fieldInfo in typeToReflect.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        {
                            if (!fieldInfo.IsPrivate) continue;        // skip the protected fields, we already yielded those above.
                            if (IsPrimitive(fieldInfo.FieldType)) continue;
                            yield return fieldInfo;
                        }
                    }
                }
            }
        }
    }

    public class ReferenceEqualityComparer : EqualityComparer<Object>
    {
        public override bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public override int GetHashCode(object obj)
        {
            if (obj == null) return 0;
            // The RuntimeHelpers.GetHashCode method always calls the Object.GetHashCode method non-virtually, 
            // even if the object's type has overridden the Object.GetHashCode method.
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    namespace ArrayExtensions
    {
        public static class ArrayExtensions
        {
            public static void ForEach(this Array array, Action<Array, int[]> action)
            {
                if (array.LongLength == 0) return;
                ArrayTraverse walker = new ArrayTraverse(array);
                do action(array, walker.Position);
                while (walker.Step());
            }
        }

        internal class ArrayTraverse
        {
            public int[] Position;
            private int[] maxLengths;

            public ArrayTraverse(Array array)
            {
                maxLengths = new int[array.Rank];
                for (int i = 0; i < array.Rank; ++i)
                {
                    maxLengths[i] = array.GetLength(i) - 1;
                }
                Position = new int[array.Rank];
            }

            public bool Step()
            {
                for (int i = 0; i < Position.Length; ++i)
                {
                    if (Position[i] < maxLengths[i])
                    {
                        Position[i]++;
                        for (int j = 0; j < i; j++)
                        {
                            Position[j] = 0;
                        }
                        return true;
                    }
                }
                return false;
            }
        }
    }

}
