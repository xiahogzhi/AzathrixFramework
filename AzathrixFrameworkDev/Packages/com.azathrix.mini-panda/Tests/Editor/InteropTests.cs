using System;
using System.Collections.Generic;
using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Exceptions;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// C# 互操作测试
    /// </summary>
    [TestFixture]
    public class InteropTests
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

        // ========== 基础互操作 ==========

        [Test]
        public void CSharp_SetGlobal()
        {
            _vm.SetGlobal("PI", Value.FromNumber(3.14159));
            var result = _vm.Eval("PI * 2");
            Assert.AreEqual(6.28318, result.AsNumber(), 0.0001);
        }

        [Test]
        public void CSharp_NativeFunction()
        {
            _vm.SetGlobal("square", Value.FromObject(NativeFunction.Create((Value v) =>
                Value.FromNumber(v.AsNumber() * v.AsNumber()))));

            var result = _vm.Eval("square(5)");
            Assert.AreEqual(25.0, result.AsNumber());
        }

        [Test]
        public void CSharp_CallScriptFunction()
        {
            _vm.SetGlobal("multiply", Value.FromObject(NativeFunction.Create((Value a, Value b) =>
                Value.FromNumber(a.AsNumber() * b.AsNumber()))));
            Assert.AreEqual(42.0, _vm.Run("return multiply(6, 7)").AsNumber());
        }

        [Test]
        public void Eval_WithEnvironment()
        {
            var result = _vm.Eval("x + y", new Dictionary<string, object> { ["x"] = 10, ["y"] = 20 });
            Assert.AreEqual(30.0, result.AsNumber());
        }

        [Test]
        public void Call_WithScope()
        {
            _vm.Run("global func greet(name) { return prefix + name + suffix }");
            var result = _vm.Call(new Dictionary<string, object> { ["prefix"] = "Hello, ", ["suffix"] = "!" }, "greet", "World");
            Assert.AreEqual("Hello, World!", result.AsString());
        }

        [Test]
        public void Call_WithScope_Dictionary()
        {
            _vm.Run("global func calculate() { return a * b + c }");
            var scope = new Dictionary<string, object> { ["a"] = 2, ["b"] = 3, ["c"] = 4 };
            var result = _vm.Call(scope, "calculate");
            Assert.AreEqual(10.0, result.AsNumber());
        }

        // ========== 缓存测试 ==========

        [Test]
        public void Compile_CacheWorks()
        {
            var code = "1 + 2";
            var compiled1 = _vm.Compile(code);
            var compiled2 = _vm.Compile(code);
            Assert.AreSame(compiled1, compiled2);
        }

        // ========== 泛型 Run/Eval 测试 ==========

        [Test]
        public void Run_GenericInt()
        {
            Assert.AreEqual(42, _vm.Run<int>("return 42"));
        }

        [Test]
        public void Run_GenericDouble()
        {
            Assert.AreEqual(3.14, _vm.Run<double>("return 3.14"), 0.001);
        }

        [Test]
        public void Run_GenericBool()
        {
            Assert.IsTrue(_vm.Run<bool>("return true"));
            Assert.IsFalse(_vm.Run<bool>("return false"));
        }

        [Test]
        public void Run_GenericString()
        {
            Assert.AreEqual("hello", _vm.Run<string>("return \"hello\""));
        }

        [Test]
        public void Eval_GenericInt()
        {
            Assert.AreEqual(10, _vm.Eval<int>("5 + 5"));
        }

        [Test]
        public void Eval_GenericWithEnv()
        {
            Assert.AreEqual(30, _vm.Eval<int>("x + y", new Dictionary<string, object> { ["x"] = 10, ["y"] = 20 }));
        }

        [Test]
        public void Run_GenericFunc()
        {
            var add = _vm.Run<Func<object, object, object>>(@"
                func add(a, b) { return a + b }
                return add
            ");
            Assert.IsNotNull(add);
            var result = add(3.0, 4.0);
            Assert.AreEqual(7.0, result);
        }

        [Test]
        public void Run_GenericAction()
        {
            _vm.Run("global var called = false");
            var action = _vm.Run<Action>(@"
                func doIt() { called = true }
                return doIt
            ");
            Assert.IsNotNull(action);
            action();
            Assert.IsTrue(_vm.Eval<bool>("called"));
        }

        [Test]
        public void Run_GenericMiniPandaArray()
        {
            var arr = _vm.Run<MiniPandaArray>("return [1, 2, 3]");
            Assert.IsNotNull(arr);
            Assert.AreEqual(3, arr.Length);
            Assert.AreEqual(2.0, arr.Get(1).AsNumber());
        }

        [Test]
        public void Run_ScopeReuse()
        {
            _vm.Run("var x = 10", "test");
            var result = _vm.Run<int>("return x + 5", "test", clearScope: false);
            Assert.AreEqual(15, result);
        }

        // ========== IEnvironmentProvider 测试 ==========

        private class TestEnvProvider : IEnvironmentProvider
        {
            public int X { get; set; } = 10;
            public int Y { get; set; } = 20;

            public Value Get(string name) => name switch
            {
                "x" => Value.FromNumber(X),
                "y" => Value.FromNumber(Y),
                _ => Value.Null
            };

            public bool Contains(string name) => name is "x" or "y";
        }

        [Test]
        public void Eval_WithEnvironmentProvider()
        {
            var provider = new TestEnvProvider();
            var result = _vm.Eval("x + y", provider);
            Assert.AreEqual(30.0, result.AsNumber());
        }

        [Test]
        public void Eval_WithEnvironmentProvider_DynamicUpdate()
        {
            var provider = new TestEnvProvider { X = 10, Y = 20 };

            var result1 = _vm.Eval<int>("x + y", provider);
            Assert.AreEqual(30, result1);

            // 修改外部值
            provider.X = 100;
            provider.Y = 200;

            // 再次求值，应该获取最新值
            var result2 = _vm.Eval<int>("x + y", provider);
            Assert.AreEqual(300, result2);
        }

        [Test]
        public void Eval_WithEnvironmentProvider_Generic()
        {
            var provider = new TestEnvProvider { X = 5, Y = 3 };
            Assert.AreEqual(15, _vm.Eval<int>("x * y", provider));
        }
    }

    /// <summary>
    /// Global 关键字测试
    /// </summary>
    [TestFixture]
    public class GlobalKeywordTests
    {
        [Test]
        public void GlobalVar_VisibleAcrossRuns()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run("global var counter = 100");
            var result = vm.Run("return counter + 1");

            Assert.AreEqual(101.0, result.AsNumber());
            vm.Shutdown();
        }

        [Test]
        public void GlobalFunc_VisibleAcrossRuns()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run("global func double(x) { return x * 2 }");
            var result = vm.Run("return double(21)");

            Assert.AreEqual(42.0, result.AsNumber());
            vm.Shutdown();
        }

        [Test]
        public void GlobalClass_VisibleAcrossRuns()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run(@"
                global class Point {
                    var x = 0
                    var y = 0
                    Point(x, y) {
                        this.x = x
                        this.y = y
                    }
                }
            ");
            var result = vm.Run(@"
                var p = Point(3, 4)
                return p.x + p.y
            ");

            Assert.AreEqual(7.0, result.AsNumber());
            vm.Shutdown();
        }

        [Test]
        public void LocalVar_NotVisibleAcrossRuns()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run("var localOnly = 999");

            Assert.Throws<MiniPandaRuntimeException>(() =>
            {
                vm.Run("return localOnly");
            });

            vm.Shutdown();
        }

        [Test]
        public void GlobalTable_ReadGlobal()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run("global var myGlobal = 42");
            var result = vm.Run("return _G.myGlobal");

            Assert.AreEqual(42.0, result.AsNumber());
            vm.Shutdown();
        }

        [Test]
        public void GlobalTable_WriteGlobal()
        {
            var vm = new MiniPanda();
            vm.Start();

            vm.Run("_G.newGlobal = 100");
            var result = vm.Run("return newGlobal");

            Assert.AreEqual(100.0, result.AsNumber());
            vm.Shutdown();
        }

        [Test]
        public void GlobalTable_AccessBuiltins()
        {
            var vm = new MiniPanda();
            vm.Start();

            var result = vm.Run("return _G.abs(-5)");

            Assert.AreEqual(5.0, result.AsNumber());
            vm.Shutdown();
        }
    }
}
