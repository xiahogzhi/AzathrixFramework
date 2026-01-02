using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 随机扩展方法
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// 计算概率（范围 0-1）
        /// </summary>
        /// <param name="chance">概率值 0-1</param>
        /// <returns>是否命中</returns>
        public static bool RandomChance01(this float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return Random.Range(0f, 1f) <= chance;
        }

        #region 加权随机

        /// <summary>
        /// 加权随机（int 权重列表）
        /// </summary>
        /// <returns>随机结果的索引，失败返回 -1</returns>
        public static int WeightRandom(this IList<int> list)
        {
            if (list.Any(i => i < 0)) return -1;

            int sum = list.Sum();
            if (sum <= 0) return -1;

            int r = Random.Range(1, sum + 1);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == 0) continue;
                r -= list[i];
                if (r <= 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// 加权随机（float 权重列表）
        /// </summary>
        /// <returns>随机结果的索引，失败返回 -1</returns>
        public static int WeightRandom(this IList<float> list)
        {
            if (list.Any(i => i < 0)) return -1;

            float sum = list.Sum();
            if (sum <= 0) return -1;

            float r = Random.Range(0f, sum);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == 0f) continue;
                r -= list[i];
                if (r <= 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// 加权随机（自定义权重获取器）
        /// </summary>
        public static int WeightRandom<T>(this IList<T> list, Func<T, int> getter)
        {
            if (list.Any(i => getter(i) < 0)) return -1;

            int sum = list.Sum(getter);
            if (sum <= 0) return -1;

            int r = Random.Range(1, sum + 1);
            for (int i = 0; i < list.Count; i++)
            {
                var c = getter(list[i]);
                if (c == 0) continue;
                r -= c;
                if (r <= 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// 加权随机（自定义权重获取器，float）
        /// </summary>
        public static int WeightRandom<T>(this IList<T> list, Func<T, float> getter)
        {
            if (list.Any(i => getter(i) < 0)) return -1;

            float sum = list.Sum(getter);
            if (sum <= 0) return -1;

            float r = Random.Range(0f, sum);
            for (int i = 0; i < list.Count; i++)
            {
                var c = getter(list[i]);
                if (c == 0f) continue;
                r -= c;
                if (r <= 0) return i;
            }
            return -1;
        }

        #endregion

        #region 圆桌概率 (Round Table)

        /// <summary>
        /// 圆桌概率：根据权重列表必定选中一个结果
        /// </summary>
        public static int RoundTableRandom(this IList<float> weights)
        {
            if (weights == null || weights.Count == 0) return -1;

            float total = weights.Sum();
            if (total <= 0) return -1;

            float r = Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i];
                if (r < cumulative)
                    return i;
            }
            return weights.Count - 1;
        }

        /// <summary>
        /// 圆桌概率：根据权重列表必定选中一个结果
        /// </summary>
        public static int RoundTableRandom(this IList<int> weights)
        {
            if (weights == null || weights.Count == 0) return -1;

            int total = weights.Sum();
            if (total <= 0) return -1;

            int r = Random.Range(0, total);
            int cumulative = 0;

            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i];
                if (r < cumulative)
                    return i;
            }
            return weights.Count - 1;
        }

        /// <summary>
        /// 圆桌概率：自定义权重获取器
        /// </summary>
        public static int RoundTableRandom<T>(this IList<T> list, Func<T, float> weightGetter)
        {
            if (list == null || list.Count == 0) return -1;

            float total = list.Sum(weightGetter);
            if (total <= 0) return -1;

            float r = Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < list.Count; i++)
            {
                cumulative += weightGetter(list[i]);
                if (r < cumulative)
                    return i;
            }
            return list.Count - 1;
        }

        #endregion

        #region 范围随机

        /// <summary>
        /// 在矩形范围内随机一个点
        /// </summary>
        public static Vector2 RandomPoint(this Rect rect)
        {
            return new Vector2(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(rect.yMin, rect.yMax)
            );
        }

        /// <summary>
        /// 在 Bounds 范围内随机一个点
        /// </summary>
        public static Vector3 RandomPoint(this Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }

        #endregion
    }
}
