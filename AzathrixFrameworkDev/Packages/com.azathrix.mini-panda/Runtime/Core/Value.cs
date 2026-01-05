using System;
using System.Linq;

namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// 值类型枚举
    /// </summary>
    public enum ValueType : byte
    {
        Null,    // 空值
        Bool,    // 布尔值
        Number,  // 数字（双精度浮点）
        Object   // 堆分配对象（字符串、数组、对象、函数等）
    }

    /// <summary>
    /// MiniPanda 统一值类型
    /// </summary>
    /// <remarks>
    /// 使用结构体实现，支持：
    /// - 基本类型：null、bool、number
    /// - 堆对象：string、array、object、function、class、instance
    /// - 类型转换：To&lt;T&gt;() 支持转换为 C# 类型
    /// - 委托转换：可将 MiniPanda 函数转换为 C# 委托
    /// </remarks>
    public struct Value
    {
        public ValueType Type;       // 值类型
        public double Number;        // 数字值（当 Type == Number 时有效）
        public bool Bool;            // 布尔值（当 Type == Bool 时有效）
        private MiniPandaHeapObject _object;  // 堆对象引用

        #region 静态常量

        /// <summary>空值常量</summary>
        public static Value Null => new Value { Type = ValueType.Null };
        /// <summary>真值常量</summary>
        public static Value True => new Value { Type = ValueType.Bool, Bool = true };
        /// <summary>假值常量</summary>
        public static Value False => new Value { Type = ValueType.Bool, Bool = false };

        #endregion

        #region 工厂方法

        /// <summary>从数字创建值</summary>
        public static Value FromNumber(double n) => new Value { Type = ValueType.Number, Number = n };
        /// <summary>从布尔创建值</summary>
        public static Value FromBool(bool b) => new Value { Type = ValueType.Bool, Bool = b };
        /// <summary>从堆对象创建值</summary>
        public static Value FromObject(MiniPandaHeapObject obj)
        {
            if (obj == null) return Null;
            return new Value { Type = ValueType.Object, _object = obj };
        }

        #endregion

        #region 类型访问

        /// <summary>获取堆对象</summary>
        public MiniPandaHeapObject AsObject() => Type == ValueType.Object ? _object : null;
        /// <summary>尝试转换为指定堆对象类型</summary>
        public T As<T>() where T : MiniPandaHeapObject => AsObject() as T;

        /// <summary>是否为空值</summary>
        public bool IsNull => Type == ValueType.Null;
        /// <summary>是否为布尔值</summary>
        public bool IsBool => Type == ValueType.Bool;
        /// <summary>是否为数字</summary>
        public bool IsNumber => Type == ValueType.Number;
        /// <summary>是否为堆对象</summary>
        public bool IsObject => Type == ValueType.Object;

        /// <summary>是否为字符串</summary>
        public bool IsString => IsObject && _object is MiniPandaString;
        /// <summary>是否为数组</summary>
        public bool IsArray => IsObject && _object is MiniPandaArray;
        /// <summary>是否为字典/对象</summary>
        public bool IsDict => IsObject && _object is MiniPandaObject;
        /// <summary>是否为可调用对象</summary>
        public bool IsFunction => IsObject && _object is ICallable;
        /// <summary>是否为类</summary>
        public bool IsClass => IsObject && _object is MiniPandaClass;
        /// <summary>是否为实例</summary>
        public bool IsInstance => IsObject && _object is MiniPandaInstance;

        #endregion

        #region 类型转换

        /// <summary>转换为数字</summary>
        public double AsNumber() => Type == ValueType.Number ? Number : 0;

        /// <summary>转换为布尔值（真值判断）</summary>
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

        /// <summary>转换为字符串</summary>
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

        /// <summary>获取可调用对象</summary>
        public ICallable AsCallable() => AsObject() as ICallable;

        /// <summary>
        /// 转换为 C# 类型
        /// </summary>
        /// <remarks>
        /// 支持：基本类型、字符串、委托（Func/Action）、MiniPanda 类型
        /// </remarks>
        public T To<T>() => (T)ToType(typeof(T));

        /// <summary>转换为 C# 类型（带 VM 上下文）</summary>
        public T To<T>(VM.VirtualMachine vm) => (T)ToType(typeof(T), vm);

        /// <summary>转换为指定类型</summary>
        public object ToType(Type targetType) => ToType(targetType, null);

        /// <summary>
        /// 转换为指定类型（核心实现）
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <param name="vm">虚拟机实例（委托转换时需要）</param>
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

        #endregion

        #region 委托转换

        /// <summary>
        /// 使用表达式树创建任意签名的 C# 委托包装 MiniPanda 函数
        /// </summary>
        private static object CreateDelegate(Type delegateType, ICallable callable, VM.VirtualMachine vm)
        {
            var invoke = delegateType.GetMethod("Invoke");
            var parameters = invoke.GetParameters();
            var returnType = invoke.ReturnType;

            // 创建参数表达式
            var paramExprs = new System.Linq.Expressions.ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                paramExprs[i] = System.Linq.Expressions.Expression.Parameter(parameters[i].ParameterType, $"p{i}");
            }

            // 捕获变量
            var callableConst = System.Linq.Expressions.Expression.Constant(callable);
            var vmConst = System.Linq.Expressions.Expression.Constant(vm);

            // 检查最后一个参数是否是 params 数组
            bool hasParams = parameters.Length > 0 &&
                parameters[parameters.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

            System.Linq.Expressions.Expression valueArrayExpr;
            if (hasParams && parameters.Length > 0)
            {
                // 有 params 参数：普通参数 + 展开的数组
                var lastParam = paramExprs[parameters.Length - 1];
                var normalParams = paramExprs.Take(parameters.Length - 1)
                    .Select(p => BuildConvertToValue(p)).ToList();

                // 调用辅助方法展开 params 数组
                var expandMethod = typeof(Value).GetMethod("ExpandParamsArray",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var expandedExpr = System.Linq.Expressions.Expression.Call(expandMethod, lastParam);

                // 合并普通参数和展开的数组
                var concatMethod = typeof(Value).GetMethod("ConcatValueArrays",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var normalArrayExpr = System.Linq.Expressions.Expression.NewArrayInit(typeof(Value), normalParams);
                valueArrayExpr = System.Linq.Expressions.Expression.Call(concatMethod, normalArrayExpr, expandedExpr);
            }
            else
            {
                // 无 params：直接构建数组
                valueArrayExpr = System.Linq.Expressions.Expression.NewArrayInit(
                    typeof(Value),
                    paramExprs.Select(p => BuildConvertToValue(p)).ToArray()
                );
            }

            // 调用 callable.Call(vm, args)
            var callMethod = typeof(ICallable).GetMethod("Call");
            var callExpr = System.Linq.Expressions.Expression.Call(
                callableConst, callMethod, vmConst, valueArrayExpr);

            System.Linq.Expressions.Expression body;
            if (returnType == typeof(void))
            {
                // Action: 直接调用，忽略返回值
                body = callExpr;
            }
            else
            {
                // Func: 调用 ToType 转换返回值
                var toTypeMethod = typeof(Value).GetMethod("ToType", new[] { typeof(Type), typeof(VM.VirtualMachine) });
                var returnTypeConst = System.Linq.Expressions.Expression.Constant(returnType);
                var toTypeCall = System.Linq.Expressions.Expression.Call(
                    callExpr, toTypeMethod, returnTypeConst, vmConst);
                body = System.Linq.Expressions.Expression.Convert(toTypeCall, returnType);
            }

            var lambda = System.Linq.Expressions.Expression.Lambda(delegateType, body, paramExprs);
            return lambda.Compile();
        }

        /// <summary>构建将参数转换为 Value 的表达式</summary>
        private static System.Linq.Expressions.Expression BuildConvertToValue(System.Linq.Expressions.Expression param)
        {
            var paramType = param.Type;

            // 如果已经是 Value，直接返回
            if (paramType == typeof(Value))
                return param;

            // bool -> Value.FromBool
            if (paramType == typeof(bool))
                return System.Linq.Expressions.Expression.Call(
                    typeof(Value).GetMethod("FromBool"), param);

            // 数值类型 -> Value.FromNumber
            if (paramType == typeof(int) || paramType == typeof(long) ||
                paramType == typeof(float) || paramType == typeof(double))
            {
                var converted = System.Linq.Expressions.Expression.Convert(param, typeof(double));
                return System.Linq.Expressions.Expression.Call(
                    typeof(Value).GetMethod("FromNumber"), converted);
            }

            // string -> Value.FromObject(MiniPandaString.Create)
            if (paramType == typeof(string))
            {
                var createMethod = typeof(MiniPandaString).GetMethod("Create", new[] { typeof(string) });
                var newStr = System.Linq.Expressions.Expression.Call(createMethod, param);
                return System.Linq.Expressions.Expression.Call(
                    typeof(Value).GetMethod("FromObject"), newStr);
            }

            // 其他类型 -> ConvertArg
            var boxed = System.Linq.Expressions.Expression.Convert(param, typeof(object));
            return System.Linq.Expressions.Expression.Call(
                typeof(Value).GetMethod("ConvertArg", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
                boxed);
        }

        /// <summary>将 C# 参数转换为 Value</summary>
        internal static Value ConvertArg(object arg)
        {
            return arg switch
            {
                null => Null,
                bool b => FromBool(b),
                int i => FromNumber(i),
                long l => FromNumber(l),
                float f => FromNumber(f),
                double d => FromNumber(d),
                string s => FromObject(MiniPandaString.Create(s)),
                Value v => v,
                _ => FromObject(MiniPandaString.Create(arg.ToString()))
            };
        }

        /// <summary>展开 params 数组为 Value[]</summary>
        private static Value[] ExpandParamsArray(object[] args)
        {
            if (args == null) return Array.Empty<Value>();
            var result = new Value[args.Length];
            for (int i = 0; i < args.Length; i++)
                result[i] = ConvertArg(args[i]);
            return result;
        }

        /// <summary>合并两个 Value 数组</summary>
        private static Value[] ConcatValueArrays(Value[] a, Value[] b)
        {
            var result = new Value[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        #endregion

        #region 重写方法和运算符

        /// <summary>转换为字符串表示</summary>
        public override string ToString() => AsString();

        /// <summary>值相等比较</summary>
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

        /// <summary>获取哈希码</summary>
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

        /// <summary>相等运算符</summary>
        public static bool operator ==(Value a, Value b) => a.Equals(b);
        /// <summary>不等运算符</summary>
        public static bool operator !=(Value a, Value b) => !a.Equals(b);

        /// <summary>从 double 隐式转换</summary>
        public static implicit operator Value(double n) => FromNumber(n);
        /// <summary>从 bool 隐式转换</summary>
        public static implicit operator Value(bool b) => FromBool(b);
        /// <summary>从 string 隐式转换</summary>
        public static implicit operator Value(string s) => s == null ? Null : FromObject(MiniPandaString.Create(s));

        #endregion
    }

    /// <summary>
    /// 可调用对象接口
    /// </summary>
    public interface ICallable
    {
        /// <summary>参数数量</summary>
        int Arity { get; }
        /// <summary>调用函数</summary>
        Value Call(VM.VirtualMachine vm, Value[] args);
    }
}
