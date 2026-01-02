using System.Collections.Generic;

namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// MiniPanda 模块对象
    /// </summary>
    public class MiniPandaModule : MiniPandaHeapObject
    {
        public string Path { get; }
        public Environment Env { get; }
        /// <summary>导出的符号列表（null 表示导出所有）</summary>
        public HashSet<string> Exports { get; }

        public MiniPandaModule(string path, Environment env, HashSet<string> exports = null)
        {
            Path = path;
            Env = env;
            Exports = exports;
        }

        /// <summary>
        /// 获取模块成员（受导出列表限制）
        /// </summary>
        public Value GetMember(string name)
        {
            // 如果有导出列表，检查是否导出
            if (Exports != null && Exports.Count > 0 && !Exports.Contains(name))
            {
                return Value.Null; // 未导出的成员不可访问
            }

            // Prefer local module values so explicit nulls are preserved.
            if (Env.ContainsLocal(name)) return Env.GetLocal(name);
            return Env.Get(name);
        }

        /// <summary>
        /// 设置模块成员
        /// </summary>
        public void SetMember(string name, Value value)
        {
            Env.Set(name, value);
        }

        public override string ToString() => $"<module '{Path}'>";
    }
}
