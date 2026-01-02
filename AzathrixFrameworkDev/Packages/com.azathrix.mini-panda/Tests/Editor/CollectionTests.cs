using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 集合（数组、对象）测试
    /// </summary>
    [TestFixture]
    public class CollectionTests
    {
        private MiniPanda _vm;

        [SetUp]
        public void Setup()
        {
            _vm = new MiniPanda();
            _vm.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Shutdown();
        }

        // ========== 数组测试 ==========

        [Test]
        public void Run_ArrayLiteral()
        {
            var arr = _vm.Run("return [1, 2, 3]").As<MiniPandaArray>();
            Assert.AreEqual(3, arr.Length);
            Assert.AreEqual(1.0, arr.Get(0).AsNumber());
            Assert.AreEqual(2.0, arr.Get(1).AsNumber());
            Assert.AreEqual(3.0, arr.Get(2).AsNumber());
        }

        [Test]
        public void Run_ArrayIndexAccess()
        {
            Assert.AreEqual(20.0, _vm.Run("var arr = [10, 20, 30]; return arr[1]").AsNumber());
        }

        [Test]
        public void Run_ArrayIndexSet()
        {
            Assert.AreEqual(100.0, _vm.Run("var arr = [1, 2, 3]; arr[1] = 100; return arr[1]").AsNumber());
        }

        [Test]
        public void EdgeCase_EmptyArray()
        {
            var arr = _vm.Run("return []").As<MiniPandaArray>();
            Assert.AreEqual(0, arr.Length);
        }

        [Test]
        public void EdgeCase_NestedArrays()
        {
            Assert.AreEqual(4.0, _vm.Run("var arr = [[1, 2], [3, 4]]; return arr[1][1]").AsNumber());
        }

        // ========== 对象测试 ==========

        [Test]
        public void Run_ObjectLiteral()
        {
            var obj = _vm.Run("return {name: \"test\", value: 42}").As<MiniPandaObject>();
            Assert.AreEqual("test", obj.Get("name").AsString());
            Assert.AreEqual(42.0, obj.Get("value").AsNumber());
        }

        [Test]
        public void Run_ObjectPropertyAccess()
        {
            Assert.AreEqual(10.0, _vm.Run("var obj = {x: 10}; return obj.x").AsNumber());
        }

        [Test]
        public void Run_ObjectPropertySet()
        {
            Assert.AreEqual(20.0, _vm.Run("var obj = {x: 10}; obj.x = 20; return obj.x").AsNumber());
        }

        [Test]
        public void EdgeCase_EmptyObject()
        {
            var obj = _vm.Run("return {}").As<MiniPandaObject>();
            Assert.IsNotNull(obj);
        }

        [Test]
        public void EdgeCase_NestedObjects()
        {
            Assert.AreEqual(42.0, _vm.Run("var obj = {inner: {value: 42}}; return obj.inner.value").AsNumber());
        }

        // ========== 集合函数测试 ==========

        [Test]
        public void Builtin_Keys()
        {
            var result = _vm.Run("var obj = {a: 1, b: 2}\nreturn keys(obj)");
            Assert.AreEqual(2, result.As<MiniPandaArray>().Length);
        }

        [Test]
        public void Builtin_Values()
        {
            var result = _vm.Run("var obj = {a: 1, b: 2}\nreturn values(obj)");
            Assert.AreEqual(2, result.As<MiniPandaArray>().Length);
        }

        [Test]
        public void Builtin_Contains_Array()
        {
            Assert.AreEqual(true, _vm.Run("return contains([1, 2, 3], 2)").AsBool());
            Assert.AreEqual(false, _vm.Run("return contains([1, 2, 3], 5)").AsBool());
        }

        [Test]
        public void Builtin_Contains_Object()
        {
            Assert.AreEqual(true, _vm.Run("return contains({a: 1}, \"a\")").AsBool());
            Assert.AreEqual(false, _vm.Run("return contains({a: 1}, \"b\")").AsBool());
        }

        [Test]
        public void Builtin_Contains_String()
        {
            Assert.AreEqual(true, _vm.Run("return contains(\"hello\", \"ell\")").AsBool());
            Assert.AreEqual(false, _vm.Run("return contains(\"hello\", \"xyz\")").AsBool());
        }

        [Test]
        public void Builtin_Slice_Array()
        {
            Assert.AreEqual(2, _vm.Run("return slice([1,2,3,4], 1, 3)").As<MiniPandaArray>().Length);
        }

        [Test]
        public void Builtin_Slice_String()
        {
            Assert.AreEqual("ell", _vm.Run("return slice(\"hello\", 1, 4)").AsString());
        }

        [Test]
        public void Builtin_Join()
        {
            Assert.AreEqual("a-b-c", _vm.Run("return join([\"a\", \"b\", \"c\"], \"-\")").AsString());
        }

        [Test]
        public void Builtin_Split()
        {
            Assert.AreEqual(3, _vm.Run("return split(\"a,b,c\", \",\")").As<MiniPandaArray>().Length);
        }

        [Test]
        public void Builtin_Len()
        {
            Assert.AreEqual(3.0, _vm.Eval("len([1, 2, 3])").AsNumber());
            Assert.AreEqual(5.0, _vm.Eval("len(\"hello\")").AsNumber());
        }

        [Test]
        public void Builtin_Push()
        {
            var arr = _vm.Run("var arr = [1, 2]; push(arr, 3); return arr").As<MiniPandaArray>();
            Assert.AreEqual(3, arr.Length);
            Assert.AreEqual(3.0, arr.Get(2).AsNumber());
        }

        [Test]
        public void Builtin_Pop()
        {
            Assert.AreEqual(3.0, _vm.Run("var arr = [1, 2, 3]; return pop(arr)").AsNumber());
        }

        [Test]
        public void Builtin_Range()
        {
            var arr = _vm.Run("return range(5)").As<MiniPandaArray>();
            Assert.AreEqual(5, arr.Length);
            Assert.AreEqual(0.0, arr.Get(0).AsNumber());
            Assert.AreEqual(4.0, arr.Get(4).AsNumber());
        }
    }
}
