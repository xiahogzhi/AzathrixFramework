using System;
using System.Collections.Generic;
using System.IO;

namespace Azathrix.MiniPanda.Compiler
{
    public class Bytecode
    {
        public List<byte> Code { get; } = new List<byte>();
        public List<object> Constants { get; } = new List<object>();
        public List<int> Lines { get; } = new List<int>();
        public string SourceFile { get; set; }
        /// <summary>导出的符号名称列表</summary>
        public HashSet<string> Exports { get; } = new HashSet<string>();

        // Deduplicate strings and numbers to reduce constant table size.
        private readonly Dictionary<string, int> _stringPool = new Dictionary<string, int>();
        private readonly Dictionary<double, int> _numberPool = new Dictionary<double, int>();

        public int AddConstant(object value)
        {
            if (value is string s)
            {
                if (_stringPool.TryGetValue(s, out var idx)) return idx;
                var index = Constants.Count;
                Constants.Add(s);
                _stringPool[s] = index;
                return index;
            }

            if (value is double d)
            {
                if (_numberPool.TryGetValue(d, out var idx)) return idx;
                var index = Constants.Count;
                Constants.Add(d);
                _numberPool[d] = index;
                return index;
            }

            Constants.Add(value);
            return Constants.Count - 1;
        }

        public void Emit(Opcode op, int line)
        {
            Code.Add((byte)op);
            Lines.Add(line);
        }

        public void EmitByte(byte b, int line)
        {
            Code.Add(b);
            Lines.Add(line);
        }

        public void EmitShort(ushort value, int line)
        {
            Code.Add((byte)(value >> 8));
            Code.Add((byte)(value & 0xFF));
            Lines.Add(line);
            Lines.Add(line);
        }

        public int EmitJump(Opcode op, int line)
        {
            Emit(op, line);
            EmitShort(0xFFFF, line);
            return Code.Count - 2;
        }

        public void PatchJump(int offset)
        {
            int jump = Code.Count - offset - 2;
            if (jump > ushort.MaxValue)
                throw new InvalidOperationException("Jump too large");
            Code[offset] = (byte)(jump >> 8);
            Code[offset + 1] = (byte)(jump & 0xFF);
        }

        public void EmitLoop(int loopStart, int line)
        {
            Emit(Opcode.Loop, line);
            int offset = Code.Count - loopStart + 2;
            if (offset > ushort.MaxValue)
                throw new InvalidOperationException("Loop body too large");
            EmitShort((ushort)offset, line);
        }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Magic number
            writer.Write(new[] { (byte)'M', (byte)'P', (byte)'B', (byte)'C' });
            // Version
            writer.Write((byte)3);

            // Constants
            writer.Write(Constants.Count);
            foreach (var constant in Constants)
            {
                WriteConstant(writer, constant);
            }

            // Code
            writer.Write(Code.Count);
            writer.Write(Code.ToArray());

            // Lines
            writer.Write(Lines.Count);
            foreach (var line in Lines)
            {
                writer.Write((ushort)line);
            }

            return ms.ToArray();
        }

        public static Bytecode Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            // Magic number
            var magic = reader.ReadBytes(4);
            if (magic[0] != 'M' || magic[1] != 'P' || magic[2] != 'B' || magic[3] != 'C')
                throw new InvalidDataException("Invalid bytecode file");

            // Version
            var version = reader.ReadByte();
            if (version < 1 || version > 3)
                throw new InvalidDataException($"Unsupported bytecode version: {version}");

            var bytecode = new Bytecode();

            // Constants
            var constantCount = reader.ReadInt32();
            for (int i = 0; i < constantCount; i++)
            {
                bytecode.Constants.Add(ReadConstant(reader, version));
            }

            // Code
            var codeLength = reader.ReadInt32();
            bytecode.Code.AddRange(reader.ReadBytes(codeLength));

            // Lines
            var lineCount = reader.ReadInt32();
            for (int i = 0; i < lineCount; i++)
            {
                bytecode.Lines.Add(reader.ReadUInt16());
            }

            return bytecode;
        }

        private static void WriteConstant(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    writer.Write((byte)0);
                    break;
                case double d:
                    writer.Write((byte)1);
                    writer.Write(d);
                    break;
                case string s:
                    writer.Write((byte)2);
                    writer.Write(s);
                    break;
                case bool b:
                    writer.Write((byte)3);
                    writer.Write(b);
                    break;
                case FunctionPrototype fp:
                    writer.Write((byte)4);
                    writer.Write(fp.Name ?? "");
                    writer.Write(fp.ClassName ?? "");
                    writer.Write(fp.Arity);
                    writer.Write(fp.UpvalueCount);
                    var bytes = fp.Code.Serialize();
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot serialize constant of type {value.GetType()}");
            }
        }

        private static object ReadConstant(BinaryReader reader, int version)
        {
            var type = reader.ReadByte();
            return type switch
            {
                0 => null,
                1 => reader.ReadDouble(),
                2 => reader.ReadString(),
                3 => reader.ReadBoolean(),
                4 => version == 1
                    ? new FunctionPrototype
                    {
                        Name = reader.ReadString(),
                        Arity = reader.ReadInt32(),
                        UpvalueCount = 0,
                        Code = Bytecode.Deserialize(reader.ReadBytes(reader.ReadInt32()))
                    }
                    : new FunctionPrototype
                    {
                        Name = reader.ReadString(),
                        ClassName = reader.ReadString(),
                        Arity = reader.ReadInt32(),
                        UpvalueCount = reader.ReadInt32(),
                        Code = Bytecode.Deserialize(reader.ReadBytes(reader.ReadInt32()))
                    },
                _ => throw new InvalidDataException($"Unknown constant type: {type}")
            };
        }
    }

    public class FunctionPrototype
    {
        public string Name { get; set; }
        public string ClassName { get; set; }
        public int Arity { get; set; }
        public string RestParam { get; set; }  // Rest parameter name (null if none)
        public Bytecode Code { get; set; }
        public int UpvalueCount { get; set; }

        public string FullName => string.IsNullOrEmpty(ClassName) ? Name : $"{ClassName}.{Name}";
    }

    public class ClassPrototype
    {
        public string Name { get; set; }
        public Dictionary<string, FunctionPrototype> Methods { get; } = new Dictionary<string, FunctionPrototype>();
    }
}
