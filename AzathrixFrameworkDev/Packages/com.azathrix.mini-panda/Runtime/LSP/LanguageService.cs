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

        // 类型信息缓存：变量名 -> 类型名
        private readonly Dictionary<string, string> _variableTypes = new Dictionary<string, string>();
        // 导入的模块：别名 -> 模块路径
        private readonly Dictionary<string, string> _importedModules = new Dictionary<string, string>();

        /// <summary>
        /// 打开文档
        /// </summary>
        public void OpenDocument(string uri, string content)
        {
            UnityEngine.Debug.Log($"[LSP] OpenDocument: {uri}");
            var info = new DocumentInfo { Uri = uri, Content = content, Lines = SplitLines(content) };
            AnalyzeDocument(info);
            _documents[uri] = info;
        }

        /// <summary>
        /// 更新文档
        /// </summary>
        public void UpdateDocument(string uri, string content)
        {
            // UnityEngine.Debug.Log($"[LSP] UpdateDocument: {uri}");
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
                var content = $"**{symbol.Name}** ({GetSymbolKindName(symbol.Kind)})";
                if (!string.IsNullOrEmpty(symbol.Detail))
                    content += $"\n\n```\n{symbol.Detail}\n```";
                if (!string.IsNullOrEmpty(symbol.Documentation))
                    content += $"\n\n{symbol.Documentation}";
                return new HoverInfo { Contents = content };
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

            // 1. 检查是否是导入的模块别名
            if (doc.ImportedModules.TryGetValue(word, out var modulePath))
            {
                var normalizedPath = modulePath.Replace(".", "/");
                var moduleDoc = _documents.Values.FirstOrDefault(d =>
                    d.Uri.EndsWith(normalizedPath + ".panda", StringComparison.OrdinalIgnoreCase) ||
                    d.Uri.EndsWith(normalizedPath.Replace("/", "\\") + ".panda", StringComparison.OrdinalIgnoreCase));
                if (moduleDoc != null)
                {
                    return new Location { Uri = moduleDoc.Uri, Range = new Range(0, 0, 0, 0) };
                }
            }

            // 2. 检查是否是模块成员访问 (module.member)
            var dotIndex = line.LastIndexOf('.', Math.Min(position.Character, line.Length - 1));
            if (dotIndex > 0)
            {
                var beforeDot = GetWordBeforeDot(line, dotIndex);
                if (doc.ImportedModules.TryGetValue(beforeDot, out var modPath))
                {
                    var normalizedModPath = modPath.Replace(".", "/");
                    var moduleDoc = _documents.Values.FirstOrDefault(d =>
                        d.Uri.EndsWith(normalizedModPath + ".panda", StringComparison.OrdinalIgnoreCase) ||
                        d.Uri.EndsWith(normalizedModPath.Replace("/", "\\") + ".panda", StringComparison.OrdinalIgnoreCase));
                    if (moduleDoc != null)
                    {
                        var memberSymbol = FindSymbolByName(moduleDoc.Symbols, word);
                        if (memberSymbol != null)
                        {
                            return new Location { Uri = moduleDoc.Uri, Range = memberSymbol.SelectionRange };
                        }
                    }
                }
            }

            // 3. 递归搜索当前文档符号
            var symbol = FindSymbolByName(doc.Symbols, word);
            if (symbol != null)
            {
                return new Location { Uri = uri, Range = symbol.SelectionRange };
            }

            return null;
        }

        private string GetWordBeforeDot(string line, int dotIndex)
        {
            var end = dotIndex;
            var start = end;
            while (start > 0 && IsIdentifierChar(line[start - 1]))
                start--;
            return line.Substring(start, end - start);
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

        /// <summary>
        /// 格式化文档
        /// </summary>
        public List<TextEdit> FormatDocument(string uri, int tabSize = 4, bool insertSpaces = true)
        {
            if (!_documents.TryGetValue(uri, out var doc)) return new List<TextEdit>();

            var edits = new List<TextEdit>();
            var lines = doc.Lines;
            var indentChar = insertSpaces ? new string(' ', tabSize) : "\t";
            var indentLevel = 0;
            var formattedLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var trimmed = line.Trim();

                // 空行保持
                if (string.IsNullOrEmpty(trimmed))
                {
                    formattedLines.Add("");
                    continue;
                }

                // 减少缩进的行（以 } 开头）
                if (trimmed.StartsWith("}"))
                    indentLevel = Math.Max(0, indentLevel - 1);

                // 构建格式化后的行
                var formatted = string.Concat(Enumerable.Repeat(indentChar, indentLevel)) + trimmed;
                formattedLines.Add(formatted);

                // 增加缩进的行（仅以 { 结尾时）
                if (trimmed.EndsWith("{"))
                    indentLevel++;
            }

            // 生成单个编辑替换整个文档
            var newContent = string.Join("\n", formattedLines);
            if (newContent != doc.Content.TrimEnd('\r').Replace("\r\n", "\n"))
            {
                edits.Add(new TextEdit
                {
                    Range = new Range(0, 0, lines.Length, 0),
                    NewText = newContent
                });
            }

            return edits;
        }

        /// <summary>
        /// 重命名符号
        /// </summary>
        public WorkspaceEdit Rename(string uri, Position position, string newName)
        {
            var edit = new WorkspaceEdit();
            if (!_documents.TryGetValue(uri, out var doc)) return edit;

            var line = GetLine(doc, position.Line);
            var oldName = GetWordAtPosition(line, position.Character);
            if (string.IsNullOrEmpty(oldName) || oldName == newName) return edit;

            // 检查是否是关键字或内置函数（不允许重命名）
            if (Keywords.Contains(oldName) || BuiltinFunctions.ContainsKey(oldName))
                return edit;

            // 查找所有引用并替换
            var edits = new List<TextEdit>();
            var pattern = $@"\b{Regex.Escape(oldName)}\b";
            var regex = new Regex(pattern);

            for (int i = 0; i < doc.Lines.Length; i++)
            {
                var lineText = doc.Lines[i];
                var matches = regex.Matches(lineText);
                foreach (Match match in matches)
                {
                    edits.Add(new TextEdit
                    {
                        Range = new Range(i, match.Index, i, match.Index + match.Length),
                        NewText = newName
                    });
                }
            }

            if (edits.Count > 0)
                edit.Changes[uri] = edits;

            return edit;
        }

        #region 私有方法

        private void AnalyzeDocument(DocumentInfo doc)
        {
            // UnityEngine.Debug.Log($"[LSP] AnalyzeDocument: uri={doc.Uri}, contentLength={doc.Content?.Length ?? 0}");

            try
            {
                var lexer = new Lexer.Lexer(doc.Content);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.Parse();

                // UnityEngine.Debug.Log($"[LSP] Parsed {ast.Count} statements");

                // 解析成功后才清空并更新数据
                doc.Symbols.Clear();
                doc.ExportedSymbols.Clear();
                doc.Diagnostics.Clear();
                doc.VariableTypes.Clear();
                doc.ImportedModules.Clear();

                // 提取符号
                ExtractSymbols(ast, doc);

                // UnityEngine.Debug.Log($"[LSP] Extracted {doc.Symbols.Count} symbols, {doc.VariableTypes.Count} variable types");
            }
            catch (Parser.ParserException pex)
            {
                // 解析失败时保留之前的符号数据，只更新诊断信息
                UnityEngine.Debug.Log($"[LSP] Parse error (keeping previous symbols): {pex.Message}");
                doc.Diagnostics.Clear();
                var line = Math.Max(0, pex.Line - 1); // LSP 行号从 0 开始
                var col = Math.Max(0, pex.Column - 1);
                doc.Diagnostics.Add(new Diagnostic
                {
                    Range = new Range(line, col, line, col + 10),
                    Severity = DiagnosticSeverity.Error,
                    Message = pex.Message
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"[LSP] Parse error: {ex.Message}");
                doc.Diagnostics.Clear();
                doc.Diagnostics.Add(new Diagnostic
                {
                    Range = new Range(0, 0, 0, 0),
                    Severity = DiagnosticSeverity.Error,
                    Message = ex.Message
                });
            }
        }

        private void ExtractSymbols(List<Stmt> statements, DocumentInfo doc)
        {
            // 第一遍：收集所有类和枚举定义
            var classNames = new HashSet<string>();
            foreach (var stmt in statements)
            {
                if (stmt is ClassDecl classDecl)
                    classNames.Add(classDecl.Name);
            }

            // 第二遍：提取所有符号
            for (int i = 0; i < statements.Count; i++)
            {
                var stmt = statements[i];
                // 获取前一行的注释
                var comment = GetPrecedingComment(doc.Lines, stmt.Line - 1);
                var isExported = false;

                switch (stmt)
                {
                    case ImportStmt importStmt:
                        // 记录导入的模块
                        var alias = importStmt.Alias ?? System.IO.Path.GetFileNameWithoutExtension(importStmt.Path);
                        doc.ImportedModules[alias] = importStmt.Path;
                        break;

                    case VarDecl varStmt:
                        isExported = varStmt.IsExport || varStmt.IsGlobal;
                        var varSymbol = new DocumentSymbol
                        {
                            Name = varStmt.Name,
                            Kind = SymbolKind.Variable,
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, varStmt.Name.Length),
                            Documentation = comment
                        };
                        doc.Symbols.Add(varSymbol);
                        if (isExported) doc.ExportedSymbols.Add(varSymbol);

                        // 推断变量类型
                        if (varStmt.Initializer is CallExpr callExpr && callExpr.Callee is IdentifierExpr idExpr)
                        {
                            // var p = Point() -> p 的类型是 Point（如果 Point 是类）
                            if (classNames.Contains(idExpr.Name))
                            {
                                doc.VariableTypes[varStmt.Name] = idExpr.Name;
                            }
                        }
                        break;

                    case FuncDecl funcStmt:
                        isExported = funcStmt.IsExport || funcStmt.IsGlobal;
                        var funcSymbol = new DocumentSymbol
                        {
                            Name = funcStmt.Name,
                            Kind = SymbolKind.Function,
                            Detail = $"func {funcStmt.Name}({string.Join(", ", funcStmt.Parameters)})",
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, funcStmt.Name.Length),
                            Documentation = comment
                        };
                        doc.Symbols.Add(funcSymbol);
                        if (isExported) doc.ExportedSymbols.Add(funcSymbol);
                        break;

                    case ClassDecl classStmt:
                        isExported = classStmt.IsExport || classStmt.IsGlobal;
                        var classSymbol = new DocumentSymbol
                        {
                            Name = classStmt.Name,
                            Kind = SymbolKind.Class,
                            Detail = !string.IsNullOrEmpty(classStmt.SuperClass)
                                ? $"class {classStmt.Name} : {classStmt.SuperClass}"
                                : $"class {classStmt.Name}",
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, classStmt.Name.Length),
                            Documentation = comment,
                            Children = new List<DocumentSymbol>()
                        };

                        // 提取显式声明的类字段
                        var fieldNames = new HashSet<string>();
                        foreach (var field in classStmt.Fields)
                        {
                            fieldNames.Add(field.Name);
                            var fieldComment = GetPrecedingComment(doc.Lines, field.Line - 1);
                            classSymbol.Children.Add(new DocumentSymbol
                            {
                                Name = field.Name,
                                Kind = SymbolKind.Field,
                                Detail = field.Name,
                                Range = new Range(field.Line - 1, 0, field.Line - 1, 100),
                                SelectionRange = new Range(field.Line - 1, 0, field.Line - 1, field.Name.Length),
                                Documentation = fieldComment
                            });
                        }

                        // 提取类方法，并分析构造函数中的 this.xxx = xxx 赋值
                        foreach (var method in classStmt.Methods)
                        {
                            var methodComment = GetPrecedingComment(doc.Lines, method.Line - 1);
                            classSymbol.Children.Add(new DocumentSymbol
                            {
                                Name = method.Name,
                                Kind = SymbolKind.Method,
                                Detail = $"{method.Name}({string.Join(", ", method.Parameters)})",
                                Range = new Range(method.Line - 1, 0, method.Line - 1, 100),
                                SelectionRange = new Range(method.Line - 1, 0, method.Line - 1, method.Name.Length),
                                Documentation = methodComment
                            });

                            // 如果是构造函数，分析 this.xxx = xxx 赋值
                            if (method.Name == classStmt.Name)
                            {
                                ExtractThisFields(method.Body, classSymbol, fieldNames);
                            }
                        }

                        doc.Symbols.Add(classSymbol);
                        if (isExported) doc.ExportedSymbols.Add(classSymbol);
                        break;

                    case EnumDecl enumStmt:
                        isExported = enumStmt.IsGlobal;
                        var enumSymbol = new DocumentSymbol
                        {
                            Name = enumStmt.Name,
                            Kind = SymbolKind.Enum,
                            Range = new Range(stmt.Line - 1, 0, stmt.Line - 1, 100),
                            SelectionRange = new Range(stmt.Line - 1, 0, stmt.Line - 1, enumStmt.Name.Length),
                            Documentation = comment,
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

                        doc.Symbols.Add(enumSymbol);
                        if (isExported) doc.ExportedSymbols.Add(enumSymbol);
                        break;
                }
            }
        }

        /// <summary>
        /// 获取指定行之前的注释
        /// </summary>
        private string GetPrecedingComment(string[] lines, int lineIndex)
        {
            if (lineIndex <= 0 || lines == null || lineIndex > lines.Length) return null;

            var comments = new List<string>();
            var idx = lineIndex - 1;

            // 向上查找连续的注释行
            while (idx >= 0)
            {
                var line = lines[idx].Trim();
                if (line.StartsWith("//"))
                {
                    comments.Insert(0, line.Substring(2).Trim());
                    idx--;
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    idx--;
                }
                else
                {
                    break;
                }
            }

            return comments.Count > 0 ? string.Join("\n", comments) : null;
        }

        /// <summary>
        /// 从构造函数中提取 this.xxx = xxx 赋值的字段
        /// </summary>
        private void ExtractThisFields(List<Stmt> body, DocumentSymbol classSymbol, HashSet<string> existingFields)
        {
            if (body == null) return;

            foreach (var stmt in body)
            {
                // 处理 this.xxx = xxx 赋值
                if (stmt is ExpressionStmt exprStmt)
                {
                    ExtractThisFieldFromExpr(exprStmt.Expression, classSymbol, existingFields);
                }
                else if (stmt is IfStmt ifStmt)
                {
                    ExtractThisFields(ifStmt.ThenBranch, classSymbol, existingFields);
                    ExtractThisFields(ifStmt.ElseBranch, classSymbol, existingFields);
                }
            }
        }

        private void ExtractThisFieldFromExpr(Expr expr, DocumentSymbol classSymbol, HashSet<string> existingFields)
        {
            // this.xxx = value
            if (expr is AssignExpr assignExpr && assignExpr.Target is GetExpr getExpr && getExpr.Object is ThisExpr)
            {
                var fieldName = getExpr.Name;
                if (!existingFields.Contains(fieldName))
                {
                    existingFields.Add(fieldName);
                    classSymbol.Children.Add(new DocumentSymbol
                    {
                        Name = fieldName,
                        Kind = SymbolKind.Field,
                        Detail = fieldName,
                        Range = new Range(expr.Line - 1, 0, expr.Line - 1, 100),
                        SelectionRange = new Range(expr.Line - 1, 0, expr.Line - 1, fieldName.Length)
                    });
                }
            }
            // this.xxx = value (SetExpr)
            else if (expr is SetExpr setExpr && setExpr.Object is ThisExpr)
            {
                var fieldName = setExpr.Name;
                if (!existingFields.Contains(fieldName))
                {
                    existingFields.Add(fieldName);
                    classSymbol.Children.Add(new DocumentSymbol
                    {
                        Name = fieldName,
                        Kind = SymbolKind.Field,
                        Detail = fieldName,
                        Range = new Range(expr.Line - 1, 0, expr.Line - 1, 100),
                        SelectionRange = new Range(expr.Line - 1, 0, expr.Line - 1, fieldName.Length)
                    });
                }
            }
        }

        /// <summary>
        /// 添加类成员到补全列表（包括继承链）
        /// </summary>
        private void AddClassMembers(List<DocumentSymbol> symbols, string className, List<CompletionItem> items)
        {
            var visitedClasses = new HashSet<string>();
            var addedMembers = new HashSet<string>();
            AddClassMembersRecursive(symbols, className, items, visitedClasses, addedMembers);
        }

        private void AddClassMembersRecursive(List<DocumentSymbol> symbols, string className, List<CompletionItem> items, HashSet<string> visitedClasses, HashSet<string> addedMembers)
        {
            if (string.IsNullOrEmpty(className) || visitedClasses.Contains(className)) return;
            visitedClasses.Add(className);

            var classSymbol = FindSymbolByName(symbols, className);
            if (classSymbol == null || classSymbol.Kind != SymbolKind.Class) return;

            // 添加当前类的成员（跳过已添加的）
            if (classSymbol.Children != null)
            {
                foreach (var m in classSymbol.Children)
                {
                    if (addedMembers.Contains(m.Name)) continue;
                    addedMembers.Add(m.Name);
                    items.Add(new CompletionItem
                    {
                        Label = m.Name,
                        Kind = SymbolKindToCompletionKind(m.Kind),
                        Detail = m.Detail,
                        Documentation = m.Documentation
                    });
                }
            }

            // 递归添加父类成员
            if (classSymbol.Detail != null && classSymbol.Detail.Contains(":"))
            {
                var parts = classSymbol.Detail.Split(':');
                if (parts.Length > 1)
                {
                    var superClass = parts[1].Trim();
                    AddClassMembersRecursive(symbols, superClass, items, visitedClasses, addedMembers);
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
            if (string.IsNullOrEmpty(objName)) return items;

            UnityEngine.Debug.Log($"[LSP] GetMemberCompletions: objName={objName}, VariableTypes={doc.VariableTypes.Count}, Symbols={doc.Symbols.Count}, ImportedModules={string.Join(",", doc.ImportedModules.Keys)}");

            // 1. 检查内置对象
            if (BuiltinObjects.TryGetValue(objName, out var members))
            {
                items.AddRange(members.Select(m => new CompletionItem
                {
                    Label = m.Key,
                    Kind = CompletionItemKind.Method,
                    Detail = m.Value
                }));
                return items;
            }

            // 2. 检查是否是导入的模块
            if (doc.ImportedModules.TryGetValue(objName, out var modulePath))
            {
                // 查找模块文档（点号转目录分隔符）
                var normalizedPath = modulePath.Replace(".", "/");
                var moduleDoc = _documents.Values.FirstOrDefault(d =>
                    d.Uri.EndsWith(normalizedPath + ".panda", StringComparison.OrdinalIgnoreCase) ||
                    d.Uri.EndsWith(normalizedPath.Replace("/", "\\") + ".panda", StringComparison.OrdinalIgnoreCase));
                if (moduleDoc != null)
                {
                    // 只显示导出的符号（严格模式）
                    items.AddRange(moduleDoc.ExportedSymbols.Select(s => new CompletionItem
                    {
                        Label = s.Name,
                        Kind = SymbolKindToCompletionKind(s.Kind),
                        Detail = s.Detail,
                        Documentation = s.Documentation
                    }));
                }
                return items;
            }

            // 3. 检查变量类型（如果是类实例）
            if (doc.VariableTypes.TryGetValue(objName, out var typeName))
            {
                UnityEngine.Debug.Log($"[LSP] Found variable type: {objName} -> {typeName}");
                AddClassMembers(doc.Symbols, typeName, items);
                if (items.Count > 0) return items;
            }
            else
            {
                UnityEngine.Debug.Log($"[LSP] Variable type not found for: {objName}. Known types: {string.Join(", ", doc.VariableTypes.Keys)}");
            }

            // 4. 检查是否直接是类名（静态成员或类实例）
            var classSymbol = FindSymbolByName(doc.Symbols, objName);
            if (classSymbol != null && classSymbol.Kind == SymbolKind.Class)
            {
                UnityEngine.Debug.Log($"[LSP] Found class symbol: {objName}");
                AddClassMembers(doc.Symbols, objName, items);
                if (items.Count > 0) return items;
            }

            // 5. 检查是否是枚举
            var enumSymbol = FindSymbolByName(doc.Symbols, objName);
            if (enumSymbol != null && enumSymbol.Kind == SymbolKind.Enum && enumSymbol.Children != null)
            {
                items.AddRange(enumSymbol.Children.Select(m => new CompletionItem
                {
                    Label = m.Name,
                    Kind = CompletionItemKind.EnumMember,
                    Detail = $"{objName}.{m.Name}"
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
                SymbolKind.Field => CompletionItemKind.Field,
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
            public List<DocumentSymbol> ExportedSymbols { get; } = new List<DocumentSymbol>();
            public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
            public Dictionary<string, string> VariableTypes { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> ImportedModules { get; } = new Dictionary<string, string>();
        }
    }
}




