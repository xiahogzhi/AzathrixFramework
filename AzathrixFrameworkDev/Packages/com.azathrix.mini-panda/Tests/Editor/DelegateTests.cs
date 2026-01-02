using System;
using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 强类型委托转换测试
    /// </summary>
    [TestFixture]
    public class DelegateTests
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

        [Test]
        public void Delegate_Action_NoParams()
        {
            _vm.Run("_G.called = false");
            var action = _vm.Run<Action>("func f() { _G.called = true } return f");
            action();
            Assert.IsTrue(_vm.Run<bool>("return _G.called"));
        }

        [Test]
        public void Delegate_Action_IntParam()
        {
            var action = _vm.Run<Action<int>>("func f(x) { _G.result = x * 2 } return f");
            action(21);
            Assert.AreEqual(42.0, _vm.Run<double>("return _G.result"));
        }

        [Test]
        public void Delegate_Action_MultipleParams()
        {
            var action = _vm.Run<Action<int, int, int>>("func f(a, b, c) { _G.result = a + b + c } return f");
            action(10, 20, 12);
            Assert.AreEqual(42.0, _vm.Run<double>("return _G.result"));
        }

        [Test]
        public void Delegate_Func_NoParams_ReturnsInt()
        {
            var f = _vm.Run<Func<int>>("func getAnswer() { return 42 } return getAnswer");
            Assert.AreEqual(42, f());
        }

        [Test]
        public void Delegate_Func_IntParam_ReturnsInt()
        {
            var f = _vm.Run<Func<int, int>>("func double(x) { return x * 2 } return double");
            Assert.AreEqual(42, f(21));
        }

        [Test]
        public void Delegate_Func_StringParam_ReturnsString()
        {
            var f = _vm.Run<Func<string, string>>("func greet(name) { return \"Hello \" + name } return greet");
            Assert.AreEqual("Hello World", f("World"));
        }

        [Test]
        public void Delegate_Func_MixedParams()
        {
            var f = _vm.Run<Func<string, int, string>>("func format(name, age) { return name + \" is \" + age } return format");
            Assert.AreEqual("Alice is 30", f("Alice", 30));
        }

        [Test]
        public void Delegate_Func_FloatParams()
        {
            var f = _vm.Run<Func<float, float, float>>("func add(a, b) { return a + b } return add");
            Assert.AreEqual(3.5f, f(1.5f, 2.0f), 0.001f);
        }

        [Test]
        public void Delegate_Func_BoolParam_ReturnsBool()
        {
            var f = _vm.Run<Func<bool, bool>>("func negate(b) { return !b } return negate");
            Assert.IsFalse(f(true));
            Assert.IsTrue(f(false));
        }

        [Test]
        public void Delegate_Func_ManyParams()
        {
            var f = _vm.Run<Func<int, int, int, int, int, int>>("func sum(a, b, c, d, e) { return a + b + c + d + e } return sum");
            Assert.AreEqual(15, f(1, 2, 3, 4, 5));
        }

        [Test]
        public void Delegate_Lambda()
        {
            var f = _vm.Run<Func<double, double>>("return (x) => x * x");
            Assert.AreEqual(49.0, f(7.0), 0.001);
        }

        [Test]
        public void Delegate_Predicate()
        {
            var predicate = _vm.Run<Predicate<int>>("func isEven(n) { return n % 2 == 0 } return isEven");
            Assert.IsTrue(predicate(4));
            Assert.IsFalse(predicate(5));
        }

        // 定义带 params 的委托类型
        public delegate int SumDelegate(params object[] args);

        [Test]
        public void Delegate_ParamsArray()
        {
            var sum = _vm.Run<SumDelegate>("func sum(...args) { var r = 0 for v in args { r += v } return r } return sum");
            Assert.AreEqual(15, sum(1, 2, 3, 4, 5));
            Assert.AreEqual(10, sum(10));
            Assert.AreEqual(0, sum());
        }
    }
}
