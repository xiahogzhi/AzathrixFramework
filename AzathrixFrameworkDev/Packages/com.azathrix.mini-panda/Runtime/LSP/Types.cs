using System.Collections.Generic;

namespace Azathrix.MiniPanda.LSP
{
    /// <summary>
    /// LSP 位置
    /// </summary>
    public struct Position
    {
        public int Line { get; set; }
        public int Character { get; set; }

        public Position(int line, int character)
        {
            Line = line;
            Character = character;
        }
    }

    /// <summary>
    /// LSP 范围
    /// </summary>
    public struct Range
    {
        public Position Start { get; set; }
        public Position End { get; set; }

        public Range(Position start, Position end)
        {
            Start = start;
            End = end;
        }

        public Range(int startLine, int startChar, int endLine, int endChar)
        {
            Start = new Position(startLine, startChar);
            End = new Position(endLine, endChar);
        }
    }

    /// <summary>
    /// LSP 位置（带文件）
    /// </summary>
    public struct Location
    {
        public string Uri { get; set; }
        public Range Range { get; set; }
    }

    /// <summary>
    /// 补全项类型
    /// </summary>
    public enum CompletionItemKind
    {
        Text = 1,
        Method = 2,
        Function = 3,
        Constructor = 4,
        Field = 5,
        Variable = 6,
        Class = 7,
        Interface = 8,
        Module = 9,
        Property = 10,
        Unit = 11,
        Value = 12,
        Enum = 13,
        Keyword = 14,
        Snippet = 15,
        Color = 16,
        File = 17,
        Reference = 18,
        Folder = 19,
        EnumMember = 20,
        Constant = 21,
        Struct = 22,
        Event = 23,
        Operator = 24,
        TypeParameter = 25
    }

    /// <summary>
    /// 补全项
    /// </summary>
    public class CompletionItem
    {
        public string Label { get; set; }
        public CompletionItemKind Kind { get; set; }
        public string Detail { get; set; }
        public string Documentation { get; set; }
        public string InsertText { get; set; }
        public string FilterText { get; set; }
        public string SortText { get; set; }
    }

    /// <summary>
    /// 悬停信息
    /// </summary>
    public class HoverInfo
    {
        public string Contents { get; set; }
        public Range? Range { get; set; }
    }

    /// <summary>
    /// 诊断严重性
    /// </summary>
    public enum DiagnosticSeverity
    {
        Error = 1,
        Warning = 2,
        Information = 3,
        Hint = 4
    }

    /// <summary>
    /// 诊断信息
    /// </summary>
    public class Diagnostic
    {
        public Range Range { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Code { get; set; }
        public string Source { get; set; } = "minipanda";
        public string Message { get; set; }
    }

    /// <summary>
    /// 符号类型
    /// </summary>
    public enum SymbolKind
    {
        File = 1,
        Module = 2,
        Namespace = 3,
        Package = 4,
        Class = 5,
        Method = 6,
        Property = 7,
        Field = 8,
        Constructor = 9,
        Enum = 10,
        Interface = 11,
        Function = 12,
        Variable = 13,
        Constant = 14,
        String = 15,
        Number = 16,
        Boolean = 17,
        Array = 18,
        Object = 19,
        Key = 20,
        Null = 21,
        EnumMember = 22,
        Struct = 23,
        Event = 24,
        Operator = 25,
        TypeParameter = 26
    }

    /// <summary>
    /// 文档符号
    /// </summary>
    public class DocumentSymbol
    {
        public string Name { get; set; }
        public string Detail { get; set; }
        public string Documentation { get; set; }
        public SymbolKind Kind { get; set; }
        public Range Range { get; set; }
        public Range SelectionRange { get; set; }
        public List<DocumentSymbol> Children { get; set; }
    }

    /// <summary>
    /// 签名帮助
    /// </summary>
    public class SignatureHelp
    {
        public List<SignatureInformation> Signatures { get; set; } = new List<SignatureInformation>();
        public int ActiveSignature { get; set; }
        public int ActiveParameter { get; set; }
    }

    /// <summary>
    /// 签名信息
    /// </summary>
    public class SignatureInformation
    {
        public string Label { get; set; }
        public string Documentation { get; set; }
        public List<ParameterInformation> Parameters { get; set; } = new List<ParameterInformation>();
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public class ParameterInformation
    {
        public string Label { get; set; }
        public string Documentation { get; set; }
    }

    /// <summary>
    /// 文本编辑
    /// </summary>
    public class TextEdit
    {
        public Range Range { get; set; }
        public string NewText { get; set; }
    }

    /// <summary>
    /// 工作区编辑
    /// </summary>
    public class WorkspaceEdit
    {
        public Dictionary<string, List<TextEdit>> Changes { get; set; } = new Dictionary<string, List<TextEdit>>();
    }
}
