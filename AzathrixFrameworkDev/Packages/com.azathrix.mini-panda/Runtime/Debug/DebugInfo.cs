using System.Collections.Generic;

namespace Azathrix.MiniPanda.Debug
{
    /// <summary>
    /// 源码位置信息
    /// </summary>
    public struct SourceLocation
    {
        public string File;
        public int Line;
        public int Column;

        public SourceLocation(string file, int line, int column = 0)
        {
            File = file;
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// 断点信息
    /// </summary>
    public class Breakpoint
    {
        public int Id { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public bool Enabled { get; set; } = true;
        public string Condition { get; set; }
        public int HitCount { get; set; }
    }

    /// <summary>
    /// 调用栈帧信息（用于调试显示）
    /// </summary>
    public class StackFrameInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    /// <summary>
    /// 变量信息（用于调试显示）
    /// </summary>
    public class VariableInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public int VariablesReference { get; set; }
    }

    /// <summary>
    /// 作用域信息
    /// </summary>
    public class ScopeInfo
    {
        public string Name { get; set; }
        public int VariablesReference { get; set; }
        public bool Expensive { get; set; }
    }

    /// <summary>
    /// 调试信息，存储字节码到源码的映射
    /// </summary>
    public class DebugInfo
    {
        /// <summary>源文件路径</summary>
        public string SourceFile { get; set; }

        /// <summary>字节码偏移到行号的映射</summary>
        public Dictionary<int, int> OffsetToLine { get; } = new Dictionary<int, int>();

        /// <summary>行号到字节码偏移的映射</summary>
        public Dictionary<int, List<int>> LineToOffsets { get; } = new Dictionary<int, List<int>>();

        /// <summary>添加映射</summary>
        public void AddMapping(int offset, int line)
        {
            OffsetToLine[offset] = line;
            if (!LineToOffsets.TryGetValue(line, out var offsets))
            {
                offsets = new List<int>();
                LineToOffsets[line] = offsets;
            }
            offsets.Add(offset);
        }

        /// <summary>获取指定偏移的行号</summary>
        public int GetLine(int offset)
        {
            return OffsetToLine.TryGetValue(offset, out var line) ? line : -1;
        }

        /// <summary>获取指定行的第一个字节码偏移</summary>
        public int GetFirstOffset(int line)
        {
            if (LineToOffsets.TryGetValue(line, out var offsets) && offsets.Count > 0)
                return offsets[0];
            return -1;
        }
    }
}
