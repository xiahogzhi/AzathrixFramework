using System;
using System.Text;
using System.Globalization;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Exceptions;
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

            // JSON
            RegisterJSON(env);

            // Debug functions
            RegisterDebug(env);
        }

        private static void RegisterJSON(Environment env)
        {
            // JSON.parse - parse JSON string to MiniPanda value
            // JSON.stringify - convert MiniPanda value to JSON string
            var jsonObj = new MiniPandaObject();

            jsonObj.Set("parse", Value.FromObject(NativeFunc.Create((Value v) =>
            {
                var json = v.AsString();
                return ParseJson(json);
            })));

            jsonObj.Set("stringify", Value.FromObject(NativeFunc.Create((Value v) =>
            {
                return Value.FromObject(new MiniPandaString(StringifyJson(v)));
            })));

            env.Define("JSON", Value.FromObject(jsonObj));
        }

        private static Value ParseJson(string json)
        {
            json = json.Trim();
            if (string.IsNullOrEmpty(json)) return Value.Null;

            int index = 0;
            return ParseJsonValue(json, ref index);
        }

        private static Value ParseJsonValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return Value.Null;

            char c = json[index];

            if (c == '"') return ParseJsonString(json, ref index);
            if (c == '{') return ParseJsonObject(json, ref index);
            if (c == '[') return ParseJsonArray(json, ref index);
            if (c == 't' || c == 'f') return ParseJsonBool(json, ref index);
            if (c == 'n') return ParseJsonNull(json, ref index);
            if (c == '-' || char.IsDigit(c)) return ParseJsonNumber(json, ref index);

            return Value.Null;
        }

        private static Value ParseJsonString(string json, ref int index)
        {
            index++; // skip opening "
            var sb = new StringBuilder();
            while (index < json.Length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < json.Length)
                {
                    index++;
                    switch (json[index])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append(json[index]); break;
                    }
                }
                else
                {
                    sb.Append(json[index]);
                }
                index++;
            }
            index++; // skip closing "
            return Value.FromObject(new MiniPandaString(sb.ToString()));
        }

        private static Value ParseJsonObject(string json, ref int index)
        {
            index++; // skip {
            var obj = new MiniPandaObject();
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                SkipWhitespace(json, ref index);
                if (json[index] == '}') break;

                // Parse key
                var keyValue = ParseJsonString(json, ref index);
                var key = keyValue.As<MiniPandaString>()?.Value ?? "";

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':') index++; // skip :

                // Parse value
                var value = ParseJsonValue(json, ref index);
                obj.Set(key, value);

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++; // skip ,
            }
            index++; // skip }
            return Value.FromObject(obj);
        }

        private static Value ParseJsonArray(string json, ref int index)
        {
            index++; // skip [
            var arr = new MiniPandaArray();
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                SkipWhitespace(json, ref index);
                if (json[index] == ']') break;

                var value = ParseJsonValue(json, ref index);
                arr.Push(value);

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++; // skip ,
            }
            index++; // skip ]
            return Value.FromObject(arr);
        }

        private static Value ParseJsonBool(string json, ref int index)
        {
            if (json.Substring(index).StartsWith("true"))
            {
                index += 4;
                return Value.True;
            }
            if (json.Substring(index).StartsWith("false"))
            {
                index += 5;
                return Value.False;
            }
            return Value.Null;
        }

        private static Value ParseJsonNull(string json, ref int index)
        {
            if (json.Substring(index).StartsWith("null"))
            {
                index += 4;
            }
            return Value.Null;
        }

        private static Value ParseJsonNumber(string json, ref int index)
        {
            int start = index;
            if (json[index] == '-') index++;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
            {
                if ((json[index] == '+' || json[index] == '-') && index > start && json[index - 1] != 'e' && json[index - 1] != 'E')
                    break;
                index++;
            }
            var numStr = json.Substring(start, index - start);
            if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return Value.FromNumber(num);
            return Value.Null;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static string StringifyJson(Value value)
        {
            if (value.IsNull) return "null";
            if (value.IsBool) return value.Bool ? "true" : "false";
            if (value.IsNumber) return value.Number.ToString(CultureInfo.InvariantCulture);

            if (value.As<MiniPandaString>() is { } str)
            {
                return "\"" + EscapeJsonString(str.Value) + "\"";
            }

            if (value.As<MiniPandaArray>() is { } arr)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < arr.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(StringifyJson(arr.Get(i)));
                }
                sb.Append("]");
                return sb.ToString();
            }

            if (value.As<MiniPandaObject>() is { } obj)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kvp in obj.Fields)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeJsonString(kvp.Key)).Append("\":");
                    sb.Append(StringifyJson(kvp.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (value.As<MiniPandaInstance>() is { } inst)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kvp in inst.Fields)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeJsonString(kvp.Key)).Append("\":");
                    sb.Append(StringifyJson(kvp.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }

            return "null";
        }

        private static string EscapeJsonString(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void RegisterDebug(Environment env)
        {
            // trace - print value with file:line info
            env.Define("trace", Value.FromObject(NativeFunc.CreateWithVM((vm, args) =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(" ");
                    sb.Append(args[i].AsString());
                }
                var location = vm.GetCurrentLocation();
                Debug.Log($"[TRACE] {sb} (at {location})");
                return Value.Null;
            })));

            // debug - same as trace, for breakpoint hooks
            env.Define("debug", Value.FromObject(NativeFunc.CreateWithVM((vm, args) =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(" ");
                    sb.Append(args[i].AsString());
                }
                var location = vm.GetCurrentLocation();
                Debug.Log($"[DEBUG] {sb} (at {location})");
                return Value.Null;
            })));

            // stacktrace - return call stack as string
            env.Define("stacktrace", Value.FromObject(NativeFunc.CreateWithVM((vm, args) =>
            {
                var sb = new StringBuilder();
                var stack = vm.GetStackTrace();
                foreach (var frame in stack)
                {
                    sb.AppendLine($"  at {frame.Function} ({frame.File}:{frame.Line})");
                }
                return Value.FromObject(new MiniPandaString(sb.ToString()));
            })));

            // assert - throw error if condition is false
            env.Define("assert", Value.FromObject(NativeFunc.CreateWithVM((vm, args) =>
            {
                if (args.Length == 0) return Value.Null;
                var condition = args[0].AsBool();
                if (!condition)
                {
                    var message = args.Length > 1 ? args[1].AsString() : "Assertion failed";
                    var location = vm.GetCurrentLocation();
                    throw new MiniPandaRuntimeException($"{message} (at {location})");
                }
                return Value.Null;
            })));
        }
    }
}
