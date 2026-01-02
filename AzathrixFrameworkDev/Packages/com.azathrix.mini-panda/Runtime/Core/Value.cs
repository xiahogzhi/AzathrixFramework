using System;
using Azathrix.MiniPanda.GC;

namespace Azathrix.MiniPanda.Core
{
    public enum ValueType : byte
    {
        Null,
        Bool,
        Number,
        Object  // All heap-allocated objects
    }

    /// <summary>
    /// </summary>
    public struct Value
    {
        public ValueType Type;
        public double Number;
        public bool Bool;
        private MiniPandaHeapObject _object;

        public static Value Null => new Value { Type = ValueType.Null };
        public static Value True => new Value { Type = ValueType.Bool, Bool = true };
        public static Value False => new Value { Type = ValueType.Bool, Bool = false };

        public static Value FromNumber(double n) => new Value { Type = ValueType.Number, Number = n };
        public static Value FromBool(bool b) => new Value { Type = ValueType.Bool, Bool = b };

        public static Value FromObject(MiniPandaHeapObject obj)
        {
            if (obj == null) return Null;
            return new Value { Type = ValueType.Object, _object = obj };
        }

        public MiniPandaHeapObject AsObject() => Type == ValueType.Object ? _object : null;

        public T As<T>() where T : MiniPandaHeapObject => AsObject() as T;

        public bool IsNull => Type == ValueType.Null;
        public bool IsBool => Type == ValueType.Bool;
        public bool IsNumber => Type == ValueType.Number;
        public bool IsObject => Type == ValueType.Object;

        public bool IsString => IsObject && _object is MiniPandaString;
        public bool IsArray => IsObject && _object is MiniPandaArray;
        public bool IsDict => IsObject && _object is MiniPandaObject;
        public bool IsFunction => IsObject && _object is ICallable;
        public bool IsClass => IsObject && _object is MiniPandaClass;
        public bool IsInstance => IsObject && _object is MiniPandaInstance;

        public double AsNumber() => Type == ValueType.Number ? Number : 0;
        public bool AsBool()
        {
            return Type switch
            {
                ValueType.Null => false,
                ValueType.Bool => Bool,
                ValueType.Number => Number != 0,
                ValueType.Object => _object != null,
                _ => false
            };
        }

        public string AsString()
        {
            return Type switch
            {
                ValueType.Null => "null",
                ValueType.Bool => Bool ? "true" : "false",
                ValueType.Number => Number.ToString(),
                ValueType.Object => _object?.ToString() ?? "null",
                _ => "unknown"
            };
        }

        public ICallable AsCallable() => AsObject() as ICallable;

        /// <summary>
        /// Convert Value to C# type T.
        /// Supports: primitives, string, delegates (Func/Action), MiniPanda types.
        /// </summary>
        public T To<T>() => (T)ToType(typeof(T));
        public T To<T>(VM.VirtualMachine vm) => (T)ToType(typeof(T), vm);

        public object ToType(Type targetType) => ToType(targetType, null);

        public object ToType(Type targetType, VM.VirtualMachine vm)
        {
            // Null
            if (Type == ValueType.Null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Value itself
            if (targetType == typeof(Value)) return this;

            // Bool
            if (targetType == typeof(bool)) return AsBool();

            // Numeric types
            if (targetType == typeof(double)) return AsNumber();
            if (targetType == typeof(float)) return (float)AsNumber();
            if (targetType == typeof(int)) return (int)AsNumber();
            if (targetType == typeof(long)) return (long)AsNumber();

            // String
            if (targetType == typeof(string)) return Type == ValueType.Object && _object is MiniPandaString s ? s.Value : AsString();

            // MiniPanda heap objects
            if (typeof(MiniPandaHeapObject).IsAssignableFrom(targetType)) return AsObject();

            // Delegate conversion
            if (typeof(Delegate).IsAssignableFrom(targetType) && AsCallable() is { } callable)
            {
                return CreateDelegate(targetType, callable, vm);
            }

            // Object - return raw
            if (targetType == typeof(object))
            {
                return Type switch
                {
                    ValueType.Null => null,
                    ValueType.Bool => Bool,
                    ValueType.Number => Number,
                    ValueType.Object => _object,
                    _ => null
                };
            }

            throw new InvalidCastException($"Cannot convert Value to {targetType.Name}");
        }

