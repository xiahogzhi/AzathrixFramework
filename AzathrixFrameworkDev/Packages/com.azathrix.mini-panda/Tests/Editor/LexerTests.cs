using NUnit.Framework;
using Azathrix.MiniPanda.Lexer;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 词法分析器测试
    /// </summary>
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
}
