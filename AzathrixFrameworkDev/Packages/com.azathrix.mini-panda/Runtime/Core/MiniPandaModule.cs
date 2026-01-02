using Azathrix.MiniPanda.GC;

namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// </summary>
    public class MiniPandaModule : MiniPandaHeapObject
    {
        public string Path { get; }
        public Environment Env { get; }

        public MiniPandaModule(string path, Environment env)
        {
            Path = path;
            Env = env;
        }

        /// <summary>
        /// </summary>
        public Value GetMember(string name)
        {
            // Prefer local module values so explicit nulls are preserved.
            if (Env.ContainsLocal(name)) return Env.GetLocal(name);
            return Env.Get(name);
        }

        /// <summary>
        /// </summary>
        public void SetMember(string name, Value value)
        {
            Env.Set(name, value);
        }

        public override string ToString() => $"<module '{Path}'>";
    }
}
