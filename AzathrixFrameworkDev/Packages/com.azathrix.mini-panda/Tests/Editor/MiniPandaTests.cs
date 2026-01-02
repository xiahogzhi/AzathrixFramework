using System;
using System.Collections.Generic;
using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.Exceptions;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    [TestFixture]
    public class MiniPandaTests
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

        // ========== Lexer Tests ==========

        [TestFixture]
        public class LexerTests
        {
            [Test]
            public void Tokenize_Numbers()
            {
                var lexer = new Lexer.Lexer("42 3.14 0.5");
                var tokens = lexer.Tokenize();

                Assert.AreEqual(TokenType.Number, tokens[0].Type);
                Assert.AreEqual(42.0, tokens[0].Literal);
                Assert.AreEqual(TokenType.Number, tokens[1].Type);
                Assert.AreEqual(3.14, tokens[1].Literal);
            }

            [Test]
            public void Tokenize_Strings()
            {
                var lexer = new Lexer.Lexer("\"hello\" \"world\"");
                var tokens = lexer.Tokenize();

                Assert.AreEqual(TokenType.String, tokens[0].Type);
                Assert.AreEqual(TokenType.String, tokens[1].Type);
            }

            [Test]
            public void Tokenize_Keywords()
            {
                var lexer = new Lexer.Lexer("var func if else while for return class import global as");
                var tokens = lexer.Tokenize();

                Assert.AreEqual(TokenType.Var, tokens[0].Type);
                Assert.AreEqual(TokenType.Func, tokens[1].Type);
                Assert.AreEqual(TokenType.If, tokens[2].Type);
                Assert.AreEqual(TokenType.Else, tokens[3].Type);
                Assert.AreEqual(TokenType.While, tokens[4].Type);
                Assert.AreEqual(TokenType.For, tokens[5].Type);
                Assert.AreEqual(TokenType.Return, tokens[6].Type);
                Assert.AreEqual(TokenType.Class, tokens[7].Type);
                Assert.AreEqual(TokenType.Import, tokens[8].Type);
                Assert.AreEqual(TokenType.Global, tokens[9].Type);
                Assert.AreEqual(TokenType.As, tokens[10].Type);
            }

            [Test]
            public void Tokenize_Operators()
            {
                var lexer = new Lexer.Lexer("+ - * / == != < > <= >=");
                var tokens = lexer.Tokenize();

                Assert.AreEqual(TokenType.Plus, tokens[0].Type);
                Assert.AreEqual(TokenType.Minus, tokens[1].Type);
                Assert.AreEqual(TokenType.Star, tokens[2].Type);
                Assert.AreEqual(TokenType.Slash, tokens[3].Type);
                Assert.AreEqual(TokenType.EqualEqual, tokens[4].Type);
                Assert.AreEqual(TokenType.BangEqual, tokens[5].Type);
            }

            [Test]
            public void Tokenize_Comments()
            {
                var lexer = new Lexer.Lexer("42 // comment\n43");
                var tokens = lexer.Tokenize();

                Assert.AreEqual(TokenType.Number, tokens[0].Type);
                Assert.AreEqual(42.0, tokens[0].Literal);
                Assert.AreEqual(TokenType.Newline, tokens[1].Type);
                Assert.AreEqual(TokenType.Number, tokens[2].Type);
                Assert.AreEqual(43.0, tokens[2].Literal);
            }
        }

        // ========== Expression Tests ==========

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
            Assert.AreEqual(5.0, _vm.Eval("-(-5)").AsNumber()); // 两个负号用括号
        }

        // ========== Compound Assignment Tests ==========

        [Test]
        public void Run_CompoundAssignment()
        {
            Assert.AreEqual(15.0, _vm.Run("var x = 10; x += 5; return x").AsNumber());
            Assert.AreEqual(12.0, _vm.Run("var y = 20; y -= 8; return y").AsNumber());
            Assert.AreEqual(18.0, _vm.Run("var z = 6; z *= 3; return z").AsNumber());
            Assert.AreEqual(25.0, _vm.Run("var w = 100; w /= 4; return w").AsNumber());
        }

        [Test]
        public void Run_IncrementDecrement()
        {
            Assert.AreEqual(6.0, _vm.Run("var a = 5; return ++a").AsNumber());
            Assert.AreEqual(5.0, _vm.Run("var b = 5; return b++").AsNumber());
            Assert.AreEqual(4.0, _vm.Run("var c = 5; return --c").AsNumber());
            Assert.AreEqual(5.0, _vm.Run("var d = 5; return d--").AsNumber());
        }

        [Test]
        public void Eval_TernaryOperator()
        {
            Assert.AreEqual(1.0, _vm.Eval("true ? 1 : 2").AsNumber());
            Assert.AreEqual(2.0, _vm.Eval("false ? 1 : 2").AsNumber());
            Assert.AreEqual(10.0, _vm.Eval("5 > 3 ? 10 : 20").AsNumber());
        }

        // ========== Variable Tests ==========

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

        // ========== Control Flow Tests ==========

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

        // ========== For Loop Tests ==========

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

        // ========== Function Tests ==========

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

        // ========== Array Tests ==========

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

        // ========== Object Tests ==========

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

        // ========== Class Tests ==========

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

        // ========== Builtin Tests ==========

        [Test]
        public void Builtin_Type()
        {
            Assert.AreEqual("number", _vm.Eval("type(42)").AsString());
            Assert.AreEqual("string", _vm.Eval("type(\"hello\")").AsString());
            Assert.AreEqual("bool", _vm.Eval("type(true)").AsString());
            Assert.AreEqual("null", _vm.Eval("type(null)").AsString());
        }

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

        [Test]
        public void Builtin_Len()
        {
            Assert.AreEqual(3.0, _vm.Eval("len([1, 2, 3])").AsNumber());
            Assert.AreEqual(5.0, _vm.Eval("len(\"hello\")").AsNumber());
        }

        [Test]
        public void Builtin_Range()
        {
            var arr = _vm.Run("return range(5)").As<MiniPandaArray>();
            Assert.AreEqual(5, arr.Length);
            Assert.AreEqual(0.0, arr.Get(0).AsNumber());
            Assert.AreEqual(4.0, arr.Get(4).AsNumber());
        }

        // ========== C# Interop Tests ==========

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
            _vm.SetGlobal("square", Value.FromObject(NativeFunc.Create((Value v) =>
                Value.FromNumber(v.AsNumber() * v.AsNumber()))));

            var result = _vm.Eval("square(5)");
            Assert.AreEqual(25.0, result.AsNumber());
        }

        [Test]
        public void CSharp_CallScriptFunction()
        {
            _vm.SetGlobal("multiply", Value.FromObject(NativeFunc.Create((Value a, Value b) =>
                Value.FromNumber(a.AsNumber() * b.AsNumber()))));
            Assert.AreEqual(42.0, _vm.Run("return multiply(6, 7)").AsNumber());
        }

        [Test]
        public void Eval_WithEnvironment()
        {
            var result = _vm.Eval("x + y", new { x = 10, y = 20 });
            Assert.AreEqual(30.0, result.AsNumber());
        }

        // ========== Scope Tests ==========

        [Test]
        public void Run_LocalScope()
        {
            Assert.AreEqual(10.0, _vm.Run(@"
                var x = 10
                { var x = 20 }
                return x
            ").AsNumber());
        }

        // ========== Cache Tests ==========

        [Test]
        public void Compile_CacheWorks()
        {
            var code = "1 + 2";
            var compiled1 = _vm.Compile(code);
            var compiled2 = _vm.Compile(code);
            Assert.AreSame(compiled1, compiled2);
        }

        // ========== Global Keyword Tests ==========

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
            public void GlobalImport_VisibleAcrossRuns()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "math")
                    {
                        var code = "func square(x) { return x * x }";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "math.panda");
                    }
                    return (null, null);
                };

                vm.Run("global import \"math\" as m");
                var result = vm.Run("return m.square(5)");

                Assert.AreEqual(25.0, result.AsNumber());
                vm.Shutdown();
            }

            [Test]
            public void GlobalVar_InsideFunction_VisibleAcrossRuns()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.Run(@"
                    func init() {
                        global var counter = 5
                    }
                    init()
                ");
                var result = vm.Run("return counter");

                Assert.AreEqual(5.0, result.AsNumber());
                vm.Shutdown();
            }

            [Test]
            public void GlobalImport_InsideFunction_VisibleAcrossRuns()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "math")
                    {
                        var code = "func square(x) { return x * x }";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "math.panda");
                    }
                    return (null, null);
                };

                vm.Run(@"
                    func init() {
                        global import ""math"" as m
                    }
                    init()
                ");
                var result = vm.Run("return m.square(6)");

                Assert.AreEqual(36.0, result.AsNumber());
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

                // _G should provide access to builtins
                var result = vm.Run("return _G.abs(-5)");

                Assert.AreEqual(5.0, result.AsNumber());
                vm.Shutdown();
            }

            [Test]
            public void GlobalTable_ModifyFromNestedScope()
            {
                var vm = new MiniPanda();
                vm.Start();

                var result = vm.Run(@"
                    global var counter = 0
                    func increment() {
                        _G.counter = _G.counter + 1
                    }
                    increment()
                    increment()
                    return counter
                ");

                Assert.AreEqual(2.0, result.AsNumber());
                vm.Shutdown();
            }
        }

        // ========== Generic Run/Eval Tests ==========

        [TestFixture]
        public class GenericRunEvalTests
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
                Assert.AreEqual(30, _vm.Eval<int>("x + y", new { x = 10, y = 20 }));
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
        }

        // ========== Import Tests ==========

        [TestFixture]
        public class ImportTests
        {
            [Test]
            public void Parse_ImportStatement()
            {
                var lexer = new Lexer.Lexer("import \"utils\" as u");
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var stmts = parser.Parse();

                Assert.AreEqual(1, stmts.Count);
                var import = stmts[0] as ImportStmt;
                Assert.IsNotNull(import);
                Assert.AreEqual("utils", import.Path);
                Assert.AreEqual("u", import.Alias);
                Assert.IsFalse(import.IsGlobal);
            }

            [Test]
            public void Parse_GlobalImportStatement()
            {
                var lexer = new Lexer.Lexer("global import \"config\" as cfg");
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var stmts = parser.Parse();

                Assert.AreEqual(1, stmts.Count);
                var import = stmts[0] as ImportStmt;
                Assert.IsNotNull(import);
                Assert.AreEqual("config", import.Path);
                Assert.AreEqual("cfg", import.Alias);
                Assert.IsTrue(import.IsGlobal);
            }

            [Test]
            public void ConvertPath_DotToSlash()
            {
                Assert.AreEqual("utils/log", MiniPanda.ConvertPath("utils.log"));
                Assert.AreEqual("math/vector/utils", MiniPanda.ConvertPath("math.vector.utils"));
                Assert.AreEqual("simple", MiniPanda.ConvertPath("simple"));
            }

            [Test]
            public void IsBytecode_DetectsHeader()
            {
                var bytecode = new byte[] { (byte)'M', (byte)'P', (byte)'B', (byte)'C', 0, 0 };
                var source = System.Text.Encoding.UTF8.GetBytes("var x = 1");

                Assert.IsTrue(MiniPanda.IsBytecode(bytecode));
                Assert.IsFalse(MiniPanda.IsBytecode(source));
                Assert.IsFalse(MiniPanda.IsBytecode(null));
                Assert.IsFalse(MiniPanda.IsBytecode(new byte[] { 1, 2 }));
            }

            [Test]
            public void Import_WithCustomLoader()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "math/utils")
                    {
                        var code = "func add(a, b) { return a + b }";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "math/utils.panda");
                    }
                    return (null, null);
                };

                var result = vm.Run(@"
                    import ""math.utils"" as m
                    return m.add(3, 4)
                ");

                Assert.AreEqual(7.0, result.AsNumber());
                vm.Shutdown();
            }

            [Test]
            public void Import_ModuleCaching()
            {
                var vm = new MiniPanda();
                vm.Start();

                int loadCount = 0;
                vm.CustomLoader = (path) =>
                {
                    if (path == "counter")
                    {
                        loadCount++;
                        var code = "var count = 0";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "counter.panda");
                    }
                    return (null, null);
                };

                vm.Run(@"
                    import ""counter"" as c1
                    import ""counter"" as c2
                ");

                Assert.AreEqual(1, loadCount);
                vm.Shutdown();
            }

            [Test]
            public void Import_ModuleVariables()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "config")
                    {
                        var code = @"
                            var VERSION = ""1.0.0""
                            var DEBUG = true
                            func getInfo() { return VERSION }
                        ";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "config.panda");
                    }
                    return (null, null);
                };

                Assert.AreEqual("1.0.0", vm.Run("import \"config\" as cfg\n return cfg.VERSION").AsString());
                Assert.AreEqual(true, vm.Run("import \"config\" as cfg\n return cfg.DEBUG").AsBool());
                Assert.AreEqual("1.0.0", vm.Run("import \"config\" as cfg\n return cfg.getInfo()").AsString());
                vm.Shutdown();
            }

            [Test]
            public void Import_WithoutAlias()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "utils")
                    {
                        var code = "func helper() { return 42 }";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "utils.panda");
                    }
                    return (null, null);
                };

                var result = vm.Run(@"
                    import ""utils""
                    return utils.helper()
                ");
                Assert.AreEqual(42.0, result.AsNumber());
                vm.Shutdown();
            }

            [Test]
            public void LoadModule_API()
            {
                var vm = new MiniPanda();
                vm.Start();

                var code = "var PI = 3.14159; func area(r) { return PI * r * r }";
                vm.LoadModule(System.Text.Encoding.UTF8.GetBytes(code), "math", "math.panda");

                Assert.AreEqual(3.14159, vm.Run("import \"math\" as m\n return m.PI").AsNumber(), 0.00001);
                Assert.AreEqual(12.56636, vm.Run("import \"math\" as m\n return m.area(2)").AsNumber(), 0.0001);
                vm.Shutdown();
            }

            [Test]
            public void Import_NestedPath()
            {
                var vm = new MiniPanda();
                vm.Start();

                vm.CustomLoader = (path) =>
                {
                    if (path == "math/vector")
                    {
                        var code = @"
                            func create(x, y) { return {x: x, y: y} }
                            func add(a, b) { return create(a.x + b.x, a.y + b.y) }
                        ";
                        return (System.Text.Encoding.UTF8.GetBytes(code), "math/vector.panda");
                    }
                    return (null, null);
                };

                var result = vm.Run(@"
                    import ""math.vector"" as vec
                    var v1 = vec.create(1, 2)
                    var v2 = vec.create(3, 4)
                    var v3 = vec.add(v1, v2)
                    return [v3.x, v3.y]
                ");
                var arr = result.AsObject() as MiniPandaArray;
                Assert.AreEqual(4.0, arr.Get(0).AsNumber());
                Assert.AreEqual(6.0, arr.Get(1).AsNumber());
                vm.Shutdown();
            }
        }
    }

    // ========== Error Handling Tests ==========
    [TestFixture]
    public class ErrorHandlingTests
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
        public void Error_DivisionByZero()
        {
            var result = _vm.Eval("10 / 0");
            Assert.IsTrue(double.IsInfinity(result.AsNumber()));
        }

        [Test]
        public void Error_InvalidFunctionCall()
        {
            Assert.Throws<MiniPandaRuntimeException>(() => _vm.Run("var x = 42; x()"));
        }

        [Test]
        public void Error_SyntaxError()
        {
            Assert.Throws<ParserException>(() => _vm.Run("var x = "));
        }
    }

    // ========== Closure/Upvalue Tests ==========
    [TestFixture]
    public class ClosureTests
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

    // ========== Additional Builtin Tests ==========
    [TestFixture]
    public class AdditionalBuiltinTests
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
    }

    // ========== Edge Case Tests ==========
    [TestFixture]
    public class EdgeCaseTests
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
        public void EdgeCase_EmptyArray()
        {
            var arr = _vm.Run("return []").As<MiniPandaArray>();
            Assert.AreEqual(0, arr.Length);
        }

        [Test]
        public void EdgeCase_EmptyObject()
        {
            var obj = _vm.Run("return {}").As<MiniPandaObject>();
            Assert.IsNotNull(obj);
        }

        [Test]
        public void EdgeCase_NullComparison()
        {
            Assert.IsTrue(_vm.Eval("null == null").AsBool());
            Assert.IsFalse(_vm.Eval("null == 0").AsBool());
            Assert.IsFalse(_vm.Eval("null == false").AsBool());
        }

        [Test]
        public void EdgeCase_StringConcatenation()
        {
            Assert.AreEqual("hello world", _vm.Eval("\"hello\" + \" \" + \"world\"").AsString());
        }

        [Test]
        public void EdgeCase_NestedArrays()
        {
            Assert.AreEqual(4.0, _vm.Run("var arr = [[1, 2], [3, 4]]; return arr[1][1]").AsNumber());
        }

        [Test]
        public void EdgeCase_NestedObjects()
        {
            Assert.AreEqual(42.0, _vm.Run("var obj = {inner: {value: 42}}; return obj.inner.value").AsNumber());
        }

        [Test]
        public void EdgeCase_FunctionAsValue()
        {
            Assert.AreEqual("hello", _vm.Run(@"
                func greet() { return ""hello"" }
                var f = greet
                return f()
            ").AsString());
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

    // ========== Value Type Tests ==========
    [TestFixture]
    public class ValueTypeTests
    {
        [Test]
        public void Value_FromNumber()
        {
            var v = Value.FromNumber(42);
            Assert.IsTrue(v.IsNumber);
            Assert.AreEqual(42.0, v.AsNumber());
        }

        [Test]
        public void Value_FromBool()
        {
            var vTrue = Value.FromBool(true);
            var vFalse = Value.FromBool(false);
            Assert.IsTrue(vTrue.IsBool);
            Assert.IsTrue(vTrue.AsBool());
            Assert.IsFalse(vFalse.AsBool());
        }

        [Test]
        public void Value_FromString()
        {
            var v = Value.FromObject(new MiniPandaString("hello"));
            Assert.IsTrue(v.IsString);
            Assert.AreEqual("hello", v.AsString());
        }

        [Test]
        public void Value_Null()
        {
            var v = Value.Null;
            Assert.IsTrue(v.IsNull);
        }

        [Test]
        public void Value_Equality()
        {
            var a = Value.FromNumber(42);
            var b = Value.FromNumber(42);
            var c = Value.FromNumber(43);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }
    }
}
