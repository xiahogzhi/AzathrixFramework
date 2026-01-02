using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 模块导入测试
    /// </summary>
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
