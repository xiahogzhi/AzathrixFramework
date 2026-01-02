using System.Collections.Generic;
using Azathrix.MiniPanda.Lexer;

namespace Azathrix.MiniPanda.Parser
{
    // Base classes
    public abstract class Stmt { public int Line; public int Column; }
    public abstract class Expr { public int Line; public int Column; }

    // Statements
    public class ExpressionStmt : Stmt
    {
        public Expr Expression;
    }

    public class VarDecl : Stmt
    {
        public string Name;
        public Expr Initializer;
        public bool IsGlobal;
        public bool IsStatic;  // 静态成员（类内部使用）
        public bool IsExport;  // 模块导出
    }

    public class FuncDecl : Stmt
    {
        public string Name;
        public List<string> Parameters;
        public List<Expr> Defaults;  // Default values for parameters (null if no default)
        public string RestParam;     // Rest parameter name (null if none)
        public List<Stmt> Body;
        public bool IsGlobal;
        public bool IsStatic;  // 静态方法（类内部使用）
        public bool IsExport;  // 模块导出
    }

    public class ClassDecl : Stmt
    {
        public string Name;
        public string SuperClass;
        public List<VarDecl> Fields;        // 实例字段
        public List<FuncDecl> Methods;      // 实例方法
        public List<VarDecl> StaticFields;  // 静态字段
        public List<FuncDecl> StaticMethods; // 静态方法
        public bool IsGlobal;
        public bool IsExport;  // 模块导出
    }

    public class IfStmt : Stmt
    {
        public Expr Condition;
        public List<Stmt> ThenBranch;
        public List<Stmt> ElseBranch;
    }

    public class WhileStmt : Stmt
    {
        public Expr Condition;
        public List<Stmt> Body;
    }

    public class ForStmt : Stmt
    {
        public string Variable;      // Value variable (or single variable)
        public string KeyVariable;   // Key variable for "for k, v in dict" (null if not used)
        public Expr Iterable;
        public List<Stmt> Body;
    }

    public class ReturnStmt : Stmt
    {
        public Expr Value;
    }

    public class BreakStmt : Stmt { }
    public class ContinueStmt : Stmt { }

    public class TryStmt : Stmt
    {
        public List<Stmt> TryBlock;
        public string CatchVar;        // Variable name for caught exception (null if no catch)
        public List<Stmt> CatchBlock;  // null if no catch
        public List<Stmt> FinallyBlock; // null if no finally
    }

    public class ThrowStmt : Stmt
    {
        public Expr Value;
    }

    public class ImportStmt : Stmt
    {
        public string Path;
        public string Alias;
        public bool IsGlobal;
    }

    public class BlockStmt : Stmt
    {
        public List<Stmt> Statements;
    }

    public class EnumDecl : Stmt
    {
        public string Name;
        public List<(string Name, Expr Value)> Members;
        public bool IsGlobal;
    }

    // Expressions
    public class LiteralExpr : Expr
    {
        public object Value;
    }

    public class StringExpr : Expr
    {
        public List<object> Parts; // string or StringInterpolation
    }

    public class IdentifierExpr : Expr
    {
        public string Name;
    }

    public class AssignExpr : Expr
    {
        public Expr Target;
        public Expr Value;
    }

    /// <summary>
    /// </summary>
    public class CompoundAssignExpr : Expr
    {
        public Expr Target;
        public TokenType Operator; // Plus, Minus, Star, Slash
        public Expr Value;
    }

    /// <summary>
    /// </summary>
    public class UpdateExpr : Expr
    {
        public Expr Target;
        public TokenType Operator; // PlusPlus, MinusMinus
        public bool IsPrefix;      // ++x vs x++
    }

    public class BinaryExpr : Expr
    {
        public Expr Left;
        public TokenType Operator;
        public Expr Right;
    }

    public class UnaryExpr : Expr
    {
        public TokenType Operator;
        public Expr Operand;
    }

    public class CallExpr : Expr
    {
        public Expr Callee;
        public List<Expr> Arguments;
    }

    public class GetExpr : Expr
    {
        public Expr Object;
        public string Name;
        public bool IsOptional;  // for ?.
    }

    public class SetExpr : Expr
    {
        public Expr Object;
        public string Name;
        public Expr Value;
    }

    public class IndexGetExpr : Expr
    {
        public Expr Object;
        public Expr Index;
        public bool IsOptional;  // for ?[
    }

    public class IndexSetExpr : Expr
    {
        public Expr Object;
        public Expr Index;
        public Expr Value;
    }

    public class ArrayExpr : Expr
    {
        public List<Expr> Elements;
    }

    public class ObjectExpr : Expr
    {
        public List<(string Key, Expr Value)> Properties;
    }

    public class LambdaExpr : Expr
    {
        public List<string> Parameters;
        public List<Expr> Defaults;  // Default values for parameters
        public string RestParam;     // Rest parameter name (null if none)
        public Expr Body;        // for single expression: (x) => x * 2
        public List<Stmt> Block; // for block: (x) => { return x * 2; }
    }

    public class ThisExpr : Expr { }
    public class SuperExpr : Expr
    {
        public string Method;
    }

    public class TernaryExpr : Expr
    {
        public Expr Condition;
        public Expr ThenExpr;
        public Expr ElseExpr;
    }

    public class NullCoalesceExpr : Expr
    {
        public Expr Left;
        public Expr Right;
    }
}
