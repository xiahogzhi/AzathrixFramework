using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 静态成员功能测试
    /// </summary>
    [TestFixture]
    public class StaticMemberTests
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

        // ========== 静态字段测试 ==========

        [Test]
        public void StaticField_BasicAccess()
        {
            Assert.AreEqual(0.0, _vm.Run(@"
                class Counter {
                    static var count = 0
                }
                return Counter.count
            ").AsNumber());
        }

        [Test]
        public void StaticField_Assignment()
        {
            Assert.AreEqual(100.0, _vm.Run(@"
                class Counter {
                    static var count = 0
                }
                Counter.count = 100
                return Counter.count
            ").AsNumber());
        }

        [Test]
        public void StaticField_SharedAcrossInstances()
        {
            Assert.AreEqual(3.0, _vm.Run(@"
                class Counter {
                    static var count = 0
                    Counter() {
                        Counter.count = Counter.count + 1
                    }
                }
                var a = Counter()
                var b = Counter()
                var c = Counter()
                return Counter.count
            ").AsNumber());
        }

        [Test]
        public void StaticField_IndependentFromInstance()
        {
            var result = _vm.Run(@"
                class Point {
                    static var origin = 0
                    var x
                    Point(x) {
                        this.x = x
                    }
                }
                Point.origin = 100
                var p = Point(50)
                return [Point.origin, p.x]
            ").As<MiniPandaArray>();
            Assert.AreEqual(100.0, result.Get(0).AsNumber());
            Assert.AreEqual(50.0, result.Get(1).AsNumber());
        }

        [Test]
        public void StaticField_MultipleFields()
        {
            var result = _vm.Run(@"
                class Config {
                    static var name = ""App""
                    static var version = ""1.0""
                    static var debug = true
                }
                return [Config.name, Config.version, Config.debug]
            ").As<MiniPandaArray>();
            Assert.AreEqual("App", result.Get(0).AsString());
            Assert.AreEqual("1.0", result.Get(1).AsString());
            Assert.AreEqual(true, result.Get(2).AsBool());
        }

        [Test]
        public void StaticField_NullInitializer()
        {
            Assert.IsTrue(_vm.Run(@"
                class Test {
                    static var data
                }
                return Test.data
            ").IsNull);
        }

        [Test]
        public void StaticField_InheritanceNotShared()
        {
            // 静态字段不在子类中共享，各自独立
            var result = _vm.Run(@"
                class Parent {
                    static var value = 10
                }
                class Child : Parent {
                    static var childValue = 20
                }
                Parent.value = 100
                return [Parent.value, Child.childValue]
            ").As<MiniPandaArray>();
            Assert.AreEqual(100.0, result.Get(0).AsNumber());
            Assert.AreEqual(20.0, result.Get(1).AsNumber());
        }

        // ========== 静态方法测试 ==========

        [Test]
        public void StaticMethod_BasicCall()
        {
            Assert.AreEqual(1.0, _vm.Run(@"
                class Counter {
                    static var count = 0
                    static func increment() {
                        Counter.count = Counter.count + 1
                        return Counter.count
                    }
                }
                return Counter.increment()
            ").AsNumber());
        }

        [Test]
        public void StaticMethod_MultipleCall()
        {
            Assert.AreEqual(3.0, _vm.Run(@"
                class Counter {
                    static var count = 0
                    static func increment() {
                        Counter.count = Counter.count + 1
                        return Counter.count
                    }
                }
                Counter.increment()
                Counter.increment()
                return Counter.increment()
            ").AsNumber());
        }

        [Test]
        public void StaticMethod_WithInstanceMethod()
        {
            Assert.AreEqual(15.0, _vm.Run(@"
                class Calculator {
                    static var lastResult = 0
                    var value
                    Calculator(v) { this.value = v }
                    static func add(a, b) {
                        Calculator.lastResult = a + b
                        return Calculator.lastResult
                    }
                    func getValue() { return this.value }
                }
                Calculator.add(5, 10)
                return Calculator.lastResult
            ").AsNumber());
        }

        [Test]
        public void StaticMethod_AccessStaticField()
        {
            Assert.AreEqual("v1.0", _vm.Run(@"
                class App {
                    static var version = ""1.0""
                    static func getVersion() {
                        return ""v"" + App.version
                    }
                }
                return App.getVersion()
            ").AsString());
        }

        [Test]
        public void StaticMethod_WithParameters()
        {
            Assert.AreEqual(30.0, _vm.Run(@"
                class Math {
                    static func add(a, b) { return a + b }
                    static func multiply(a, b) { return a * b }
                }
                return Math.add(10, 20)
            ").AsNumber());
        }
    }
}
