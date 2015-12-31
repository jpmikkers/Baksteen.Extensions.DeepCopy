using System.Collections.Generic;
using System.Reflection;
using System.ArrayExtensions;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace System
{
    public static class ObjectExtensions
    {
        private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsPrimitive(this Type type)
        {
            if (type == typeof(String)) return true;
            return (type.IsValueType & type.IsPrimitive);
        }

        public static Object Copy(this Object originalObject)
        {
            return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
        }

        private static Object InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
        {
            if (originalObject == null) return null;
            var typeToReflect = originalObject.GetType();
            if (IsPrimitive(typeToReflect)) return originalObject;
            if (typeof(XElement).IsAssignableFrom(typeToReflect)) return new XElement(originalObject as XElement);
            if (visited.ContainsKey(originalObject)) return visited[originalObject];
            if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;

            var cloneObject = CloneMethod.Invoke(originalObject, null);
            visited.Add(originalObject, cloneObject);

            if (typeToReflect.IsArray)
            {
                var arrayType = typeToReflect.GetElementType();
                if (IsPrimitive(arrayType) == false)
                {
                    Array clonedArray = (Array)cloneObject;
                    clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                }
            }

            foreach (var fieldInfo in NonShallowFields(typeToReflect))
            {
                var originalFieldValue = fieldInfo.GetValue(originalObject);
                var clonedFieldValue = InternalCopy(originalFieldValue,visited);
                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }

            return cloneObject;
        }

        public static T Copy<T>(this T original)
        {
            return (T)Copy((Object)original);
        }

        /// <summary>
        /// From the given type hierarchy (i.e. including all base types), return all fields that should be deep-copied
        /// </summary>
        /// <param name="typeToReflect"></param>
        /// <returns></returns>
        private static IEnumerable<FieldInfo> NonShallowFields(Type typeToReflect)
        {
            // this loop will yield all protected and public fields of the flattened type hierarchy, and the private fields of the type itself.
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                if (IsPrimitive(fieldInfo.FieldType)) continue; // this is 5% faster than a where clause..
                yield return fieldInfo;
            }

            // so now what's left to yield: the private fields of the base types
            while (typeToReflect.BaseType != null)          // TODO: test whether comparing to typeof(object) is enough, it's faster
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
