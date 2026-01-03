using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Azathrix.MiniPanda.Lexer;
using Azathrix.MiniPanda.Parser;

namespace Azathrix.MiniPanda.LSP
{
    /// <summary>
    /// MiniPanda 语言服务 - 提供智能提示功能
    /// </summary>
    public class LanguageService
    {
        // 关键字列表
        private static readonly string[] Keywords = {
            "var", "func", "class", "if", "else", "while", "for", "in",
            "return", "break", "continue", "true", "false", "null",
            "this", "super", "import", "as", "try", "catch", "finally",
            "throw", "enum", "static", "export"
        };

        // 内置函数
        private static readonly Dictionary<string, string> BuiltinFunctions = new Dictionary<string, string>
        {
            { "print", "print(value) - 输出值到控制台" },
            { "type", "type(value) - 返回值的类型名称" },
            { "str", "str(value) - 转换为字符串" },
            { "num", "num(value) - 转换为数字" },
            { "bool", "bool(value) - 转换为布尔值" },
            { "len", "len(collection) - 返回集合长度" },
            { "push", "push(array, value) - 向数组添加元素" },
            { "pop", "pop(array) - 移除并返回数组最后一个元素" },
            { "range", "range(start, end, step?) - 生成数字范围" },
            { "keys", "keys(object) - 返回对象的所有键" },
            { "values", "values(object) - 返回对象的所有值" },
            { "contains", "contains(collection, value) - 检查集合是否包含值" },
            { "slice", "slice(array, start, end?) - 返回数组切片" },
            { "join", "join(array, separator?) - 连接数组元素为字符串" },
            { "split", "split(string, separator) - 分割字符串为数组" },
            { "abs", "abs(number) - 返回绝对值" },
            { "floor", "floor(number) - 向下取整" },
            { "ceil", "ceil(number) - 向上取整" },
            { "round", "round(number) - 四舍五入" },
            { "sqrt", "sqrt(number) - 返回平方根" },
            { "pow", "pow(base, exponent) - 返回幂" },
            { "min", "min(a, b) - 返回较小值" },
            { "max", "max(a, b) - 返回较大值" },
            { "random", "random() - 返回 0-1 之间的随机数" },
            { "randomInt", "randomInt(min, max) - 返回指定范围的随机整数" },
            { "time", "time() - 返回当前时间戳（毫秒）" },
            { "now", "now() - 返回当前日期时间字符串" },
            { "trace", "trace(...args) - 输出调试信息" },
            { "debug", "debug(...args) - 输出调试信息（带堆栈）" },
            { "stacktrace", "stacktrace() - 返回当前调用栈" },
            { "assert", "assert(condition, message?) - 断言条件为真" }
        };

