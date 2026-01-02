using UnityEngine;

namespace Azathrix.GameKit.Runtime.Utils
{
    /// <summary>
    /// 数学工具类
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// 抛物线插值
        /// </summary>
        /// <param name="start">起点</param>
        /// <param name="end">终点</param>
        /// <param name="height">最高点高度</param>
        /// <param name="step">进度 0-1</param>
        public static Vector2 Parabola(Vector2 start, Vector2 end, float height, float step)
        {
            float Func(float x) => 4 * (-height * x * x + height * x);
            var mid = Vector2.Lerp(start, end, step);
            return new Vector2(mid.x, Func(step) + Mathf.Lerp(start.y, end.y, step));
        }

        /// <summary>
        /// 二阶贝塞尔曲线
        /// </summary>
        /// <param name="points">控制点（至少3个）</param>
        /// <param name="t">进度 0-1</param>
        public static Vector2 QuadraticBezier(Vector2[] points, float t)
        {
            if (points.Length < 3) return Vector2.zero;
            Vector2 a = points[0];
            Vector2 b = points[1];
            Vector2 c = points[2];
            Vector3 aa = a + (b - a) * t;
            Vector3 bb = b + (c - b) * t;
            return aa + (bb - aa) * t;
        }

        /// <summary>
        /// 三阶贝塞尔曲线
        /// </summary>
        public static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// 计算两个向量之间的角度（度）
        /// </summary>
        public static float AngleBetween(Vector2 from, Vector2 to)
        {
            return Vector2.Angle(from, to);
        }

        /// <summary>
        /// 计算两个向量之间的有符号角度（度）
        /// </summary>
        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            return Vector2.SignedAngle(from, to);
        }

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <returns>是否相交</returns>
        public static bool LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
        {
            intersection = Vector2.zero;

            float d = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
            if (Mathf.Abs(d) < 0.0001f)
                return false;

            float t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / d;
            float u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / d;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                intersection = new Vector2(p1.x + t * (p2.x - p1.x), p1.y + t * (p2.y - p1.y));
                return true;
            }
            return false;
        }

        /// <summary>
        /// 计算点到线段的最近点
        /// </summary>
        public static Vector2 ClosestPointOnLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float len = line.magnitude;
            line.Normalize();

            float d = Vector2.Dot(point - lineStart, line);
            d = Mathf.Clamp(d, 0, len);

            return lineStart + line * d;
        }
    }
}