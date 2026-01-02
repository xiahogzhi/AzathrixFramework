using UnityEngine;

namespace Azathrix.GameKit.Runtime.Utils
{
    /// <summary>
    /// 随机工具类
    /// </summary>
    public static class RandomUtils
    {
        /// <summary>
        /// 随机布尔值
        /// </summary>
        public static bool RandomBool() => Random.value > 0.5f;

        /// <summary>
        /// 随机范围（整数）
        /// </summary>
        public static int RandomRange(int min, int max) => Random.Range(min, max);

        /// <summary>
        /// 随机范围（浮点数）
        /// </summary>
        public static float RandomRange(float min, float max) => Random.Range(min, max);

        /// <summary>
        /// 计算概率分配
        /// </summary>
        /// <param name="a">当前总概率</param>
        /// <param name="b">当前概率</param>
        /// <param name="isFirst">是否是第一个</param>
        /// <param name="isEnd">是否是最后一个</param>
        public static void CalcRate(ref float a, ref float b, bool isFirst, bool isEnd)
        {
            a = Mathf.Clamp01(a);
            b = Mathf.Clamp01(b);

            if (a >= 1)
            {
                b = isFirst ? 1 : 0;
                return;
            }

            if (a + b < 1)
            {
                if (isEnd)
                    b = 1 - a;
                else
                    a += b;
            }
            else
            {
                b = 1 - a;
                a = 1;
            }
        }

        #region 保底概率 (Pity System)

        /// <summary>
        /// 保底概率：每次失败后概率递增，直到必定成功
        /// </summary>
        /// <param name="baseChance">基础概率 (0-1)</param>
        /// <param name="failCount">当前连续失败次数</param>
        /// <param name="pityCount">保底次数（达到此次数必定成功）</param>
        /// <returns>是否成功</returns>
        public static bool PityRandom(float baseChance, int failCount, int pityCount)
        {
            if (failCount >= pityCount - 1) return true;
            if (pityCount <= 1) return true;

            float actualChance = baseChance + (1f - baseChance) * failCount / (pityCount - 1);
            return Random.Range(0f, 1f) <= actualChance;
        }

        /// <summary>
        /// 保底概率（软保底）：概率在接近保底时加速递增
        /// </summary>
        /// <param name="baseChance">基础概率 (0-1)</param>
        /// <param name="failCount">当前连续失败次数</param>
        /// <param name="softPity">软保底开始次数</param>
        /// <param name="hardPity">硬保底次数</param>
        /// <returns>是否成功</returns>
        public static bool SoftPityRandom(float baseChance, int failCount, int softPity, int hardPity)
        {
            if (failCount >= hardPity - 1) return true;

            float actualChance = baseChance;
            if (failCount >= softPity)
            {
                float progress = (float)(failCount - softPity) / (hardPity - softPity);
                actualChance = baseChance + (1f - baseChance) * progress * progress;
            }
            return Random.Range(0f, 1f) <= actualChance;
        }

        #endregion

        #region 分布随机

        /// <summary>
        /// 高斯/正态分布随机
        /// </summary>
        public static float GaussianRandom(float mean = 0f, float stdDev = 1f)
        {
            float u1 = 1f - Random.Range(0f, 1f);
            float u2 = 1f - Random.Range(0f, 1f);
            float randStdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        /// <summary>
        /// 在圆内随机一个点
        /// </summary>
        public static Vector2 RandomInCircle(float radius = 1f)
        {
            return Random.insideUnitCircle * radius;
        }

        /// <summary>
        /// 在圆周上随机一个点
        /// </summary>
        public static Vector2 RandomOnCircle(float radius = 1f)
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// 在球内随机一个点
        /// </summary>
        public static Vector3 RandomInSphere(float radius = 1f)
        {
            return Random.insideUnitSphere * radius;
        }

        /// <summary>
        /// 在球面上随机一个点
        /// </summary>
        public static Vector3 RandomOnSphere(float radius = 1f)
        {
            return Random.onUnitSphere * radius;
        }

        /// <summary>
        /// 随机 2D 方向（归一化向量）
        /// </summary>
        public static Vector2 RandomDirection2D()
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>
        /// 随机 3D 方向（归一化向量）
        /// </summary>
        public static Vector3 RandomDirection3D()
        {
            return Random.onUnitSphere;
        }

        /// <summary>
        /// 在扇形范围内随机方向
        /// </summary>
        public static Vector2 RandomInCone2D(Vector2 forward, float angle)
        {
            float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
            float randomAngle = Random.Range(-halfAngle, halfAngle);
            float cos = Mathf.Cos(randomAngle);
            float sin = Mathf.Sin(randomAngle);
            return new Vector2(
                forward.x * cos - forward.y * sin,
                forward.x * sin + forward.y * cos
            );
        }

        /// <summary>
        /// 在两个值之间随机（排除某个范围）
        /// </summary>
        public static float RandomExcluding(float min, float max, float excludeMin, float excludeMax)
        {
            float range1 = excludeMin - min;
            float range2 = max - excludeMax;
            float totalRange = range1 + range2;

            if (totalRange <= 0) return min;

            float r = Random.Range(0f, totalRange);
            return r < range1 ? min + r : excludeMax + (r - range1);
        }

        #endregion
    }
}
