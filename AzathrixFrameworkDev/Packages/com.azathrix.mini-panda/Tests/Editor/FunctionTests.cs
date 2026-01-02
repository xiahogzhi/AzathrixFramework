using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 函数和闭包测试
    /// </summary>
    [TestFixture]
    public class FunctionTests
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

        // ========== 基础函数测试 ==========

        [Test]
        public void Run_FunctionDeclaration()
        {
            Assert.AreEqual(7.0, _vm.Run(@"
                func add(a, b) { return a + b }
                return add(3, 4)
            ").AsNumber());
        }

        [Test]
        public void Run_FunctionSingleLine()
        {
            Assert.AreEqual(10.0, _vm.Run("func double(x) return x * 2; return double(5)").AsNumber());
        }

        [Test]
        public void Run_Recursion()
        {
            Assert.AreEqual(55.0, _vm.Run(@"
                func fib(n) {
                    if n <= 1 return n
                    return fib(n - 1) + fib(n - 2)
                }
                return fib(10)
            ").AsNumber());
        }

        [Test]
        public void Run_Lambda()
        {
            Assert.AreEqual(12.0, _vm.Run("var triple = (x) => x * 3; return triple(4)").AsNumber());
        }

        // ========== 默认参数测试 ==========

        [Test]
        public void DefaultParam_Basic()
        {
            Assert.AreEqual(10, _vm.Run("func foo(x = 10) { return x }\nreturn foo()").AsNumber());
        }

        [Test]
        public void DefaultParam_Override()
        {
            Assert.AreEqual(5, _vm.Run("func foo(x = 10) { return x }\nreturn foo(5)").AsNumber());
        }

        [Test]
        public void DefaultParam_Multiple()
        {
            Assert.AreEqual(30, _vm.Run("func add(a, b = 10, c = 20) { return a + b + c }\nreturn add(0)").AsNumber());
        }

        [Test]
        public void DefaultParam_PartialOverride()
        {
            Assert.AreEqual(25, _vm.Run("func add(a, b = 10, c = 20) { return a + b + c }\nreturn add(0, 5)").AsNumber());
        }

        [Test]
        public void DefaultParam_Lambda()
        {
            Assert.AreEqual(10, _vm.Run("var f = (x = 10) => x\nreturn f()").AsNumber());
        }

        // ========== 可变参数测试 ==========

        [Test]
        public void RestParam_Basic()
        {
            Assert.AreEqual(3, _vm.Run("func sum(...args) { return len(args) }\nreturn sum(1, 2, 3)").AsNumber());
        }

        [Test]
        public void RestParam_Empty()
        {
            Assert.AreEqual(0, _vm.Run("func f(...args) { return len(args) }\nreturn f()").AsNumber());
        }

        [Test]
        public void RestParam_WithRegularParams()
        {
            var result = _vm.Run(@"
func greet(greeting, ...names) {
    var result = greeting
    for name in names {
        result = result + "" "" + name
    }
    return result
}
return greet(""Hello"", ""Alice"", ""Bob"")
").AsString();
            Assert.AreEqual("Hello Alice Bob", result);
        }

        [Test]
        public void RestParam_Sum()
        {
            Assert.AreEqual(15, _vm.Run(@"
func sum(...nums) {
    var total = 0
    for n in nums { total += n }
    return total
}
return sum(1, 2, 3, 4, 5)
").AsNumber());
        }

        [Test]
        public void RestParam_Lambda()
        {
            Assert.AreEqual(6, _vm.Run(@"
var sum = (...args) => {
    var total = 0
    for a in args { total += a }
    return total
}
return sum(1, 2, 3)
").AsNumber());
        }

        // ========== 闭包测试 ==========

        [Test]
        public void Closure_CapturesVariable()
        {
            Assert.AreEqual(3.0, _vm.Run(@"
                func makeCounter() {
                    var count = 0
                    return () => { count = count + 1; return count }
                }
                var counter = makeCounter()
                counter()
                counter()
                return counter()
            ").AsNumber());
        }

        [Test]
        public void Closure_IndependentInstances()
        {
            Assert.AreEqual(8.0, _vm.Run(@"
                func makeAdder(x) { return (y) => x + y }
                var add5 = makeAdder(5)
                return add5(3)
            ").AsNumber());
        }

        [Test]
        public void Closure_NestedClosures()
        {
            Assert.AreEqual(6.0, _vm.Run(@"
                func outer(x) {
                    func middle(y) {
                        func inner(z) { return x + y + z }
                        return inner
                    }
                    return middle
                }
                return outer(1)(2)(3)
            ").AsNumber());
        }
    }
}