        private static object CreateDelegate(Type delegateType, ICallable callable, VM.VirtualMachine vm)
        {
            var invoke = delegateType.GetMethod("Invoke");
            var parameters = invoke.GetParameters();
            var returnType = invoke.ReturnType;

            // Create a wrapper that calls the MiniPanda function
            if (returnType == typeof(void))
            {
                // Action variants
                return parameters.Length switch
                {
                    0 => new Action(() => callable.Call(vm, Array.Empty<Value>())),
                    1 => CreateAction1(callable, vm),
                    2 => CreateAction2(callable, vm),
                    _ => throw new NotSupportedException($"Delegate with {parameters.Length} parameters not supported")
                };
            }
            else
            {
                // Func variants
                return parameters.Length switch
                {
                    0 => CreateFunc0(callable, returnType, vm),
                    1 => CreateFunc1(callable, returnType, vm),
                    2 => CreateFunc2(callable, returnType, vm),
                    _ => throw new NotSupportedException($"Delegate with {parameters.Length} parameters not supported")
                };
            }
        }

        private static Delegate CreateAction1(ICallable c, VM.VirtualMachine vm)
        {
            return new Action<object>(a => c.Call(vm, new[] { ConvertArg(a) }));
        }

        private static Delegate CreateAction2(ICallable c, VM.VirtualMachine vm)
        {
            return new Action<object, object>((a, b) => c.Call(vm, new[] { ConvertArg(a), ConvertArg(b) }));
        }

        private static Delegate CreateFunc0(ICallable c, Type ret, VM.VirtualMachine vm)
        {
            return new Func<object>(() => c.Call(vm, Array.Empty<Value>()).ToType(ret, vm));
        }

        private static Delegate CreateFunc1(ICallable c, Type ret, VM.VirtualMachine vm)
        {
            return new Func<object, object>(a => c.Call(vm, new[] { ConvertArg(a) }).ToType(ret, vm));
        }

        private static Delegate CreateFunc2(ICallable c, Type ret, VM.VirtualMachine vm)
        {
            return new Func<object, object, object>((a, b) => c.Call(vm, new[] { ConvertArg(a), ConvertArg(b) }).ToType(ret, vm));
        }

        private static Value ConvertArg(object arg)
        {
            return arg switch
            {
                null => Null,
                bool b => FromBool(b),
                int i => FromNumber(i),
                long l => FromNumber(l),
                float f => FromNumber(f),
                double d => FromNumber(d),
                string s => FromObject(new MiniPandaString(s)),
                Value v => v,
                _ => FromObject(new MiniPandaString(arg.ToString()))
            };
        }

        public override string ToString() => AsString();

        public override bool Equals(object obj)
        {
            if (obj is Value other)
            {
                if (Type != other.Type) return false;
                return Type switch
                {
                    ValueType.Null => true,
                    ValueType.Bool => Bool == other.Bool,
                    ValueType.Number => Math.Abs(Number - other.Number) < double.Epsilon,
                    ValueType.Object => ReferenceEquals(_object, other._object),
                    _ => false
                };
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Type switch
            {
                ValueType.Null => 0,
                ValueType.Bool => Bool.GetHashCode(),
                ValueType.Number => Number.GetHashCode(),
                ValueType.Object => _object?.GetHashCode() ?? 0,
                _ => 0
            };
        }

        public static bool operator ==(Value a, Value b) => a.Equals(b);
        public static bool operator !=(Value a, Value b) => !a.Equals(b);

        public static implicit operator Value(double n) => FromNumber(n);
        public static implicit operator Value(bool b) => FromBool(b);
        public static implicit operator Value(string s) => s == null ? Null : FromObject(new MiniPandaString(s));
    }

    public interface ICallable
    {
        int Arity { get; }
        Value Call(VM.VirtualMachine vm, Value[] args);
    }
}
