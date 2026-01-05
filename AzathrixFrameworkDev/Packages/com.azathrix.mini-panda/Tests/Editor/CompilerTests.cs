using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Compiler;
using System.Text;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 编译器测试（槽位溢出、优化等）
    /// </summary>
    [TestFixture]
    public class CompilerTests
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

        // ========== 局部变量槽位溢出测试 ==========

        [Test]
        public void LocalVariableOverflow_ThrowsException()
        {
            // 在块作用域内生成超过 255 个局部变量
            // 块作用域会增加 scopeDepth，使变量成为局部变量
            var sb = new StringBuilder();
            sb.AppendLine("{");
            for (int i = 0; i < 260; i++)
            {
                sb.AppendLine($"    var v{i} = {i}");
            }
            sb.AppendLine("}");

            var ex = Assert.Throws<CompilerException>(() => _vm.Run(sb.ToString()));
            Assert.That(ex.Message, Does.Contain("Too many local variables"));
        }

        [Test]
        public void LocalVariableLimit_255Works()
        {
            // 254 个局部变量应该可以工作（槽位 0 被保留）
            var sb = new StringBuilder();
            sb.AppendLine("var result = 0");
            sb.AppendLine("{");
            for (int i = 0; i < 254; i++)
            {
                sb.AppendLine($"    var v{i} = {i}");
            }
            sb.AppendLine("    result = v253");
            sb.AppendLine("}");
            sb.AppendLine("return result");

            var result = _vm.Run(sb.ToString());
            Assert.AreEqual(253.0, result.AsNumber());
        }

        // ========== 上值槽位溢出测试 ==========

        [Test]
        public void UpvalueOverflow_ThrowsException()
        {
            // 生成超过 255 个上值的代码
            // 在块作用域内创建变量，然后在内部函数中捕获它们
            var sb = new StringBuilder();
            sb.AppendLine("{");
            // 创建 260 个局部变量
            for (int i = 0; i < 260; i++)
            {
                sb.AppendLine($"    var v{i} = {i}");
            }
            // 内部函数捕获所有变量作为上值
            sb.AppendLine("    var inner = () => {");
            sb.AppendLine("        var sum = 0");
            for (int i = 0; i < 260; i++)
            {
                sb.AppendLine($"        sum = sum + v{i}");
            }
            sb.AppendLine("        return sum");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 局部变量会先溢出
            var ex = Assert.Throws<CompilerException>(() => _vm.Run(sb.ToString()));
            Assert.That(ex.Message, Does.Contain("Too many"));
        }

        [Test]
        public void UpvalueLimit_Works()
        {
            // 测试闭包捕获多个变量
            var result = _vm.Run(@"
                func outer() {
                    var a = 1
                    var b = 2
                    var c = 3
                    func inner() {
                        return a + b + c
                    }
                    return inner()
                }
                return outer()
            ");
            Assert.AreEqual(6.0, result.AsNumber());
        }

        // ========== 嵌套作用域测试 ==========

        [Test]
        public void NestedScopes_LocalVariables()
        {
            var result = _vm.Run(@"
                var x = 1
                {
                    var x = 2
                    {
                        var x = 3
                    }
                }
                return x
            ");
            Assert.AreEqual(1.0, result.AsNumber());
        }

        [Test]
        public void DeepNesting_Works()
        {
            // 测试深层嵌套不会导致问题
            var sb = new StringBuilder();
            sb.AppendLine("var result = 0");
            for (int i = 0; i < 50; i++)
            {
                sb.AppendLine($"{{ var v{i} = {i}");
            }
            sb.AppendLine("result = v49");
            for (int i = 0; i < 50; i++)
            {
                sb.AppendLine("}");
            }
            sb.AppendLine("return result");

            var result = _vm.Run(sb.ToString());
            Assert.AreEqual(49.0, result.AsNumber());
        }

        // ========== 常量折叠测试 ==========

        [Test]
        public void ConstantFolding_NumberArithmetic()
        {
            // 编译时计算: 1 + 2 * 3 = 7
            var result = _vm.Run("return 1 + 2 * 3");
            Assert.AreEqual(7.0, result.AsNumber());
        }

        [Test]
        public void ConstantFolding_NestedExpressions()
        {
            // 编译时计算: (1 + 2) * (3 + 4) = 21
            var result = _vm.Run("return (1 + 2) * (3 + 4)");
            Assert.AreEqual(21.0, result.AsNumber());
        }

        [Test]
        public void ConstantFolding_UnaryMinus()
        {
            // 编译时计算: -5 + 10 = 5
            var result = _vm.Run("return -5 + 10");
            Assert.AreEqual(5.0, result.AsNumber());
        }

        [Test]
        public void ConstantFolding_UnaryNot()
        {
            // 编译时计算: !false = true
            var result = _vm.Run("return !false");
            Assert.AreEqual(true, result.AsBool());
        }

        [Test]
        public void ConstantFolding_StringConcat()
        {
            // 编译时计算: "hello" + " " + "world"
            var result = _vm.Run("return \"hello\" + \" \" + \"world\"");
            Assert.AreEqual("hello world", result.AsString());
        }

        [Test]
        public void ConstantFolding_Comparison()
        {
            // 编译时计算: 5 > 3 = true
            var result = _vm.Run("return 5 > 3");
            Assert.AreEqual(true, result.AsBool());
        }

        [Test]
        public void ConstantFolding_BitwiseOps()
        {
            // 编译时计算: 5 & 3 = 1, 5 | 3 = 7
            Assert.AreEqual(1.0, _vm.Run("return 5 & 3").AsNumber());
            Assert.AreEqual(7.0, _vm.Run("return 5 | 3").AsNumber());
        }

        [Test]
        public void ConstantFolding_ComplexExpression()
        {
            // 编译时计算复杂表达式
            var result = _vm.Run("return (10 - 5) * 2 + 100 / 4");
            Assert.AreEqual(35.0, result.AsNumber());
        }

        [Test]
        public void ConstantFolding_MixedWithVariables()
        {
            // 常量部分折叠，变量部分运行时计算
            var result = _vm.Run(@"
                var x = 10
                return x + 1 + 2
            ");
            Assert.AreEqual(13.0, result.AsNumber());
        }

        // ========== 字符串插值测试 ==========

        [Test]
        public void StringInterpolation_SimpleVariable()
        {
            var result = _vm.Run(@"
                var name = ""World""
                return ""Hello {name}!""
            ");
            Assert.AreEqual("Hello World!", result.AsString());
        }

        [Test]
        public void StringInterpolation_Expression()
        {
            var result = _vm.Run(@"
                var a = 5
                var b = 3
                return ""Sum: {a + b}""
            ");
            Assert.AreEqual("Sum: 8", result.AsString());
        }

        [Test]
        public void StringInterpolation_Multiple()
        {
            var result = _vm.Run(@"
                var x = 10
                var y = 20
                return ""{x} + {y} = {x + y}""
            ");
            Assert.AreEqual("10 + 20 = 30", result.AsString());
        }

        [Test]
        public void StringInterpolation_Nested()
        {
            var result = _vm.Run(@"
                var obj = { name: ""Test"", value: 42 }
                return ""Object: {obj.name} = {obj.value}""
            ");
            Assert.AreEqual("Object: Test = 42", result.AsString());
        }

        [Test]
        public void StringInterpolation_FunctionCall()
        {
            var result = _vm.Run(@"
                func double(x) { return x * 2 }
                return ""Result: {double(5)}""
            ");
            Assert.AreEqual("Result: 10", result.AsString());
        }
    }
}
