using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using Baksteen.Extensions.DeepCopy;
using System.Numerics;

namespace unittests
{
    [TestClass]
    public class ObjectExtensionsTests
    {
        private class MySingleObject : IEquatable<MySingleObject?>
        {
            public string One = "single one";
            private int two = 2;

            public int Two
            {
                get { return two; }
                set { two = value; }
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as MySingleObject);
            }

            public bool Equals(MySingleObject? other)
            {
                return other is not null &&
                       One == other.One &&
                       two == other.two &&
                       Two == other.Two;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(One, two, Two);
            }
        }

        private class MyNestedObject
        {
            public MySingleObject Single = new MySingleObject();
            public string Meta = "metadata";
        }

        private class OverriddenHash
        {
            public override int GetHashCode()
            {
                return 42;
            }
        }

        /// <summary>
        /// Encapsulates an object, the container will always be seen as a mutable ref type.
        /// Simplifies testing deepcopying.
        /// </summary>
        /// <typeparam name="T">Type to be encapsulated</typeparam>
        private class Wrapper<T> : IEquatable<Wrapper<T>>
        {
            public T Value { get; set; } = default!;

            public override bool Equals(object? obj)
            {
                return Equals(obj as Wrapper<T>);
            }

            public bool Equals(Wrapper<T>? other)
            {
                return other != null &&
                       EqualityComparer<T>.Default.Equals(Value, other.Value);
            }

            public override int GetHashCode()
            {
                return -1937169414 + EqualityComparer<T>.Default.GetHashCode(Value!);
            }

            public override string ToString()
            {
                return Value?.ToString() ?? "";
            }
        }

        [TestMethod]
        public void PrimitiveTest()
        {
            Assert.IsTrue(typeof(bool).IsPrimitive);
            Assert.IsTrue(typeof(byte).IsPrimitive);
            Assert.IsTrue(typeof(sbyte).IsPrimitive);
            Assert.IsTrue(typeof(char).IsPrimitive);
            Assert.IsTrue(typeof(short).IsPrimitive);
            Assert.IsTrue(typeof(ushort).IsPrimitive);
            Assert.IsTrue(typeof(int).IsPrimitive);
            Assert.IsTrue(typeof(uint).IsPrimitive);
            Assert.IsTrue(typeof(nint).IsPrimitive);
            Assert.IsTrue(typeof(nuint).IsPrimitive);
            Assert.IsTrue(typeof(long).IsPrimitive);
            Assert.IsTrue(typeof(ulong).IsPrimitive);
            Assert.IsTrue(typeof(float).IsPrimitive);
            Assert.IsTrue(typeof(double).IsPrimitive);
        }

        [TestMethod]
        public void NonPrimitiveTest()
        {
            Assert.IsFalse(typeof(object).IsPrimitive);
            Assert.IsFalse(typeof(string).IsPrimitive);
            Assert.IsFalse(typeof(Half).IsPrimitive);
            Assert.IsFalse(typeof(decimal).IsPrimitive);
            Assert.IsFalse(typeof(Complex).IsPrimitive);
            Assert.IsFalse(typeof(BigInteger).IsPrimitive);
            Assert.IsFalse(typeof(Guid).IsPrimitive);
            Assert.IsFalse(typeof(DateTime).IsPrimitive);
            Assert.IsFalse(typeof(DateOnly).IsPrimitive);
            Assert.IsFalse(typeof(TimeOnly).IsPrimitive);
            Assert.IsFalse(typeof(TimeSpan).IsPrimitive);
            Assert.IsFalse(typeof(DateTimeOffset).IsPrimitive);
            Assert.IsFalse(typeof(Range).IsPrimitive);
            Assert.IsFalse(typeof(Index).IsPrimitive);
        }

        [TestMethod]
        public void Copy_Null()
        {
            object? t1 = null;
            var t2 = t1.DeepCopy();
            Assert.IsNull(t2);
        }

        [TestMethod]
        public void Copy_ShouldNotDeepCopyTypeInstances()
        {
            var t1 = typeof(MySingleObject);
            var t2 = t1.DeepCopy()!;
            Assert.AreSame(t1, t2);

            t1 = typeof(MySingleObject).GetType();
            t2 = t1.DeepCopy()!;
            Assert.AreSame(t1, t2);
        }

        [TestMethod]
        public void Copy_ShouldNotDeepCopyTypeFields()
        {
            var t1 = new { TypeField = typeof(MySingleObject) };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            Assert.AreSame(t1.TypeField, t2.TypeField);
        }

