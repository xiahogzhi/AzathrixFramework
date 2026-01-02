using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 显式导出测试
    /// </summary>
    [TestFixture]
    public class ExportTests
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
        public void Export_VarAccessible()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "math")
                {
                    var code = @"
                        export var PI = 3.14159
                        var SECRET = 42
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "math.panda");
                }
                return (null, null);
            };

            // 导出的变量可以访问
            var result = _vm.Run(@"
                import ""math"" as m
                return m.PI
            ");
            Assert.AreEqual(3.14159, result.AsNumber(), 0.00001);
        }

        [Test]
        public void Export_VarNotExported_ReturnsNull()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "math")
                {
                    var code = @"
                        export var PI = 3.14159
                        var SECRET = 42
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "math.panda");
                }
                return (null, null);
            };

            // 未导出的变量返回 null
            var result = _vm.Run(@"
                import ""math"" as m
                return m.SECRET
            ");
            Assert.IsTrue(result.IsNull);
        }

        [Test]
        public void Export_FuncAccessible()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "utils")
                {
                    var code = @"
                        export func add(a, b) { return a + b }
                        func helper() { return 999 }
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "utils.panda");
                }
                return (null, null);
            };

            // 导出的函数可以调用
            var result = _vm.Run(@"
                import ""utils"" as u
                return u.add(3, 4)
            ");
            Assert.AreEqual(7.0, result.AsNumber());
        }

        [Test]
        public void Export_FuncNotExported_ReturnsNull()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "utils")
                {
                    var code = @"
                        export func add(a, b) { return a + b }
                        func helper() { return 999 }
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "utils.panda");
                }
                return (null, null);
            };

            // 未导出的函数返回 null
            var result = _vm.Run(@"
                import ""utils"" as u
                return u.helper
            ");
            Assert.IsTrue(result.IsNull);
        }

        [Test]
        public void Export_ClassAccessible()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "shapes")
                {
                    var code = @"
                        export class Point {
                            Point(x, y) {
                                this.x = x
                                this.y = y
                            }
                        }
                        class InternalHelper {}
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "shapes.panda");
                }
                return (null, null);
            };

            // 导出的类可以使用
            var result = _vm.Run(@"
                import ""shapes"" as s
                var p = s.Point(3, 4)
                return p.x + p.y
            ");
            Assert.AreEqual(7.0, result.AsNumber());
        }

        [Test]
        public void Export_ClassNotExported_ReturnsNull()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "shapes")
                {
                    var code = @"
                        export class Point {
                            Point(x, y) {
                                this.x = x
                                this.y = y
                            }
                        }
                        class InternalHelper {}
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "shapes.panda");
                }
                return (null, null);
            };

            // 未导出的类返回 null
            var result = _vm.Run(@"
                import ""shapes"" as s
                return s.InternalHelper
            ");
            Assert.IsTrue(result.IsNull);
        }

        [Test]
        public void Export_MultipleExports()
        {
            _vm.CustomLoader = (path) =>
            {
                if (path == "lib")
                {
                    var code = @"
                        export var VERSION = ""1.0""
                        export func greet(name) { return ""Hello "" + name }
                        export class Config {
                            Config() { this.debug = true }
                        }
                        var internal = 123
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "lib.panda");
                }
                return (null, null);
            };

            Assert.AreEqual("1.0", _vm.Run("import \"lib\" as lib\n return lib.VERSION").AsString());
            Assert.AreEqual("Hello World", _vm.Run("import \"lib\" as lib\n return lib.greet(\"World\")").AsString());
            Assert.AreEqual(true, _vm.Run("import \"lib\" as lib\n var c = lib.Config()\n return c.debug").AsBool());
            Assert.IsTrue(_vm.Run("import \"lib\" as lib\n return lib.internal").IsNull);
        }

        [Test]
        public void NoExport_AllAccessible()
        {
            // 如果模块没有任何 export，所有成员都可以访问（向后兼容）
            _vm.CustomLoader = (path) =>
            {
                if (path == "legacy")
                {
                    var code = @"
                        var x = 10
                        func foo() { return 20 }
                    ";
                    return (System.Text.Encoding.UTF8.GetBytes(code), "legacy.panda");
                }
                return (null, null);
            };

            Assert.AreEqual(10.0, _vm.Run("import \"legacy\" as m\n return m.x").AsNumber());
            Assert.AreEqual(20.0, _vm.Run("import \"legacy\" as m\n return m.foo()").AsNumber());
        }
    }
}
