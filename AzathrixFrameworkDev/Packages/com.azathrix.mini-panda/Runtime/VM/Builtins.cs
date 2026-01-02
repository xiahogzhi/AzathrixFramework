using System;
using System.Text;
using System.Globalization;
using Azathrix.MiniPanda.Core;
using UnityEngine;
using Environment = Azathrix.MiniPanda.Core.Environment;
using Random = System.Random;

namespace Azathrix.MiniPanda.VM
{
    public static class Builtins
    {
        public static bool PrintStackTrace { get; set; } = false;

        public static void Register(Environment env)
        {
            StringBuilder sb = new StringBuilder();
            env.Define("print", Value.FromObject(NativeFunc.CreateWithVM((vm, args) =>
            {
                var msg = args.Length > 0 ? args[0].AsString() : "";

                if (PrintStackTrace)
                {
                    sb.AppendLine(msg);
                    sb.AppendLine("");
                    foreach (var variable in vm.GetStackTrace())
                    {
                        sb.AppendLine($"\t{variable.Function} (at {variable.File}:{variable.Line})");
                    }
                    Debug.Log(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    Debug.Log(msg);
                }
                return Value.Null;
            })));
            
            // Type checking
            env.Define("type", Value.FromObject(NativeFunc.Create((Value v) =>
            {
                if (v.IsNull) return "null";
                if (v.IsBool) return "bool";
                if (v.IsNumber) return "number";
                if (v.IsString) return "string";
                if (v.IsArray) return "array";
                if (v.IsDict) return "object";
                if (v.IsFunction) return "function";
                if (v.IsClass) return "class";
                if (v.IsInstance) return "instance";
                return "unknown";
            })));

            // Conversions
            env.Define("str", Value.FromObject(NativeFunc.Create((Value v) => v.AsString())));
            env.Define("num", Value.FromObject(NativeFunc.Create((Value v) =>
            {
                if (v.IsNumber) return v;
                if (double.TryParse(v.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return Value.FromNumber(n);
                return Value.Null;
            })));
            env.Define("bool", Value.FromObject(NativeFunc.Create((Value v) => Value.FromBool(v.AsBool()))));

            // Math
            env.Define("abs",
                Value.FromObject(NativeFunc.Create((Value v) => Value.FromNumber(Math.Abs(v.AsNumber())))));
            env.Define("floor",
                Value.FromObject(NativeFunc.Create((Value v) => Value.FromNumber(Math.Floor(v.AsNumber())))));
            env.Define("ceil",
                Value.FromObject(NativeFunc.Create((Value v) => Value.FromNumber(Math.Ceiling(v.AsNumber())))));
            env.Define("round",
                Value.FromObject(NativeFunc.Create((Value v) => Value.FromNumber(Math.Round(v.AsNumber())))));
            env.Define("sqrt",
                Value.FromObject(NativeFunc.Create((Value v) => Value.FromNumber(Math.Sqrt(v.AsNumber())))));
            env.Define("pow",
                Value.FromObject(NativeFunc.Create((Value a, Value b) =>
                    Value.FromNumber(Math.Pow(a.AsNumber(), b.AsNumber())))));
            env.Define("min", Value.FromObject(NativeFunc.Create(args =>
            {
                if (args.Length == 0) return Value.Null;
                var min = args[0].AsNumber();
                for (int i = 1; i < args.Length; i++)
                {
                    var n = args[i].AsNumber();
                    if (n < min) min = n;
                }

                return Value.FromNumber(min);
            })));
            env.Define("max", Value.FromObject(NativeFunc.Create(args =>
            {
                if (args.Length == 0) return Value.Null;
                var max = args[0].AsNumber();
                for (int i = 1; i < args.Length; i++)
                {
                    var n = args[i].AsNumber();
                    if (n > max) max = n;
                }

                return Value.FromNumber(max);
            })));

            // Range
            env.Define("range", Value.FromObject(NativeFunc.Create(args =>
            {
                int start = 0, end = 0, step = 1;
                if (args.Length == 1)
                {
                    end = (int) args[0].AsNumber();
                }
                else if (args.Length >= 2)
                {
                    start = (int) args[0].AsNumber();
                    end = (int) args[1].AsNumber();
                    if (args.Length >= 3)
                    {
                        step = (int) args[2].AsNumber();
                    }
                }

                var array = new MiniPandaArray();
                if (step > 0)
                {
                    for (int i = start; i < end; i += step)
                    {
                        array.Push(Value.FromNumber(i));
                    }
                }
                else if (step < 0)
                {
                    for (int i = start; i > end; i += step)
                    {
                        array.Push(Value.FromNumber(i));
                    }
                }

                return Value.FromObject(array);
            })));

            // Array operations
            env.Define("len", Value.FromObject(NativeFunc.Create((Value v) =>
            {
                if (v.As<MiniPandaArray>() is { } arr) return Value.FromNumber(arr.Length);
                if (v.As<MiniPandaString>() is { } str) return Value.FromNumber(str.Value.Length);
                if (v.As<MiniPandaObject>() is { } obj) return Value.FromNumber(obj.Fields.Count);
                return Value.FromNumber(0);
            })));

            env.Define("push", Value.FromObject(NativeFunc.Create((Value arr, Value val) =>
            {
                if (arr.As<MiniPandaArray>() is { } array)
                {
                    array.Push(val);
                }

                return arr;
            })));

            env.Define("pop", Value.FromObject(NativeFunc.Create((Value arr) =>
            {
                if (arr.As<MiniPandaArray>() is { } array)
                {
                    return array.Pop();
                }

                return Value.Null;
            })));

            // Time
            env.Define("time", Value.FromObject(NativeFunc.Create(() =>
                Value.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0))));

            // Random
            var random = new Random();
            env.Define("random", Value.FromObject(NativeFunc.Create(() =>
                Value.FromNumber(random.NextDouble()))));

            env.Define("randomInt", Value.FromObject(NativeFunc.Create((Value min, Value max) =>
                Value.FromNumber(random.Next((int) min.AsNumber(), (int) max.AsNumber())))));
        }
    }
}
