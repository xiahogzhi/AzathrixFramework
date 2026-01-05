using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Exceptions;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 解析器测试（边界条件、错误处理等）
    /// </summary>
    [TestFixture]
    public class ParserTests
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

        // ========== Lambda 解析测试 ==========

        [Test]
        public void Lambda_EmptyParams()
        {
            var result = _vm.Run(@"
                var f = () => 42
                return f()
            ");
            Assert.AreEqual(42.0, result.AsNumber());
        }

        [Test]
        public void Lambda_SingleParam()
        {
            var result = _vm.Run(@"
                var f = (x) => x * 2
                return f(5)
            ");
            Assert.AreEqual(10.0, result.AsNumber());
        }

        [Test]
        public void Lambda_MultipleParams()
        {
            var result = _vm.Run(@"
                var f = (a, b, c) => a + b + c
                return f(1, 2, 3)
            ");
            Assert.AreEqual(6.0, result.AsNumber());
        }

        [Test]
        public void Lambda_DefaultParams()
        {
            var result = _vm.Run(@"
                var f = (a, b = 10) => a + b
                return f(5)
            ");
            Assert.AreEqual(15.0, result.AsNumber());
        }

        [Test]
        public void Lambda_DefaultParamsWithExpression()
        {
            var result = _vm.Run(@"
                var f = (a, b = 2 + 3) => a + b
                return f(5)
            ");
            Assert.AreEqual(10.0, result.AsNumber());
        }

        [Test]
        public void Lambda_DefaultParamsWithNestedParens()
        {
            var result = _vm.Run(@"
                var f = (a, b = (1 + 2) * 3) => a + b
                return f(1)
            ");
            Assert.AreEqual(10.0, result.AsNumber());
        }

        [Test]
        public void Lambda_RestParams()
        {
            var result = _vm.Run(@"
                var f = (...args) => args.length
                return f(1, 2, 3, 4, 5)
            ");
            Assert.AreEqual(5.0, result.AsNumber());
        }

        [Test]
        public void Lambda_MixedParams()
        {
            var result = _vm.Run(@"
                var f = (a, b = 10, ...rest) => a + b + rest.length
                return f(1, 2, 3, 4, 5)
            ");
            Assert.AreEqual(6.0, result.AsNumber()); // 1 + 2 + 3
        }

        [Test]
        public void Lambda_InGroupedExpression()
        {
            // 确保 (expr) 不会被误认为 lambda
            var result = _vm.Run(@"
                var x = (1 + 2) * 3
                return x
            ");
            Assert.AreEqual(9.0, result.AsNumber());
        }

        // ========== 枚举解析测试 ==========

        [Test]
        public void Enum_AutoIncrement()
        {
            var result = _vm.Run(@"
                enum Color {
                    Red,
                    Green,
                    Blue
                }
                return Color.Blue
            ");
            Assert.AreEqual(2.0, result.AsNumber());
        }

        [Test]
        public void Enum_ExplicitValues()
        {
            var result = _vm.Run(@"
                enum Status {
                    Pending = 10,
                    Active = 20,
                    Done = 30
                }
                return Status.Active
            ");
            Assert.AreEqual(20.0, result.AsNumber());
        }

        [Test]
        public void Enum_MixedValues()
        {
            var result = _vm.Run(@"
                enum Priority {
                    Low,
                    Medium = 5,
                    High
                }
                return Priority.High
            ");
            Assert.AreEqual(6.0, result.AsNumber());
        }

        [Test]
        public void Enum_StringValues()
        {
            var result = _vm.Run(@"
                enum Direction {
                    Up = ""up"",
                    Down = ""down""
                }
                return Direction.Down
            ");
            Assert.AreEqual("down", result.AsString());
        }

        [Test]
        public void Enum_GlobalAccess()
        {
            _vm.Run(@"
                global enum GameState {
                    Menu,
                    Playing, 
                    Paused
                }
            ");
            var result = _vm.Run("return GameState.Playing");
            Assert.AreEqual(1.0, result.AsNumber());
        }

        // ========== 错误处理测试 ==========

        [Test]
        public void Parser_UnexpectedToken_ThrowsException()
        {
            Assert.Throws<ParserException>(() => _vm.Run("var x = @"));
        }

        [Test]
        public void Parser_UnterminatedString_ThrowsException()
        {
            Assert.Throws<LexerException>(() => _vm.Run("var x = \"hello"));
        }

        [Test]
        public void Parser_MissingClosingParen_ThrowsException()
        {
            Assert.Throws<ParserException>(() => _vm.Run("var x = (1 + 2"));
        }

        [Test]
        public void Parser_MissingClosingBrace_ThrowsException()
        {
            Assert.Throws<ParserException>(() => _vm.Run("var obj = { a: 1"));
        }

        [Test]
        public void Parser_MissingClosingBracket_ThrowsException()
        {
            Assert.Throws<ParserException>(() => _vm.Run("var arr = [1, 2, 3"));
        }
    }
}