        [TestMethod]
        public void Copy_ShouldNotDeepCopyImmutableTypes()
        {
            static void SubTest(object? v) => Assert.AreSame(v, v.DeepCopy());

            SubTest(new nint());
            SubTest(new nuint());
            SubTest(new bool());
            SubTest(new byte());
            SubTest(new sbyte());
            SubTest(new char());
            SubTest(new short());
            SubTest(new ushort());
            SubTest(new int());
            SubTest(new uint());
            SubTest(new long());
            SubTest(new ulong());
            SubTest(new float());
            SubTest(new double());
            SubTest(new Half());
            SubTest(new decimal());
            SubTest(new Complex());
            SubTest(new Quaternion());
            SubTest(new Vector2());
            SubTest(new Vector3());
            SubTest(new Vector4());
            SubTest(new Plane());
            SubTest(new Matrix3x2());
            SubTest(new Matrix4x4());
            SubTest(new BigInteger());
            SubTest(new Guid());
            SubTest(new DateTime());
            SubTest(new DateOnly());
            SubTest(new TimeOnly());
            SubTest(new TimeSpan());
            SubTest(new DateTimeOffset());
            SubTest(new Range());
            SubTest(new Index());
            SubTest(new string(""));
            SubTest(DBNull.Value);
            SubTest(new Version());
            SubTest(new Uri(@"http://localhost:80"));
        }

