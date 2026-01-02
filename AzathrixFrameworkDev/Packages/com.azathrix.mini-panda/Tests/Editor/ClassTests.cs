using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 类和继承测试
    /// </summary>
    [TestFixture]
    public class ClassTests
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
        public void Run_ClassDeclaration()
        {
            var result = _vm.Run(@"
                class Point {
                    Point(x, y) {
                        this.x = x
                        this.y = y
                    }
                }
                var p = Point(3, 4)
                return p.x + p.y
            ");
            Assert.AreEqual(7.0, result.AsNumber());
        }

        [Test]
        public void Run_ClassMethod()
        {
            Assert.AreEqual(2.0, _vm.Run(@"
                class Counter {
                    Counter() { this.count = 0 }
                    func inc() { this.count = this.count + 1 }
                }
                var c = Counter()
                c.inc()
                c.inc()
                return c.count
            ").AsNumber());
        }

        [Test]
        public void Run_ClassInheritance()
        {
            Assert.AreEqual("Buddy barks", _vm.Run(@"
                class Animal {
                    Animal(name) { this.name = name }
                    func speak() { return this.name + "" says hello"" }
                }
                class Dog : Animal {
                    Dog(name, breed) {
                        super.Animal(name)
                        this.breed = breed
                    }
                    func speak() { return this.name + "" barks"" }
                }
                var dog = Dog(""Buddy"", ""Labrador"")
                return dog.speak()
            ").AsString());
        }

        [Test]
        public void Run_SuperMethodCall()
        {
            Assert.AreEqual(30.0, _vm.Run(@"
                class Base {
                    Base(x) { this.x = x }
                    func getValue() { return this.x }
                }
                class Derived : Base {
                    Derived(x, y) {
                        super.Base(x)
                        this.y = y
                    }
                    func getSum() { return super.getValue() + this.y }
                }
                var d = Derived(10, 20)
                return d.getSum()
            ").AsNumber());
        }

        [Test]
        public void EdgeCase_ChainedMethodCalls()
        {
            Assert.AreEqual(40.0, _vm.Run(@"
                class Builder {
                    Builder() { this.value = 0 }
                    func add(n) { this.value = this.value + n; return this }
                    func mul(n) { this.value = this.value * n; return this }
                }
                var b = Builder()
                return b.add(5).mul(2).add(10).mul(2).value
            ").AsNumber());
        }
    }
}
