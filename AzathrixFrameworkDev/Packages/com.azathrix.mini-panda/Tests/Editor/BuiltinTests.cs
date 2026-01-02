using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Exceptions;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 内置函数测试
    /// </summary>
    [TestFixture]
    public class BuiltinTests
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

        // ========== 类型函数 ==========

        [Test]
        public void Builtin_Type()
        {
            Assert.AreEqual("number", _vm.Eval("type(42)").AsString());
            Assert.AreEqual("string", _vm.Eval("type(\"hello\")").AsString());
            Assert.AreEqual("bool", _vm.Eval("type(true)").AsString());
            Assert.AreEqual("null", _vm.Eval("type(null)").AsString());
        }

        [Test]
        public void Builtin_ToString()
        {
            Assert.AreEqual("42", _vm.Eval("str(42)").AsString());
            Assert.AreEqual("true", _vm.Eval("str(true)").AsString());
            Assert.AreEqual("null", _vm.Eval("str(null)").AsString());
        }

        [Test]
        public void Builtin_ToNumber()
        {
            Assert.AreEqual(42.0, _vm.Eval("num(\"42\")").AsNumber());
            Assert.AreEqual(3.14, _vm.Eval("num(\"3.14\")").AsNumber());
        }

        // ========== 数学函数 ==========

        [Test]
        public void Builtin_Math()
        {
            Assert.AreEqual(5.0, _vm.Eval("abs(-5)").AsNumber());
            Assert.AreEqual(3.0, _vm.Eval("floor(3.7)").AsNumber());
            Assert.AreEqual(4.0, _vm.Eval("ceil(3.2)").AsNumber());
            Assert.AreEqual(3.0, _vm.Eval("round(3.4)").AsNumber());
            Assert.AreEqual(4.0, _vm.Eval("sqrt(16)").AsNumber());
            Assert.AreEqual(8.0, _vm.Eval("pow(2, 3)").AsNumber());
        }

        [Test]
        public void Builtin_MinMax()
        {
            Assert.AreEqual(1.0, _vm.Eval("min(3, 1, 4, 1, 5)").AsNumber());
            Assert.AreEqual(5.0, _vm.Eval("max(3, 1, 4, 1, 5)").AsNumber());
        }

        // ========== JSON 函数 ==========

        [Test]
        public void Builtin_JSON_Parse()
        {
            var obj = _vm.Run("return json.parse(\"\\{\\\"name\\\":\\\"test\\\",\\\"value\\\":42\\}\")").As<MiniPandaObject>();
            Assert.AreEqual("test", obj.Get("name").AsString());
            Assert.AreEqual(42.0, obj.Get("value").AsNumber());

            var arr = _vm.Run("return json.parse(\"[1,2,3]\")").As<MiniPandaArray>();
            Assert.AreEqual(3, arr.Length);
            Assert.AreEqual(2.0, arr.Get(1).AsNumber());

            Assert.AreEqual(42.0, _vm.Run("return json.parse(\"42\")").AsNumber());
            Assert.AreEqual(true, _vm.Run("return json.parse(\"true\")").AsBool());
            Assert.IsTrue(_vm.Run("return json.parse(\"null\")").IsNull);
        }

        [Test]
        public void Builtin_JSON_Stringify()
        {
            Assert.AreEqual("{\"name\":\"test\",\"value\":42}", _vm.Run("return json.stringify({name: \"test\", value: 42})").AsString());
            Assert.AreEqual("[1,2,3]", _vm.Run("return json.stringify([1, 2, 3])").AsString());
            Assert.AreEqual("42", _vm.Run("return json.stringify(42)").AsString());
            Assert.AreEqual("true", _vm.Run("return json.stringify(true)").AsString());
            Assert.AreEqual("null", _vm.Run("return json.stringify(null)").AsString());
        }

        // ========== 调试函数 ==========

        [Test]
        public void Builtin_Assert_Pass()
        {
            _vm.Run("assert(true)");
            _vm.Run("assert(1 == 1)");
            _vm.Run("assert(5 > 3, \"5 should be greater than 3\")");
        }

        [Test]
        public void Builtin_Assert_Fail()
        {
            Assert.Throws<MiniPandaRuntimeException>(() => _vm.Run("assert(false)"));
            Assert.Throws<MiniPandaRuntimeException>(() => _vm.Run("assert(1 == 2, \"Numbers should be equal\")"));
        }

        [Test]
        public void Builtin_Stacktrace()
        {
            var result = _vm.Run(@"
                func inner() { return stacktrace() }
                func outer() { return inner() }
                return outer()
            ");
            var trace = result.AsString();
            Assert.IsTrue(trace.Contains("inner"));
            Assert.IsTrue(trace.Contains("outer"));
        }
    }
}
