using System.Collections.Generic;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.GC;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Core
{
    public class MiniPandaClass : MiniPandaHeapObject
    {
        public string Name { get; }
        public MiniPandaClass SuperClass { get; set; }
        public Dictionary<string, MiniPandaFunction> Methods { get; } = new Dictionary<string, MiniPandaFunction>();

        public MiniPandaClass(string name)
        {
            Name = name;
        }

        public MiniPandaFunction FindMethod(string name)
        {
            if (Methods.TryGetValue(name, out var method))
                return method;
            return SuperClass?.FindMethod(name);
        }

        public override string ToString() => $"<class {Name}>";
    }

    public class MiniPandaInstance : MiniPandaHeapObject
    {
        public MiniPandaClass Class { get; }
        public Dictionary<string, Value> Fields { get; } = new Dictionary<string, Value>();

        public MiniPandaInstance(MiniPandaClass klass)
        {
            Class = klass;
        }

        public Value Get(string name)
        {
            if (Fields.TryGetValue(name, out var value))
                return value;

            var method = Class.FindMethod(name);
            if (method != null)
                return Value.FromObject(method.Bind(this));

            return Value.Null;
        }

        public void Set(string name, Value value)
        {
            Fields[name] = value;
        }

        public override string ToString() => $"<{Class.Name} instance>";
    }

    public class MiniPandaFunction : MiniPandaHeapObject, ICallable
    {
        public FunctionPrototype Prototype { get; }
        public Environment Closure { get; }
        public Upvalue[] Upvalues { get; }
        public MiniPandaInstance BoundInstance { get; private set; }
        public bool IsInitializer { get; set; }

        public int Arity => Prototype.Arity;

        public MiniPandaFunction(FunctionPrototype prototype, Environment closure, Upvalue[] upvalues = null)
        {
            Prototype = prototype;
            Closure = closure;
            Upvalues = upvalues ?? new Upvalue[prototype.UpvalueCount];
        }

        public MiniPandaFunction Bind(MiniPandaInstance instance)
        {
            return new MiniPandaFunction(Prototype, Closure, Upvalues)
            {
                BoundInstance = instance,
                IsInitializer = IsInitializer
            };
        }

        public Value Call(VM.VirtualMachine vm, Value[] args)
        {
            return vm.CallFunction(this, args);
        }

        public override string ToString() => $"<func {Prototype.Name}>";
    }

    public class MiniPandaBoundMethod : MiniPandaHeapObject, ICallable
    {
        public MiniPandaInstance Instance { get; }
        public MiniPandaFunction Method { get; }

        public int Arity => Method.Arity;

        public MiniPandaBoundMethod(MiniPandaInstance instance, MiniPandaFunction method)
        {
            Instance = instance;
            Method = method;
        }

        public Value Call(VM.VirtualMachine vm, Value[] args)
        {
            return vm.CallMethod(Instance, Method, args);
        }

        public override string ToString() => $"<bound method {Method.Prototype.Name}>";
    }
}
