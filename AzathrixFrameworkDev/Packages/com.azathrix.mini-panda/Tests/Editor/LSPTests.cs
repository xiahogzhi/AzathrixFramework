using NUnit.Framework;
using Azathrix.MiniPanda.LSP;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// LSP 语言服务测试
    /// </summary>
    [TestFixture]
    public class LSPTests
    {
        private LanguageService _service;

        [SetUp]
        public void Setup()
        {
            _service = new LanguageService();
        }

        [Test]
        public void Completion_Keywords()
        {
            _service.OpenDocument("test://test.panda", "");
            var completions = _service.GetCompletions("test://test.panda", new Position(0, 0));

            Assert.IsTrue(completions.Exists(c => c.Label == "var"));
            Assert.IsTrue(completions.Exists(c => c.Label == "func"));
            Assert.IsTrue(completions.Exists(c => c.Label == "class"));
            Assert.IsTrue(completions.Exists(c => c.Label == "if"));
            Assert.IsTrue(completions.Exists(c => c.Label == "for"));
        }

        [Test]
        public void Completion_BuiltinFunctions()
        {
            _service.OpenDocument("test://test.panda", "");
            var completions = _service.GetCompletions("test://test.panda", new Position(0, 0));

            Assert.IsTrue(completions.Exists(c => c.Label == "print"));
            Assert.IsTrue(completions.Exists(c => c.Label == "len"));
            Assert.IsTrue(completions.Exists(c => c.Label == "type"));
        }

        [Test]
        public void Completion_BuiltinObjects()
        {
            _service.OpenDocument("test://test.panda", "");
            var completions = _service.GetCompletions("test://test.panda", new Position(0, 0));

            Assert.IsTrue(completions.Exists(c => c.Label == "date"));
            Assert.IsTrue(completions.Exists(c => c.Label == "json"));
            Assert.IsTrue(completions.Exists(c => c.Label == "regex"));
        }

        [Test]
        public void Completion_FilterByPrefix()
        {
            _service.OpenDocument("test://test.panda", "pr");
            var completions = _service.GetCompletions("test://test.panda", new Position(0, 2));

            Assert.IsTrue(completions.Exists(c => c.Label == "print"));
            Assert.IsFalse(completions.Exists(c => c.Label == "len"));
        }

        [Test]
        public void Hover_BuiltinFunction()
        {
            _service.OpenDocument("test://test.panda", "print");
            var hover = _service.GetHover("test://test.panda", new Position(0, 2));

            Assert.IsNotNull(hover);
            Assert.IsTrue(hover.Contents.Contains("print"));
        }

        [Test]
        public void Hover_Keyword()
        {
            _service.OpenDocument("test://test.panda", "var x = 1");
            var hover = _service.GetHover("test://test.panda", new Position(0, 1));

            Assert.IsNotNull(hover);
            Assert.IsTrue(hover.Contents.Contains("var"));
            Assert.IsTrue(hover.Contents.Contains("关键字"));
        }

        [Test]
        public void DocumentSymbols_Variable()
        {
            _service.OpenDocument("test://test.panda", "var x = 1");
            var symbols = _service.GetDocumentSymbols("test://test.panda");

            Assert.AreEqual(1, symbols.Count);
            Assert.AreEqual("x", symbols[0].Name);
            Assert.AreEqual(SymbolKind.Variable, symbols[0].Kind);
        }

        [Test]
        public void DocumentSymbols_Function()
        {
            _service.OpenDocument("test://test.panda", "func add(a, b) { return a + b }");
            var symbols = _service.GetDocumentSymbols("test://test.panda");

            Assert.AreEqual(1, symbols.Count);
            Assert.AreEqual("add", symbols[0].Name);
            Assert.AreEqual(SymbolKind.Function, symbols[0].Kind);
        }

        [Test]
        public void DocumentSymbols_Class()
        {
            var code = @"
class Point {
    Point(x, y) {
        this.x = x
        this.y = y
    }
}";
            _service.OpenDocument("test://test.panda", code);
            var symbols = _service.GetDocumentSymbols("test://test.panda");

            Assert.AreEqual(1, symbols.Count);
            Assert.AreEqual("Point", symbols[0].Name);
            Assert.AreEqual(SymbolKind.Class, symbols[0].Kind);
        }

        [Test]
        public void Definition_Variable()
        {
            _service.OpenDocument("test://test.panda", "var x = 1\nprint(x)");
            var location = _service.GetDefinition("test://test.panda", new Position(1, 6));

            Assert.IsNotNull(location);
            Assert.AreEqual("test://test.panda", location.Value.Uri);
        }

        [Test]
        public void SignatureHelp_BuiltinFunction()
        {
            _service.OpenDocument("test://test.panda", "print(");
            var help = _service.GetSignatureHelp("test://test.panda", new Position(0, 6));

            Assert.IsNotNull(help);
            Assert.AreEqual(1, help.Signatures.Count);
            Assert.IsTrue(help.Signatures[0].Label.Contains("print"));
        }

        [Test]
        public void Diagnostics_SyntaxError()
        {
            _service.OpenDocument("test://test.panda", "var x = ");
            var diagnostics = _service.GetDiagnostics("test://test.panda");

            Assert.IsTrue(diagnostics.Count > 0);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        }

        [Test]
        public void UpdateDocument()
        {
            _service.OpenDocument("test://test.panda", "var x = 1");
            var symbols1 = _service.GetDocumentSymbols("test://test.panda");
            Assert.AreEqual(1, symbols1.Count);

            _service.UpdateDocument("test://test.panda", "var x = 1\nvar y = 2");
            var symbols2 = _service.GetDocumentSymbols("test://test.panda");
            Assert.AreEqual(2, symbols2.Count);
        }

        [Test]
        public void CloseDocument()
        {
            _service.OpenDocument("test://test.panda", "var x = 1");
            _service.CloseDocument("test://test.panda");

            var symbols = _service.GetDocumentSymbols("test://test.panda");
            Assert.AreEqual(0, symbols.Count);
        }
    }
}
