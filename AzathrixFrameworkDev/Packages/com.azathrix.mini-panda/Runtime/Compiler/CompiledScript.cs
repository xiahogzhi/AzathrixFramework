using System.IO;

namespace Azathrix.MiniPanda.Compiler
{
    public class CompiledScript
    {
        public Bytecode Bytecode { get; }
        public string SourceHash { get; }

        public CompiledScript(Bytecode bytecode, string sourceHash = null)
        {
            Bytecode = bytecode;
            SourceHash = sourceHash;
        }

        public void SaveToFile(string path)
        {
            var data = Bytecode.Serialize();
            File.WriteAllBytes(path, data);
        }

        public static CompiledScript LoadFromFile(string path)
        {
            var data = File.ReadAllBytes(path);
            var bytecode = Bytecode.Deserialize(data);
            return new CompiledScript(bytecode);
        }
    }
}
