using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 核心语法测试（表达式、变量、控制流）
    /// </summary>
    [TestFixture]
    public class CoreTests
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

        // ========== 表达式测试 ==========

        [Test]
        public void Eval_NumberLiteral()
        {
            var result = _vm.Eval("42");
            Assert.AreEqual(42.0, result.AsNumber());
        }

        [Test]
        public void Eval_BooleanLiteral()
        {
            Assert.AreEqual(true, _vm.Eval("true").AsBool());
            Assert.AreEqual(false, _vm.Eval("false").AsBool());
        }

        [Test]
        public void Eval_StringLiteral()
        {
            var result = _vm.Eval("\"hello\"");
            Assert.AreEqual("hello", result.AsString());
        }

        [Test]
        public void Eval_Arithmetic()
        {
            Assert.AreEqual(7.0, _vm.Eval("3 + 4").AsNumber());
            Assert.AreEqual(6.0, _vm.Eval("10 - 4").AsNumber());
            Assert.AreEqual(12.0, _vm.Eval("3 * 4").AsNumber());
            Assert.AreEqual(5.0, _vm.Eval("20 / 4").AsNumber());
            Assert.AreEqual(1.0, _vm.Eval("10 % 3").AsNumber());
        }

        [Test]
        public void Eval_ArithmeticPrecedence()
        {
            Assert.AreEqual(14.0, _vm.Eval("2 + 3 * 4").AsNumber());
            Assert.AreEqual(20.0, _vm.Eval("(2 + 3) * 4").AsNumber());
        }

        [Test]
        public void Eval_Comparison()
        {
            Assert.AreEqual(true, _vm.Eval("5 > 3").AsBool());
            Assert.AreEqual(false, _vm.Eval("5 < 3").AsBool());
            Assert.AreEqual(true, _vm.Eval("5 >= 5").AsBool());
            Assert.AreEqual(true, _vm.Eval("5 <= 5").AsBool());
            Assert.AreEqual(true, _vm.Eval("5 == 5").AsBool());
            Assert.AreEqual(true, _vm.Eval("5 != 3").AsBool());
        }

        [Test]
        public void Eval_LogicalOperators()
        {
            Assert.AreEqual(true, _vm.Eval("true && true").AsBool());
            Assert.AreEqual(false, _vm.Eval("true && false").AsBool());
            Assert.AreEqual(true, _vm.Eval("true || false").AsBool());
            Assert.AreEqual(false, _vm.Eval("false || false").AsBool());
            Assert.AreEqual(false, _vm.Eval("!true").AsBool());
        }

        [Test]
        public void Eval_UnaryMinus()
        {
            Assert.AreEqual(-5.0, _vm.Eval("-5").AsNumber());
            Assert.AreEqual(5.0, _vm.Eval("-(-5)").AsNumber());
        }

        [Test]
        public void Eval_TernaryOperator()
        {
            Assert.AreEqual(1.0, _vm.Eval("true ? 1 : 2").AsNumber());
            Assert.AreEqual(2.0, _vm.Eval("false ? 1 : 2").AsNumber());
            Assert.AreEqual(10.0, _vm.Eval("5 > 3 ? 10 : 20").AsNumber());
        }

        // ========== 复合赋值测试 ==========

        [Test]
        public void Run_CompoundAssignment()
        {
            Assert.AreEqual(15.0, _vm.Run("var x = 10; x += 5; return x").AsNumber());
            Assert.AreEqual(12.0, _vm.Run("var y = 20; y -= 8; return y").AsNumber());
            Assert.AreEqual(18.0, _vm.Run("var z = 6; z *= 3; return z").AsNumber());
            Assert.AreEqual(25.0, _vm.Run("var w = 100; w /= 4; return w").AsNumber());
            Assert.AreEqual(1.0, _vm.Run("var m = 10; m %= 3; return m").AsNumber());
        }

        [Test]
        public void Run_IncrementDecrement()
        {
            Assert.AreEqual(6.0, _vm.Run("var a = 5; return ++a").AsNumber());
            Assert.AreEqual(5.0, _vm.Run("var b = 5; return b++").AsNumber());
            Assert.AreEqual(4.0, _vm.Run("var c = 5; return --c").AsNumber());
            Assert.AreEqual(5.0, _vm.Run("var d = 5; return d--").AsNumber());
        }

        // ========== 变量测试 ==========

        [Test]
        public void Run_VarDeclaration()
        {
            Assert.AreEqual(10.0, _vm.Run("var x = 10; return x").AsNumber());
        }

        [Test]
        public void Run_VarAssignment()
        {
            Assert.AreEqual(20.0, _vm.Run("var x = 10; x = 20; return x").AsNumber());
        }

        [Test]
        public void Run_VarWithoutInitializer()
        {
            Assert.IsTrue(_vm.Run("var x; return x").IsNull);
        }

        [Test]
        public void Run_LocalScope()
        {
            Assert.AreEqual(10.0, _vm.Run(@"
                var x = 10
                { var x = 20 }
                return x
            ").AsNumber());
        }

        // ========== 控制流测试 ==========

        [Test]
        public void Run_IfStatement()
        {
            Assert.AreEqual(1.0, _vm.Run("var result = 0\n if true { result = 1 }\n return result").AsNumber());
        }

        [Test]
        public void Run_IfElseStatement()
        {
            Assert.AreEqual(2.0, _vm.Run("var result = 0\n if false { result = 1 } else { result = 2 }\n return result").AsNumber());
        }

        [Test]
        public void Run_WhileLoop()
        {
            Assert.AreEqual(10.0, _vm.Run(@"
                var i = 0
                var sum = 0
                while i < 5 {
                    sum = sum + i
                    i = i + 1
                }
                return sum
            ").AsNumber());
        }

        [Test]
        public void Run_WhileBreak()
        {
            Assert.AreEqual(5.0, _vm.Run(@"
                var i = 0
                while true {
                    i = i + 1
                    if i == 5 break
                }
                return i
            ").AsNumber());
        }

        [Test]
        public void Run_WhileContinue()
        {
            Assert.AreEqual(25.0, _vm.Run(@"
                var i = 0
                var sum = 0
                while i < 10 {
                    i = i + 1
                    if i % 2 == 0 continue
                    sum = sum + i
                }
                return sum
            ").AsNumber());
        }

        // ========== For 循环测试 ==========

        [Test]
        public void Run_ForLoop_Array()
        {
            Assert.AreEqual(15.0, _vm.Run(@"
                var arr = [1, 2, 3, 4, 5]
                var sum = 0
                for n in arr { sum = sum + n }
                return sum
            ").AsNumber());
        }

        [Test]
        public void Run_ForLoop_Range()
        {
            Assert.AreEqual(10.0, _vm.Run(@"
                var sum = 0
                for i in range(5) { sum = sum + i }
                return sum
            ").AsNumber());
        }

        [Test]
        public void Run_ForLoop_Break()
        {
            Assert.AreEqual(10.0, _vm.Run(@"
                var sum = 0
                for i in range(10) {
                    if i == 5 break
                    sum = sum + i
                }
                return sum
            ").AsNumber());
        }

        [Test]
        public void Run_ForLoop_Continue()
        {
            Assert.AreEqual(25.0, _vm.Run(@"
                var sum = 0
                for i in range(10) {
                    if i % 2 == 0 continue
                    sum = sum + i
                }
                return sum
            ").AsNumber());
        }

        // ========== 字符串插值测试 ==========

        [Test]
        public void StringInterpolation_Basic()
        {
            Assert.AreEqual("Hello World!", _vm.Run("var name = \"World\"\nreturn \"Hello {name}!\"").AsString());
        }

        [Test]
        public void StringInterpolation_Expression()
        {
            Assert.AreEqual("Result: 15", _vm.Run("return \"Result: {10 + 5}\"").AsString());
        }

        [Test]
        public void StringInterpolation_Multiple()
        {
            Assert.AreEqual("a=1, b=2", _vm.Run("var a = 1\nvar b = 2\nreturn \"a={a}, b={b}\"").AsString());
        }

        // ========== 空值合并测试 ==========

        [Test]
        public void NullCoalesce_LeftNotNull()
        {
            Assert.AreEqual(5, _vm.Run("return 5 ?? 10").AsNumber());
        }

        [Test]
        public void NullCoalesce_LeftNull()
        {
            Assert.AreEqual(10, _vm.Run("return null ?? 10").AsNumber());
        }

        [Test]
        public void NullCoalesce_Chain()
        {
            Assert.AreEqual(3, _vm.Run("return null ?? null ?? 3").AsNumber());
        }
    }
}