        // 内置对象
        private static readonly Dictionary<string, Dictionary<string, string>> BuiltinObjects = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "date", new Dictionary<string, string>
                {
                    { "now", "date.now() - 返回当前时间戳" },
                    { "format", "date.format(timestamp, format) - 格式化日期" },
                    { "parse", "date.parse(string) - 解析日期字符串" }
                }
            },
            {
                "json", new Dictionary<string, string>
                {
                    { "parse", "json.parse(string) - 解析 JSON 字符串" },
                    { "stringify", "json.stringify(value) - 转换为 JSON 字符串" }
                }
            },
            {
                "regex", new Dictionary<string, string>
                {
                    { "match", "regex.match(pattern, string) - 匹配正则表达式" },
                    { "replace", "regex.replace(pattern, string, replacement) - 替换匹配内容" },
                    { "test", "regex.test(pattern, string) - 测试是否匹配" }
                }
            }
        };

        private readonly ConcurrentDictionary<string, DocumentInfo> _documents = new ConcurrentDictionary<string, DocumentInfo>();

        /// <summary>
        /// 打开文档
        /// </summary>
        public void OpenDocument(string uri, string content)
        {
            var info = new DocumentInfo { Uri = uri, Content = content, Lines = SplitLines(content) };
            AnalyzeDocument(info);
            _documents[uri] = info;
        }

        /// <summary>
        /// 更新文档
        /// </summary>
        public void UpdateDocument(string uri, string content)
        {
            if (_documents.TryGetValue(uri, out var info))
            {
                info.Content = content;
                info.Lines = SplitLines(content);
                AnalyzeDocument(info);
            }
            else
            {
                OpenDocument(uri, content);
            }
        }

        /// <summary>
        /// 关闭文档
        /// </summary>
        public void CloseDocument(string uri)
        {
            _documents.TryRemove(uri, out _);
        }

        /// <summary>
        /// 获取补全项
        /// </summary>
        public List<CompletionItem> GetCompletions(string uri, Position position)
        {
            var items = new List<CompletionItem>();

            if (!_documents.TryGetValue(uri, out var doc)) return items;

            var line = GetLine(doc, position.Line);
            var prefix = GetWordAtPosition(line, position.Character);
            var context = GetCompletionContext(line, position.Character);

            // 根据上下文提供补全
            switch (context)
            {
                case CompletionContext.MemberAccess:
                    items.AddRange(GetMemberCompletions(line, position.Character, doc));
                    break;
                case CompletionContext.Import:
                    // 导入补全（可扩展）
                    break;
                default:
                    // 关键字
                    items.AddRange(Keywords
                        .Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .Select(k => new CompletionItem
                        {
                            Label = k,
                            Kind = CompletionItemKind.Keyword,
                            Detail = "关键字"
                        }));

                    // 内置函数
                    items.AddRange(BuiltinFunctions
                        .Where(f => string.IsNullOrEmpty(prefix) || f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .Select(f => new CompletionItem
                        {
                            Label = f.Key,
                            Kind = CompletionItemKind.Function,
                            Detail = f.Value,
                            InsertText = f.Key + "($0)"
                        }));

                    // 内置对象
                    items.AddRange(BuiltinObjects.Keys
                        .Where(o => string.IsNullOrEmpty(prefix) || o.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .Select(o => new CompletionItem
                        {
                            Label = o,
                            Kind = CompletionItemKind.Module,
                            Detail = "内置对象"
                        }));

                    // 文档中的符号（包括类的方法）
                    items.AddRange(GetAllSymbols(doc.Symbols)
                        .Where(s => string.IsNullOrEmpty(prefix) || s.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .Select(s => new CompletionItem
                        {
                            Label = s.Name,
                            Kind = SymbolKindToCompletionKind(s.Kind),
                            Detail = s.Detail
                        }));
                    break;
            }

            return items;
        }

        /// <summary>
        /// 获取悬停信息
        /// </summary>
        public HoverInfo GetHover(string uri, Position position)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return null;

            var line = GetLine(doc, position.Line);
            var word = GetWordAtPosition(line, position.Character);
            if (string.IsNullOrEmpty(word)) return null;

            // 检查内置函数
            if (BuiltinFunctions.TryGetValue(word, out var funcDoc))
            {
                return new HoverInfo { Contents = $"```\n{funcDoc}\n```" };
            }

            // 检查内置对象
            if (BuiltinObjects.ContainsKey(word))
            {
                var methods = string.Join("\n", BuiltinObjects[word].Values);
                return new HoverInfo { Contents = $"**{word}** (内置对象)\n\n```\n{methods}\n```" };
            }

            // 检查关键字
            if (Keywords.Contains(word))
            {
                return new HoverInfo { Contents = $"**{word}** (关键字)" };
            }

            // 检查文档符号（递归搜索）
            var symbol = FindSymbolByName(doc.Symbols, word);
            if (symbol != null)
            {
                return new HoverInfo
                {
                    Contents = $"**{symbol.Name}** ({GetSymbolKindName(symbol.Kind)})\n\n{symbol.Detail}"
                };
            }

            return null;
        }

        /// <summary>
        /// 获取定义位置
        /// </summary>
        public Location? GetDefinition(string uri, Position position)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return null;

            var line = GetLine(doc, position.Line);
            var word = GetWordAtPosition(line, position.Character);
            if (string.IsNullOrEmpty(word)) return null;

            // 递归搜索符号（包括类的方法）
            var symbol = FindSymbolByName(doc.Symbols, word);
            if (symbol != null)
            {
                return new Location
                {
                    Uri = uri,
                    Range = symbol.SelectionRange
                };
            }

            return null;
        }

        /// <summary>
        /// 递归查找符号
        /// </summary>
        private DocumentSymbol FindSymbolByName(List<DocumentSymbol> symbols, string name)
        {
            foreach (var symbol in symbols)
            {
                if (symbol.Name == name) return symbol;
                if (symbol.Children != null)
                {
                    var child = FindSymbolByName(symbol.Children, name);
                    if (child != null) return child;
                }
            }
            return null;
        }

        /// <summary>
        /// 递归获取所有符号（展平）
        /// </summary>
        private IEnumerable<DocumentSymbol> GetAllSymbols(List<DocumentSymbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                yield return symbol;
                if (symbol.Children != null)
                {
                    foreach (var child in GetAllSymbols(symbol.Children))
                    {
                        yield return child;
                    }
                }
            }
        }

        /// <summary>
        /// 获取诊断信息
        /// </summary>
        public List<Diagnostic> GetDiagnostics(string uri)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return new List<Diagnostic>();
            return doc.Diagnostics;
        }

        /// <summary>
        /// 获取文档符号
        /// </summary>
        public List<DocumentSymbol> GetDocumentSymbols(string uri)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return new List<DocumentSymbol>();
            return doc.Symbols;
        }

        /// <summary>
        /// 获取签名帮助
        /// </summary>
        public SignatureHelp GetSignatureHelp(string uri, Position position)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return null;

            var line = GetLine(doc, position.Line);
            var funcName = GetFunctionNameAtPosition(line, position.Character);
            if (string.IsNullOrEmpty(funcName)) return null;

            // 检查内置函数
            if (BuiltinFunctions.TryGetValue(funcName, out var funcDoc))
            {
                var sig = new SignatureInformation { Label = funcDoc };
                // 解析参数
                var match = Regex.Match(funcDoc, @"\(([^)]*)\)");
                if (match.Success)
                {
                    var paramsStr = match.Groups[1].Value;
                    foreach (var param in paramsStr.Split(','))
                    {
                        var trimmed = param.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            sig.Parameters.Add(new ParameterInformation { Label = trimmed });
                        }
                    }
                }

                return new SignatureHelp
                {
                    Signatures = new List<SignatureInformation> { sig },
                    ActiveParameter = CountCommas(line, position.Character)
                };
            }

            return null;
        }

        #region 私有方法

        private void AnalyzeDocument(DocumentInfo doc)
        {
            doc.Symbols.Clear();
            doc.Diagnostics.Clear();

            try
            {
                var lexer = new Lexer.Lexer(doc.Content);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.Parse();

                // 提取符号
                ExtractSymbols(ast, doc.Symbols);
            }
            catch (Exception ex)
            {
                // 解析错误作为诊断
                doc.Diagnostics.Add(new Diagnostic
                {
                    Range = new Range(0, 0, 0, 0),
                    Severity = DiagnosticSeverity.Error,
                    Message = ex.Message
                });
            }
        }

        private void ExtractSymbols(List<Stmt> statements, List<DocumentSymbol> symbols)
        {
            foreach (var stmt in statements)
            {
                switch (stmt)
                {
                    case VarDecl varStmt:
                        symbols.Add(new DocumentSymbol
                        {
                            Name = varStmt.Name,
                            Kind = SymbolKind.Variable,
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, varStmt.Name.Length)
                        });
                        break;

                    case FuncDecl funcStmt:
                        symbols.Add(new DocumentSymbol
                        {
                            Name = funcStmt.Name,
                            Kind = SymbolKind.Function,
                            Detail = $"func {funcStmt.Name}({string.Join(", ", funcStmt.Parameters)})",
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, funcStmt.Name.Length)
                        });
                        break;

                    case ClassDecl classStmt:
                        var classSymbol = new DocumentSymbol
                        {
                            Name = classStmt.Name,
                            Kind = SymbolKind.Class,
                            Detail = !string.IsNullOrEmpty(classStmt.SuperClass)
                                ? $"class {classStmt.Name} : {classStmt.SuperClass}"
                                : $"class {classStmt.Name}",
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, classStmt.Name.Length),
                            Children = new List<DocumentSymbol>()
                        };

                        foreach (var method in classStmt.Methods)
                        {
                            classSymbol.Children.Add(new DocumentSymbol
                            {
                                Name = method.Name,
                                Kind = SymbolKind.Method,
                                Detail = $"{method.Name}({string.Join(", ", method.Parameters)})",
                                Range = new Range(method.Line - 1, 0, method.Line - 1, 100),
                                SelectionRange = new Range(method.Line - 1, 0, method.Line - 1, method.Name.Length)
                            });
                        }

                        symbols.Add(classSymbol);
                        break;

                    case EnumDecl enumStmt:
                        var enumSymbol = new DocumentSymbol
                        {
                            Name = enumStmt.Name,
                            Kind = SymbolKind.Enum,
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, enumStmt.Name.Length),
                            Children = new List<DocumentSymbol>()
                        };

                        foreach (var member in enumStmt.Members)
                        {
                            enumSymbol.Children.Add(new DocumentSymbol
                            {
                                Name = member.Name,
                                Kind = SymbolKind.EnumMember,
                                Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                                SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, member.Name.Length)
                            });
                        }

                        symbols.Add(enumSymbol);
                        break;
                }
            }
        }

        private static string[] SplitLines(string content)
        {
            return string.IsNullOrEmpty(content) ? Array.Empty<string>() : content.Split('\n');
        }

        private string GetLine(DocumentInfo doc, int lineNumber)
        {
            var lines = doc.Lines ?? Array.Empty<string>();
            return lineNumber < lines.Length ? lines[lineNumber] : "";
        }

        private string GetWordAtPosition(string line, int character)
        {
            if (character > line.Length) character = line.Length;

            var start = character;
            while (start > 0 && IsIdentifierChar(line[start - 1]))
                start--;

            var end = character;
            while (end < line.Length && IsIdentifierChar(line[end]))
                end++;

            return line.Substring(start, end - start);
        }

        private bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private CompletionContext GetCompletionContext(string line, int character)
        {
            if (character > 0 && character <= line.Length && line[character - 1] == '.')
                return CompletionContext.MemberAccess;

            if (line.TrimStart().StartsWith("import"))
                return CompletionContext.Import;

            return CompletionContext.Normal;
        }

        private List<CompletionItem> GetMemberCompletions(string line, int character, DocumentInfo doc)
        {
            var items = new List<CompletionItem>();

            // 获取点号前的对象名
            var dotPos = character - 1;
            if (dotPos < 0 || dotPos >= line.Length || line[dotPos] != '.') return items;

            var objEnd = dotPos;
            var objStart = objEnd;
            while (objStart > 0 && IsIdentifierChar(line[objStart - 1]))
                objStart--;

            var objName = line.Substring(objStart, objEnd - objStart);

            // 检查内置对象
            if (BuiltinObjects.TryGetValue(objName, out var members))
            {
                items.AddRange(members.Select(m => new CompletionItem
                {
                    Label = m.Key,
                    Kind = CompletionItemKind.Method,
                    Detail = m.Value
                }));
            }

            return items;
        }

        private string GetFunctionNameAtPosition(string line, int character)
        {
            // 向前查找函数名
            var parenPos = line.LastIndexOf('(', Math.Min(character, line.Length - 1));
            if (parenPos < 0) return null;

            var end = parenPos;
            var start = end;
            while (start > 0 && IsIdentifierChar(line[start - 1]))
                start--;

            return line.Substring(start, end - start);
        }

        private int CountCommas(string line, int character)
        {
            var count = 0;
            var depth = 0;
            var parenStart = line.LastIndexOf('(', Math.Min(character, line.Length - 1));

            for (int i = parenStart + 1; i < character && i < line.Length; i++)
            {
                switch (line[i])
                {
                    case '(': depth++; break;
                    case ')': depth--; break;
                    case ',' when depth == 0: count++; break;
                }
            }

            return count;
        }

        private CompletionItemKind SymbolKindToCompletionKind(SymbolKind kind)
        {
            return kind switch
            {
                SymbolKind.Function => CompletionItemKind.Function,
                SymbolKind.Method => CompletionItemKind.Method,
                SymbolKind.Class => CompletionItemKind.Class,
                SymbolKind.Variable => CompletionItemKind.Variable,
                SymbolKind.Enum => CompletionItemKind.Enum,
                SymbolKind.EnumMember => CompletionItemKind.EnumMember,
                _ => CompletionItemKind.Text
            };
        }

        private string GetSymbolKindName(SymbolKind kind)
        {
            return kind switch
            {
                SymbolKind.Function => "函数",
                SymbolKind.Method => "方法",
                SymbolKind.Class => "类",
                SymbolKind.Variable => "变量",
                SymbolKind.Enum => "枚举",
                SymbolKind.EnumMember => "枚举成员",
                _ => "符号"
            };
        }

        #endregion

        private enum CompletionContext
        {
            Normal,
            MemberAccess,
            Import
        }

        private class DocumentInfo
        {
            public string Uri { get; set; }
            public string Content { get; set; }
            public string[] Lines { get; set; } = Array.Empty<string>();
            public List<DocumentSymbol> Symbols { get; } = new List<DocumentSymbol>();
            public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
        }
    }
}




