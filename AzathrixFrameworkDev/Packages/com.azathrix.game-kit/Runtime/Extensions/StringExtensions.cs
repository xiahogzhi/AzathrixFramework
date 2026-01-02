using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 字符串扩展方法
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// 判断字符串是否为空或空白
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        /// <summary>
        /// 判断字符串是否为空
        /// </summary>
        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// 截断字符串
        /// </summary>
        public static string Truncate(this string s, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLength)
                return s;
            return s.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// 转换为标题大小写
        /// </summary>
        public static string ToTitleCase(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
        }

        /// <summary>
        /// 转换为驼峰命名
        /// </summary>
        public static string ToCamelCase(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var words = Regex.Split(s, @"[\s_-]+");
            var sb = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                if (string.IsNullOrEmpty(words[i]))
                    continue;

                if (sb.Length == 0)
                    sb.Append(words[i].ToLower());
                else
                    sb.Append(char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 转换为蛇形命名
        /// </summary>
        public static string ToSnakeCase(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c))
                {
                    if (sb.Length > 0)
                        sb.Append('_');
                    sb.Append(char.ToLower(c));
                }
                else if (c == ' ' || c == '-')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 移除所有空白字符
        /// </summary>
        public static string RemoveWhitespace(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return Regex.Replace(s, @"\s+", "");
        }

        /// <summary>
        /// 重复字符串
        /// </summary>
        public static string Repeat(this string s, int count)
        {
            if (string.IsNullOrEmpty(s) || count <= 0)
                return string.Empty;
            return new StringBuilder(s.Length * count).Insert(0, s, count).ToString();
        }

        /// <summary>
        /// 反转字符串
        /// </summary>
        public static string Reverse(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var chars = s.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }
    }
}
