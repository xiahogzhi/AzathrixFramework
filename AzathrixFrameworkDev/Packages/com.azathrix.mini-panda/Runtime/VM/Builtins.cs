using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.Exceptions;
using UnityEngine;
using Environment = Azathrix.MiniPanda.Core.Environment;
using Random = System.Random;

namespace Azathrix.MiniPanda.VM
{
    /// <summary>
    /// 内置函数注册器
    /// </summary>
    /// <remarks>
    /// 提供 MiniPanda 脚本的标准库函数，包括：
    /// - 输出和类型检查：print, type
    /// - 类型转换：str, num, bool
    /// - 数学函数：abs, floor, ceil, round, sqrt, pow, min, max
    /// - 数组操作：len, push, pop, range
    /// - 集合函数：keys, values, contains, slice, join, split
    /// - 时间函数：time, random, randomInt
    /// - JSON：json.parse, json.stringify
    /// - 日期时间：date.now, date.format, date.parse 等
    /// - 正则表达式：regex.test, regex.match, regex.replace 等
    /// - 调试函数：trace, debug, stacktrace, assert
    /// </remarks>
    public static class Builtins
    {
        /// <summary>是否在 print 时打印调用栈</summary>
        public static bool PrintStackTrace { get; set; } = false;

        /// <summary>
        /// 注册所有内置函数到环境
        /// </summary>
        /// <param name="env">目标环境</param>
        public static void Register(Environment env)
        {
            StringBuilder sb = new StringBuilder();
            env.Define("print", Value.FromObject(NativeFunction.CreateWithVM((vm, args) =>
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

            #region 类型检查和转换

            // Type checking
            env.Define("type", Value.FromObject(NativeFunction.Create((Value v) =>
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
            env.Define("str", Value.FromObject(NativeFunction.Create((Value v) => v.AsString())));
            env.Define("num", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                if (v.IsNumber) return v;
                if (double.TryParse(v.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return Value.FromNumber(n);
                return Value.Null;
            })));
            env.Define("bool", Value.FromObject(NativeFunction.Create((Value v) => Value.FromBool(v.AsBool()))));

            #endregion

            #region 数学函数

            // Math
            env.Define("abs",
                Value.FromObject(NativeFunction.Create((Value v) => Value.FromNumber(Math.Abs(v.AsNumber())))));
            env.Define("floor",
                Value.FromObject(NativeFunction.Create((Value v) => Value.FromNumber(Math.Floor(v.AsNumber())))));
            env.Define("ceil",
                Value.FromObject(NativeFunction.Create((Value v) => Value.FromNumber(Math.Ceiling(v.AsNumber())))));
            env.Define("round",
                Value.FromObject(NativeFunction.Create((Value v) => Value.FromNumber(Math.Round(v.AsNumber())))));
            env.Define("sqrt",
                Value.FromObject(NativeFunction.Create((Value v) => Value.FromNumber(Math.Sqrt(v.AsNumber())))));
            env.Define("pow",
                Value.FromObject(NativeFunction.Create((Value a, Value b) =>
                    Value.FromNumber(Math.Pow(a.AsNumber(), b.AsNumber())))));
            env.Define("min", Value.FromObject(NativeFunction.Create(args =>
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
            env.Define("max", Value.FromObject(NativeFunction.Create(args =>
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

            #endregion

            #region 数组和集合操作

            // Range
            env.Define("range", Value.FromObject(NativeFunction.Create(args =>
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
            env.Define("len", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                if (v.As<MiniPandaArray>() is { } arr) return Value.FromNumber(arr.Length);
                if (v.As<MiniPandaString>() is { } str) return Value.FromNumber(str.Value.Length);
                if (v.As<MiniPandaObject>() is { } obj) return Value.FromNumber(obj.Fields.Count);
                return Value.FromNumber(0);
            })));

            env.Define("push", Value.FromObject(NativeFunction.Create((Value arr, Value val) =>
            {
                if (arr.As<MiniPandaArray>() is { } array)
                {
                    array.Push(val);
                }

                return arr;
            })));

            env.Define("pop", Value.FromObject(NativeFunction.Create((Value arr) =>
            {
                if (arr.As<MiniPandaArray>() is { } array)
                {
                    return array.Pop();
                }

                return Value.Null;
            })));

            // Collection functions
            env.Define("keys", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                var result = new MiniPandaArray();
                if (v.As<MiniPandaObject>() is { } obj)
                {
                    foreach (var key in obj.Fields.Keys)
                        result.Push(Value.FromObject(new MiniPandaString(key)));
                }
                else if (v.As<MiniPandaInstance>() is { } inst)
                {
                    foreach (var key in inst.Fields.Keys)
                        result.Push(Value.FromObject(new MiniPandaString(key)));
                }
                return Value.FromObject(result);
            })));

            env.Define("values", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                var result = new MiniPandaArray();
                if (v.As<MiniPandaObject>() is { } obj)
                {
                    foreach (var val in obj.Fields.Values)
                        result.Push(val);
                }
                else if (v.As<MiniPandaInstance>() is { } inst)
                {
                    foreach (var val in inst.Fields.Values)
                        result.Push(val);
                }
                return Value.FromObject(result);
            })));

            env.Define("contains", Value.FromObject(NativeFunction.Create((Value collection, Value item) =>
            {
                if (collection.As<MiniPandaArray>() is { } arr)
                {
                    for (int i = 0; i < arr.Length; i++)
                        if (arr.Get(i).Equals(item)) return Value.True;
                    return Value.False;
                }
                if (collection.As<MiniPandaObject>() is { } obj)
                {
                    return Value.FromBool(obj.Fields.ContainsKey(item.AsString()));
                }
                if (collection.As<MiniPandaString>() is { } str)
                {
                    return Value.FromBool(str.Value.Contains(item.AsString()));
                }
                return Value.False;
            })));

            env.Define("slice", Value.FromObject(NativeFunction.Create(args =>
            {
                if (args.Length < 2) return Value.Null;
                var start = (int)args[1].AsNumber();
                var end = args.Length > 2 ? (int)args[2].AsNumber() : -1;

                if (args[0].As<MiniPandaArray>() is { } arr)
                {
                    if (start < 0) start = arr.Length + start;
                    if (end < 0) end = arr.Length + end + 1;
                    var result = new MiniPandaArray();
                    for (int i = start; i < end && i < arr.Length; i++)
                        result.Push(arr.Get(i));
                    return Value.FromObject(result);
                }
                if (args[0].As<MiniPandaString>() is { } str)
                {
                    if (start < 0) start = str.Value.Length + start;
                    if (end < 0) end = str.Value.Length + end + 1;
                    end = Math.Min(end, str.Value.Length);
                    return Value.FromObject(new MiniPandaString(str.Value.Substring(start, end - start)));
                }
                return Value.Null;
            })));

            env.Define("join", Value.FromObject(NativeFunction.Create((Value arr, Value sep) =>
            {
                if (arr.As<MiniPandaArray>() is { } array)
                {
                    var sb = new StringBuilder();
                    var separator = sep.AsString();
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (i > 0) sb.Append(separator);
                        sb.Append(array.Get(i).AsString());
                    }
                    return Value.FromObject(new MiniPandaString(sb.ToString()));
                }
                return Value.Null;
            })));

            env.Define("split", Value.FromObject(NativeFunction.Create((Value str, Value sep) =>
            {
                var result = new MiniPandaArray();
                if (str.As<MiniPandaString>() is { } s)
                {
                    var parts = s.Value.Split(new[] { sep.AsString() }, StringSplitOptions.None);
                    foreach (var part in parts)
                        result.Push(Value.FromObject(new MiniPandaString(part)));
                }
                return Value.FromObject(result);
            })));

            #endregion

            #region 时间和随机数

            // Time
            env.Define("time", Value.FromObject(NativeFunction.Create(() =>
                Value.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0))));

            // Random
            var random = new Random();
            env.Define("random", Value.FromObject(NativeFunction.Create(() =>
                Value.FromNumber(random.NextDouble()))));

            env.Define("randomInt", Value.FromObject(NativeFunction.Create((Value min, Value max) =>
                Value.FromNumber(random.Next((int) min.AsNumber(), (int) max.AsNumber())))));

            #endregion

            // 注册子模块
            RegisterJSON(env);
            RegisterDebug(env);
            RegisterDateTime(env);
            RegisterRegex(env);
        }

        #region 日期时间模块

        /// <summary>注册日期时间相关函数</summary>
        private static void RegisterDateTime(Environment env)
        {
            var dateObj = new MiniPandaObject();

            // Date.now() - current timestamp in milliseconds
            dateObj.Set("now", Value.FromObject(NativeFunction.Create(() =>
                Value.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))));

            // Date.time() - current timestamp in seconds (float)
            dateObj.Set("time", Value.FromObject(NativeFunction.Create(() =>
                Value.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0))));

            // Date.year/month/day/hour/minute/second - get component from timestamp (ms)
            dateObj.Set("year", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Year))));
            dateObj.Set("month", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Month))));
            dateObj.Set("day", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Day))));
            dateObj.Set("hour", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Hour))));
            dateObj.Set("minute", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Minute))));
            dateObj.Set("second", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber(DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.Second))));
            dateObj.Set("weekday", Value.FromObject(NativeFunction.Create((Value ts) =>
                Value.FromNumber((int)DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime.DayOfWeek))));

            // Date.format(timestamp, format) - format timestamp
            dateObj.Set("format", Value.FromObject(NativeFunction.Create((Value ts, Value fmt) =>
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds((long)ts.AsNumber()).LocalDateTime;
                var format = fmt.IsNull ? "yyyy-MM-dd HH:mm:ss" : fmt.AsString();
                return Value.FromObject(new MiniPandaString(dt.ToString(format)));
            })));

            // Date.parse(str) - parse date string to timestamp (ms)
            dateObj.Set("parse", Value.FromObject(NativeFunction.Create((Value str) =>
            {
                if (DateTime.TryParse(str.AsString(), out var dt))
                    return Value.FromNumber(new DateTimeOffset(dt).ToUnixTimeMilliseconds());
                return Value.Null;
            })));

            // Date.create(year, month, day, hour?, minute?, second?) - create timestamp
            dateObj.Set("create", Value.FromObject(NativeFunction.Create(args =>
            {
                if (args.Length < 3) return Value.Null;
                var year = (int)args[0].AsNumber();
                var month = (int)args[1].AsNumber();
                var day = (int)args[2].AsNumber();
                var hour = args.Length > 3 ? (int)args[3].AsNumber() : 0;
                var minute = args.Length > 4 ? (int)args[4].AsNumber() : 0;
                var second = args.Length > 5 ? (int)args[5].AsNumber() : 0;
                var dt = new DateTime(year, month, day, hour, minute, second);
                return Value.FromNumber(new DateTimeOffset(dt).ToUnixTimeMilliseconds());
            })));

            env.Define("date", Value.FromObject(dateObj));

            // Shorthand: now() returns current timestamp in ms
            env.Define("now", Value.FromObject(NativeFunction.Create(() =>
                Value.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))));
        }

        #endregion

        #region 正则表达式模块

        /// <summary>注册正则表达式相关函数</summary>
        private static void RegisterRegex(Environment env)
        {
            var regexObj = new MiniPandaObject();

            // regex.test(pattern, str) - returns true if pattern matches
            regexObj.Set("test", Value.FromObject(NativeFunction.Create((Value pattern, Value str) =>
            {
                try
                {
                    return Value.FromBool(Regex.IsMatch(str.AsString(), pattern.AsString()));
                }
                catch
                {
                    return Value.False;
                }
            })));

            // regex.match(pattern, str) - returns first match or null
            regexObj.Set("match", Value.FromObject(NativeFunction.Create((Value pattern, Value str) =>
            {
                try
                {
                    var match = Regex.Match(str.AsString(), pattern.AsString());
                    if (!match.Success) return Value.Null;

                    var result = new MiniPandaObject();
                    result.Set("value", Value.FromObject(new MiniPandaString(match.Value)));
                    result.Set("index", Value.FromNumber(match.Index));

                    var groups = new MiniPandaArray();
                    foreach (Group g in match.Groups)
                    {
                        groups.Elements.Add(Value.FromObject(new MiniPandaString(g.Value)));
                    }
                    result.Set("groups", Value.FromObject(groups));

                    return Value.FromObject(result);
                }
                catch
                {
                    return Value.Null;
                }
            })));

            // regex.matchAll(pattern, str) - returns all matches
            regexObj.Set("matchAll", Value.FromObject(NativeFunction.Create((Value pattern, Value str) =>
            {
                try
                {
                    var matches = Regex.Matches(str.AsString(), pattern.AsString());
                    var results = new MiniPandaArray();

                    foreach (Match match in matches)
                    {
                        var result = new MiniPandaObject();
                        result.Set("value", Value.FromObject(new MiniPandaString(match.Value)));
                        result.Set("index", Value.FromNumber(match.Index));

                        var groups = new MiniPandaArray();
                        foreach (Group g in match.Groups)
                        {
                            groups.Elements.Add(Value.FromObject(new MiniPandaString(g.Value)));
                        }
                        result.Set("groups", Value.FromObject(groups));

                        results.Elements.Add(Value.FromObject(result));
                    }

                    return Value.FromObject(results);
                }
                catch
                {
                    return Value.FromObject(new MiniPandaArray());
                }
            })));

            // regex.replace(pattern, str, replacement) - replace matches
            regexObj.Set("replace", Value.FromObject(NativeFunction.Create((Value pattern, Value str, Value replacement) =>
            {
                try
                {
                    var result = Regex.Replace(str.AsString(), pattern.AsString(), replacement.AsString());
                    return Value.FromObject(new MiniPandaString(result));
                }
                catch
                {
                    return str;
                }
            })));

            // regex.split(pattern, str) - split string by pattern
            regexObj.Set("split", Value.FromObject(NativeFunction.Create((Value pattern, Value str) =>
            {
                try
                {
                    var parts = Regex.Split(str.AsString(), pattern.AsString());
                    var result = new MiniPandaArray();
                    foreach (var part in parts)
                    {
                        result.Elements.Add(Value.FromObject(new MiniPandaString(part)));
                    }
                    return Value.FromObject(result);
                }
                catch
                {
                    var result = new MiniPandaArray();
                    result.Elements.Add(str);
                    return Value.FromObject(result);
                }
            })));

            env.Define("regex", Value.FromObject(regexObj));
        }

        #endregion

        #region JSON 模块

        /// <summary>注册 JSON 解析和序列化函数</summary>
        private static void RegisterJSON(Environment env)
        {
            // JSON.parse - parse JSON string to MiniPanda value
            // JSON.stringify - convert MiniPanda value to JSON string
            var jsonObj = new MiniPandaObject();

            jsonObj.Set("parse", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                var json = v.AsString();
                return ParseJson(json);
            })));

            jsonObj.Set("stringify", Value.FromObject(NativeFunction.Create((Value v) =>
            {
                return Value.FromObject(new MiniPandaString(StringifyJson(v)));
            })));

            env.Define("json", Value.FromObject(jsonObj));
        }

        /// <summary>解析 JSON 字符串</summary>
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

        #endregion

        #region 调试模块

        /// <summary>注册调试相关函数</summary>
        private static void RegisterDebug(Environment env)
        {
            // trace - print value with file:line info
            env.Define("trace", Value.FromObject(NativeFunction.CreateWithVM((vm, args) =>
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
            env.Define("debug", Value.FromObject(NativeFunction.CreateWithVM((vm, args) =>
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
            env.Define("stacktrace", Value.FromObject(NativeFunction.CreateWithVM((vm, args) =>
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
            env.Define("assert", Value.FromObject(NativeFunction.CreateWithVM((vm, args) =>
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

        #endregion
    }
}
