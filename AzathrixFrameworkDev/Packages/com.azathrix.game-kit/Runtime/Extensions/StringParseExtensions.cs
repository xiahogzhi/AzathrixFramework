using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Azathrix.Framework.Tools;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 字符串解析扩展方法
    /// </summary>
    public static class StringParseExtensions
    {
        #region 基础类型解析

        /// <summary>
        /// 尝试解析为 bool
        /// </summary>
        public static bool TryParseBool(this string str, bool defaultValue = default)
        {
            return bool.TryParse(str, out var p) ? p : defaultValue;
        }

        /// <summary>
        /// 尝试解析为 int
        /// </summary>
        public static int TryParseInt(this string str, int defaultValue = default)
        {
            return int.TryParse(str, out var p) ? p : defaultValue;
        }

        /// <summary>
        /// 尝试解析为 long
        /// </summary>
        public static long TryParseLong(this string str, long defaultValue = default)
        {
            return long.TryParse(str, out var p) ? p : defaultValue;
        }

        /// <summary>
        /// 尝试解析为 float
        /// </summary>
        public static float TryParseFloat(this string str, float defaultValue = default)
        {
            return float.TryParse(str, out var p) ? p : defaultValue;
        }

        /// <summary>
        /// 尝试解析为 double
        /// </summary>
        public static double TryParseDouble(this string str, double defaultValue = default)
        {
            return double.TryParse(str, out var p) ? p : defaultValue;
        }

        /// <summary>
        /// 尝试解析为枚举
        /// </summary>
        public static T TryParseEnum<T>(this string str, T defaultValue = default) where T : struct, Enum
        {
            return Enum.TryParse<T>(str, out var p) ? p : defaultValue;
        }

        #endregion

        #region 动态类型解析

        private static readonly Dictionary<string, object> ParseCache = new();

        /// <summary>
        /// 根据类型动态解析字符串
        /// </summary>
        /// <param name="param">字符串参数</param>
        /// <param name="type">目标类型</param>
        /// <param name="info">调试信息</param>
        public static object ParseByType(this string param, Type type, string info = null)
        {
            if (string.IsNullOrEmpty(param))
                return null;

            var rid = $"{type.GetHashCode()}_{param.GetHashCode()}";
            if (ParseCache.TryGetValue(rid, out var cached))
                return cached;

            try
            {
                object result;

                if (type.IsArray)
                {
                    var el = type.GetElementType();
                    if (el == null) return null;

                    var parts = param.Split(",");
                    var array = Array.CreateInstance(el, parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                        array.SetValue(parts[i].ParseByType(el, info), i);

                    result = array;
                }
                else if (type == typeof(int))
                    result = int.Parse(param);
                else if (type == typeof(float))
                    result = float.Parse(param);
                else if (type == typeof(bool))
                    result = bool.Parse(param);
                else if (type == typeof(string))
                    result = param;
                else if (type.IsEnum)
                    result = Enum.Parse(type, param);
                else
                    return null;

                ParseCache[rid] = result;
                return result;
            }
            catch (Exception e)
            {
                Log.Error($"[{info}]无法将：{param} 转换为 {type}");
                Debug.LogException(e);
                return null;
            }
        }

        #endregion

        #region 特殊格式解析

        /// <summary>
        /// 解析参数字符串为字典
        /// <para>格式: "1.0,10|2.0,20" -> {1.0: 10, 2.0: 20}</para>
        /// </summary>
        public static Dictionary<double, int> ParameterCutting(this string str)
        {
            var result = new Dictionary<double, int>();
            var parts = str.Split("|");

            if (parts.Length > 1)
            {
                foreach (var part in parts)
                {
                    var numbers = part.Split(',');
                    if (double.TryParse(numbers[0].Trim(), out var first) &&
                        int.TryParse(numbers[1].Trim(), out var second))
                    {
                        result.TryAdd(first, second);
                    }
                }
            }
            else
            {
                if (int.TryParse(parts[0].Trim(), out var first))
                    result.TryAdd(1, first);
            }

            return result;
        }

        /// <summary>
        /// 解析元组字符串为整数列表
        /// <para>格式: "(1,42)(43,126)" -> [1, 42, 43, 126]</para>
        /// </summary>
        public static List<int> TryParseTuple(this string tupleStr)
        {
            return Regex.Matches(tupleStr, @"\d+")
                .Cast<Match>()
                .Select(m => int.Parse(m.Value))
                .ToList();
        }

        #endregion
    }
}
