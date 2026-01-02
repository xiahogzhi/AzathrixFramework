using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 数学扩展方法
    /// </summary>
    public static class MathExtensions
    {
        #region 取整

        /// <summary>
        /// 四舍五入取整（接近整数时直接取整）
        /// </summary>
        public static int RoundToInt(this float value)
        {
            if (Mathf.Abs(value - (int)value) <= 0.001f)
                return (int)value;
            return Mathf.RoundToInt(value);
        }

        /// <summary>
        /// 转换为整数（带微小偏移修正）
        /// </summary>
        public static int ToInt(this float value)
        {
            return value < 0 ? (int)(value - 0.001f) : (int)(value + 0.001f);
        }

        /// <summary>
        /// 向下取整（带微小偏移修正）
        /// </summary>
        public static int ToFloorInt(this float value)
        {
            return Mathf.FloorToInt(value + 0.001f);
        }

        /// <summary>
        /// 向上取整（接近整数时直接取整）
        /// </summary>
        public static int CeilToInt(this float value)
        {
            if (Mathf.Abs(value - (int)value) <= 0.001f)
                return (int)value;
            return Mathf.CeilToInt(value);
        }

        #endregion

        #region 归一化/限制

        /// <summary>
        /// 按公式 x/(x+y) 转为百分比，范围 -1 到 1
        /// </summary>
        public static float FormulaNormalize(this int x, float y = 100) => FormulaNormalize((float)x, y);

        /// <summary>
        /// 按公式 x/(x+y) 转为百分比，范围 -1 到 1
        /// </summary>
        public static float FormulaNormalize(this float x, float y = 100)
        {
            if (x == 0) return 0;
            if (x < 0)
            {
                x *= -1;
                return -(x / (x + y));
            }
            return x / (x + y);
        }

        /// <summary>
        /// 限制范围在 -1 到 1
        /// </summary>
        public static float Clamp11(this float x)
        {
            return Mathf.Clamp(x, -1f, 1f);
        }

        /// <summary>
        /// 值映射：将值从一个范围映射到另一个范围
        /// </summary>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
        }

        /// <summary>
        /// 近似比较
        /// </summary>
        public static bool Approximately(this float a, float b, float tolerance = 0.001f)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }

        #endregion

        #region 除法/倒数

        /// <summary>
        /// 安全除法（避免除零）
        /// </summary>
        public static int Div(this int a, int b, int defaultValue = 0)
        {
            return (b == 0 || a == 0) ? defaultValue : a / b;
        }

        /// <summary>
        /// 安全除法（避免除零）
        /// </summary>
        public static float Div(this float a, float b, float defaultValue = 0)
        {
            return (b == 0 || a == 0) ? defaultValue : a / b;
        }

        /// <summary>
        /// 获取倒数（速度转时间时使用）
        /// </summary>
        public static float GetReciprocal(this float scale)
        {
            return scale == 0 ? 0 : 1f / scale;
        }

        #endregion

        #region 文本转换

        /// <summary>
        /// 转换为百分比文本
        /// </summary>
        public static string ToPercentText(this float p)
        {
            return Mathf.FloorToInt(p * 100f).ToString();
        }

        /// <summary>
        /// 向上取整并转为文本
        /// </summary>
        public static string ToCeilText(this float b)
        {
            return Mathf.CeilToInt(b).ToString();
        }

        #endregion

        #region 循环/取模

        /// <summary>
        /// 循环值到指定范围（如角度 0-360）
        /// </summary>
        public static float Wrap(this float value, float min, float max)
        {
            float range = max - min;
            return range == 0 ? min : min + ((value - min) % range + range) % range;
        }

        /// <summary>
        /// 循环值到指定范围
        /// </summary>
        public static int Wrap(this int value, int min, int max)
        {
            int range = max - min;
            return range == 0 ? min : min + ((value - min) % range + range) % range;
        }

        /// <summary>
        /// 真正的取模（处理负数）
        /// </summary>
        public static int Mod(this int x, int m)
        {
            return (x % m + m) % m;
        }

        /// <summary>
        /// 真正的取模（处理负数）
        /// </summary>
        public static float Mod(this float x, float m)
        {
            return (x % m + m) % m;
        }

        #endregion

        #region 范围检查/插值

        /// <summary>
        /// 判断值是否在范围内（包含边界）
        /// </summary>
        public static bool IsBetween(this float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断值是否在范围内（包含边界）
        /// </summary>
        public static bool IsBetween(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// 平滑插值（0-1 范围内的平滑曲线）
        /// </summary>
        public static float SmoothStep(this float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 反向线性插值：计算 value 在 a 到 b 之间的比例
        /// </summary>
        public static float InverseLerp(this float value, float a, float b)
        {
            return Mathf.InverseLerp(a, b, value);
        }

        #endregion

    }
}
