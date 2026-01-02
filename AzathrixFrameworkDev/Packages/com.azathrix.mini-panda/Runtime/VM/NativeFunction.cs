using System;
using Azathrix.MiniPanda.Core;

namespace Azathrix.MiniPanda.VM
{
    public class NativeFunction : MiniPandaHeapObject, ICallable
    {
        private readonly Func<Value[], Value> _function;
        private readonly Func<VirtualMachine, Value[], Value> _vmFunction;
        private readonly int _arity;

        public int Arity => _arity;

        public NativeFunction(Func<Value[], Value> function, int arity = -1)
        {
            _function = function;
            _arity = arity;
        }

        public NativeFunction(Func<VirtualMachine, Value[], Value> function, int arity = -1)
        {
            _vmFunction = function;
            _arity = arity;
        }

        public Value Call(VirtualMachine vm, Value[] args)
        {
            if (_vmFunction != null)
                return _vmFunction(vm, args);
            return _function(args);
        }

        public override string ToString() => "<native func>";

        // Factory methods
        public static NativeFunction Create(Func<Value[], Value> func, int arity = -1)
            => new NativeFunction(func, arity);

        public static NativeFunction Create(Action<Value[]> action, int arity = -1)
            => new NativeFunction(args => { action(args); return Value.Null; }, arity);

        public static NativeFunction Create(Func<VirtualMachine, Value[], Value> func, int arity = -1)
            => new NativeFunction(func, arity);

        public static NativeFunction Create(Func<Value> func)
            => new NativeFunction(_ => func(), 0);

        public static NativeFunction Create(Func<Value, Value> func)
            => new NativeFunction(args => func(args.Length > 0 ? args[0] : Value.Null), 1);

        public static NativeFunction Create(Func<Value, Value, Value> func)
            => new NativeFunction(args => func(
                args.Length > 0 ? args[0] : Value.Null,
                args.Length > 1 ? args[1] : Value.Null), 2);

        public static NativeFunction Create(Func<Value, Value, Value, Value> func)
            => new NativeFunction(args => func(
                args.Length > 0 ? args[0] : Value.Null,
                args.Length > 1 ? args[1] : Value.Null,
                args.Length > 2 ? args[2] : Value.Null), 3);

        public static NativeFunction CreateWithVM(Func<VirtualMachine, Value[], Value> func, int arity = -1)
            => new NativeFunction(func, arity);
    }
}
