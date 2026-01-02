using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 集合扩展方法
    /// </summary>
    public static class CollectionExtensions
    {
        #region 数组

        /// <summary>
        /// 安全获取数组元素
        /// </summary>
        public static T TryGet<T>(this T[] array, int index, T defaultValue = default)
        {
            if (array == null || index < 0 || index >= array.Length)
                return defaultValue;
            return array[index];
        }

        /// <summary>
        /// 过滤数组（返回不满足条件的元素）
        /// </summary>
        public static T[] GetWithFilter<T>(this T[] array, Func<T, bool> filter)
        {
            var result = new List<T>();
            for (int i = 0; i < array.Length; i++)
            {
                if (!filter(array[i]))
                    result.Add(array[i]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// 随机打乱数组
        /// </summary>
        public static void Shuffle<T>(this T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>
        /// 随机获取数组元素
        /// </summary>
        public static T RandomElement<T>(this T[] array)
        {
            if (array == null || array.Length == 0)
                return default;
            return array[Random.Range(0, array.Length)];
        }

        /// <summary>
        /// 判断数组是否为空
        /// </summary>
        public static bool IsNullOrEmpty<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }

        #endregion

        #region 列表

        /// <summary>
        /// 安全获取列表元素
        /// </summary>
        public static T TryGet<T>(this List<T> list, int index, T defaultValue = default)
        {
            if (list == null || index < 0 || index >= list.Count)
                return defaultValue;
            return list[index];
        }

        /// <summary>
        /// 过滤列表（返回不满足条件的元素）
        /// </summary>
        public static T[] GetWithFilter<T>(this List<T> list, Func<T, bool> filter)
        {
            var result = new List<T>();
            for (int i = 0; i < list.Count; i++)
            {
                if (!filter(list[i]))
                    result.Add(list[i]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// 获取下一个可用 ID（int）
        /// </summary>
        public static int GetNextId<T>(this List<T> list, Func<T, int> idGetter)
        {
            int id = 1;
            foreach (var v in list)
            {
                var cid = idGetter(v);
                if (id <= cid)
                    id = cid + 1;
            }
            return id;
        }

        /// <summary>
        /// 获取下一个可用 ID（uint）
        /// </summary>
        public static uint GetNextUId<T>(this List<T> list, Func<T, uint> idGetter)
        {
            uint id = 1;
            foreach (var v in list)
            {
                var cid = idGetter(v);
                if (id <= cid)
                    id = cid + 1;
            }
            return id;
        }

        /// <summary>
        /// 随机打乱列表
        /// </summary>
        public static void Shuffle<T>(this List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 随机获取列表元素
        /// </summary>
        public static T RandomElement<T>(this List<T> list)
        {
            if (list == null || list.Count == 0)
                return default;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// 判断列表是否为空
        /// </summary>
        public static bool IsNullOrEmpty<T>(this List<T> list)
        {
            return list == null || list.Count == 0;
        }

        #endregion

        #region 字典

        /// <summary>
        /// 设置字典值
        /// </summary>
        public static void SetValue(this Dictionary<string, object> data, string key, object value)
        {
            data[key] = value;
        }

        /// <summary>
        /// 安全获取字典值
        /// </summary>
        public static T GetValue<T>(this Dictionary<string, object> data, string key, T defaultValue = default)
        {
            try
            {
                if (data.TryGetValue(key, out var value))
                    return (T)value;
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// 遍历集合
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
        }

        /// <summary>
        /// 遍历集合（带索引）
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            int index = 0;
            foreach (var item in source)
                action(item, index++);
        }

        /// <summary>
        /// 获取最小值元素
        /// </summary>
        public static T MinBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector) where TKey : IComparable<TKey>
        {
            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
                return default;

            T minItem = enumerator.Current;
            TKey minKey = selector(minItem);

            while (enumerator.MoveNext())
            {
                TKey key = selector(enumerator.Current);
                if (key.CompareTo(minKey) < 0)
                {
                    minKey = key;
                    minItem = enumerator.Current;
                }
            }
            return minItem;
        }

        /// <summary>
        /// 获取最大值元素
        /// </summary>
        public static T MaxBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector) where TKey : IComparable<TKey>
        {
            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
                return default;

            T maxItem = enumerator.Current;
            TKey maxKey = selector(maxItem);

            while (enumerator.MoveNext())
            {
                TKey key = selector(enumerator.Current);
                if (key.CompareTo(maxKey) > 0)
                {
                    maxKey = key;
                    maxItem = enumerator.Current;
                }
            }
            return maxItem;
        }

        /// <summary>
        /// 分批处理
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            var batch = new List<T>(size);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count >= size)
                {
                    yield return batch;
                    batch = new List<T>(size);
                }
            }
            if (batch.Count > 0)
                yield return batch;
        }

        #endregion

        #region HashSet

        /// <summary>
        /// 批量添加元素
        /// </summary>
        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items)
        {
            foreach (var item in items)
                set.Add(item);
        }

        #endregion
    }
}