        [TestMethod]
        public void Copy_XElementWithChildren()
        {
            XElement el = XElement.Parse(@"
                <root>
                    <child attrib='wow'>hi</child>
                    <child attrib='yeah'>hello</child>
                </root>");
            XElement copied = el.DeepCopy()!;

            var children = copied.Elements("child").ToList();
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual("wow", children[0].Attribute("attrib")!.Value);
            Assert.AreEqual("hi", children[0].Value);

            Assert.AreEqual("yeah", children[1].Attribute("attrib")!.Value);
            Assert.AreEqual("hello", children[1].Value);
        }

        [TestMethod]
        public void Copy_CopiesNestedObject()
        {
            MyNestedObject copied = new MyNestedObject();

            Assert.AreEqual("metadata", copied.Meta);
            Assert.AreEqual("single one", copied.Single.One);
            Assert.AreEqual(2, copied.Single.Two);
        }

        [TestMethod]
        public void Copy_CopiesEnumerables()
        {
            IList<MySingleObject> list = new List<MySingleObject>()
            {
                new MySingleObject() {One = "1"},
                new MySingleObject() {One = "2"}
            };
            IList<MySingleObject> copied = list.DeepCopy()!;

            Assert.AreEqual(2, copied.Count);
            Assert.AreEqual("1", copied[0].One);
            Assert.AreEqual("2", copied[1].One);
        }

        [TestMethod]
        public void Copy_CopiesSingleObject()
        {
            MySingleObject copied = new MySingleObject().DeepCopy()!;

            Assert.AreEqual("single one", copied.One);
            Assert.AreEqual(2, copied.Two);
        }

        [TestMethod]
        public void Copy_CopiesSingleBuiltInObjects()
        {
            Assert.AreEqual("hello there", "hello there".DeepCopy());
            Assert.AreEqual(123, 123.DeepCopy());
        }

        [TestMethod]
        public void Copy_CopiesSelfReferencingArray()
        {
            object[] arr = new object[1];
            arr[0] = arr;
            var copy = arr.DeepCopy()!;
            Assert.ReferenceEquals(copy, copy[0]);
        }

        [TestMethod]
        public void ReferenceEqualityComparerShouldNotUseOverriddenHash()
        {
            var t = new OverriddenHash();
            var equalityComparer = ReferenceEqualityComparer.Instance;
            Assert.AreNotEqual(42, equalityComparer.GetHashCode(t));
            Assert.AreEqual(equalityComparer.GetHashCode(t), RuntimeHelpers.GetHashCode(t));
        }

        static IEnumerable<T> ToIEnumerable<T>(System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            while(enumerator.MoveNext())
            {
                yield return (T)enumerator.Current;
            }
        }

        static void AssertArraysAreEqual<T>(Array array1, Array array2, bool refsMustBeDifferent)
        {
            Assert.AreEqual(array1.GetType(), array2.GetType());
            Assert.AreEqual(array1.LongLength, array2.LongLength);

            var counts1 = Enumerable.Range(0, array1.Rank).Select(array1.GetLongLength).ToArray();
            var counts2 = Enumerable.Range(0, array2.Rank).Select(array2.GetLongLength).ToArray();

            foreach(var (First, Second) in counts1.Zip(counts2))
            {
                Assert.AreEqual(First, Second);
            }

            foreach(var (x, y) in ToIEnumerable<T>(array1).Zip(ToIEnumerable<T>(array2)))
            {
                Assert.AreEqual(x, y);
                if(refsMustBeDifferent) Assert.AreNotSame(x, y);
            }
        }

        [TestMethod]
        public void Copy_Copies1dArray()
        {
            var t1 = new int[] { 1, 2, 3 };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<int>(t1, t2, false);
        }

        [TestMethod]
        public void Copy_Copies1dRefElementArray()
        {
            var t1 = new Wrapper<int>[]
            {
                new Wrapper<int>{ Value = 1 } ,
                new Wrapper<int>{ Value = 2 } ,
                new Wrapper<int>{ Value = 3 } ,
            };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<Wrapper<int>>(t1, t2, refsMustBeDifferent: true);
        }

        [TestMethod]
        public void Copy_Copies2dArray()
        {
            var t1 = new int[,]
            {
                { 1, 2 },
                { 3, 4 },
                { 5, 6 },
            };

            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<int>(t1, t2, false);
        }

        [TestMethod]
        public void Copy_Copies2dRefElementArray()
        {
            var t1 = new Wrapper<int>[,]
            {
                { new Wrapper<int>{ Value = 1 } , new Wrapper<int>{ Value = 2 } },
                { new Wrapper<int>{ Value = 3 } , new Wrapper<int>{ Value = 4 } },
                { new Wrapper<int>{ Value = 5 } , new Wrapper<int>{ Value = 6 } },
            };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<Wrapper<int>>(t1, t2, refsMustBeDifferent: true);
        }

        [TestMethod]
        public void Copy_Copies3dArray()
        {
            var t1 = new int[,,]
            {
                {
                    { 1, 2 },
                    { 3, 4 },
                    { 5, 6 }
                },
                {
                    { 7, 8 },
                    { 9, 10 },
                    { 11, 12 }
                }
            };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<int>(t1, t2, false);
        }

        [TestMethod]
        public void Copy_Copies3dRefElementArray()
        {
            var t1 = new Wrapper<int>[,,]
            {
                {
                    { new Wrapper<int>{ Value = 1 } , new Wrapper<int>{ Value = 2 } },
                    { new Wrapper<int>{ Value = 3 } , new Wrapper<int>{ Value = 4 } },
                    { new Wrapper<int>{ Value = 5 } , new Wrapper<int>{ Value = 6 } },
                },
                {
                    { new Wrapper<int>{ Value = 7 } , new Wrapper<int>{ Value = 8 } },
                    { new Wrapper<int>{ Value = 9 } , new Wrapper<int>{ Value = 10 } },
                    { new Wrapper<int>{ Value = 11 } , new Wrapper<int>{ Value = 12 } },
                }
            };
            var t2 = t1.DeepCopy()!;
            Assert.AreNotSame(t1, t2);
            AssertArraysAreEqual<Wrapper<int>>(t1, t2, refsMustBeDifferent: true);
        }

        [TestMethod]
        public void Copy_DeepCopiesMutableFieldsOfValueTypes()
        {
            // Tuple itself is an immutable valuetype, MySingleObject is a mutable reference type
            var a = new Tuple<MySingleObject>(new MySingleObject());
            var b = a.DeepCopy()!;
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(a.Item1, b.Item1);
            Assert.AreEqual(a.Item1, b.Item1);
            Assert.AreEqual(a, b);
        }

        [TestMethod]
        public void Copy_ShallowCopiesImmutableFieldsOfValueTypes()
        {
            // Tuple itself is an immutable valuetype, string is an immutable reference type
            var a = new Tuple<string>("U0FGZSBpcyBTaGl0dHkgQWdpbGUgRm9yIEVudGVycHJpc2VzIQ==");
            var b = a.DeepCopy()!;
            Assert.AreNotSame(a, b);
            Assert.AreSame(a.Item1, b.Item1);
            Assert.AreEqual(a.Item1, b.Item1);
            Assert.AreEqual(a, b);
        }
    }
}
