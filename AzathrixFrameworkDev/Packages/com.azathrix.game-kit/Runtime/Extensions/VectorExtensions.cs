using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 向量扩展方法
    /// </summary>
    public static class VectorExtensions
    {
        #region Vector2

        /// <summary>
        /// 旋转 Vector2
        /// </summary>
        /// <param name="dir">方向</param>
        /// <param name="angle">角度（度）</param>
        public static Vector2 Rotate(this Vector2 dir, float angle)
        {
            angle *= Mathf.Deg2Rad;
            return new Vector2(
                Mathf.Cos(angle) * dir.x + Mathf.Sin(angle) * dir.y,
                -Mathf.Sin(angle) * dir.x + Mathf.Cos(angle) * dir.y
            );
        }

        /// <summary>
        /// 将方向转换为四方向（上下左右）
        /// </summary>
        public static Vector2Int ToHardDirection(this Vector2 dir)
        {
            float up = dir.y > 0 ? dir.y : 0;
            float down = dir.y < 0 ? -dir.y : 0;
            float right = dir.x > 0 ? dir.x : 0;
            float left = dir.x < 0 ? -dir.x : 0;

            if (up > down && up > left - 0.2f && up > right - 0.2f)
                return Vector2Int.up;
            if (down > up && down > left - 0.2f && down > right - 0.2f)
                return Vector2Int.down;
            if (left > up && left > down && left > right)
                return Vector2Int.left;
            if (right > up && right > down && right > left)
                return Vector2Int.right;

            return Vector2Int.zero;
        }

        /// <summary>
        /// 判断值是否在范围内（x为最小值，y为最大值）
        /// </summary>
        public static bool Contains(this Vector2 range, float value)
        {
            return value >= range.x && value <= range.y;
        }

        /// <summary>
        /// 修改 X 分量
        /// </summary>
        public static Vector2 WithX(this Vector2 v, float x) => new(x, v.y);

        /// <summary>
        /// 修改 Y 分量
        /// </summary>
        public static Vector2 WithY(this Vector2 v, float y) => new(v.x, y);

        /// <summary>
        /// 转换为 Vector3（Z = 0）
        /// </summary>
        public static Vector3 ToVector3(this Vector2 v) => new(v.x, v.y, 0);

        /// <summary>
        /// 转换为 Vector3（指定 Z）
        /// </summary>
        public static Vector3 ToVector3(this Vector2 v, float z) => new(v.x, v.y, z);

        /// <summary>
        /// 绝对值
        /// </summary>
        public static Vector2 Abs(this Vector2 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y));

        /// <summary>
        /// 符号
        /// </summary>
        public static Vector2 Sign(this Vector2 v) => new(Mathf.Sign(v.x), Mathf.Sign(v.y));

        /// <summary>
        /// 计算从当前点到目标点的方向（归一化）
        /// </summary>
        public static Vector2 DirectionTo(this Vector2 from, Vector2 to) => (to - from).normalized;

        /// <summary>
        /// 计算到目标点的距离
        /// </summary>
        public static float DistanceTo(this Vector2 from, Vector2 to) => Vector2.Distance(from, to);

        /// <summary>
        /// 计算两点的中点
        /// </summary>
        public static Vector2 MidPoint(this Vector2 a, Vector2 b) => (a + b) * 0.5f;

        /// <summary>
        /// 限制向量长度
        /// </summary>
        public static Vector2 ClampMagnitude(this Vector2 v, float maxLength) => Vector2.ClampMagnitude(v, maxLength);

        #endregion

        #region Vector3

        /// <summary>
        /// 绕 X 轴旋转
        /// </summary>
        public static Vector3 RotateX(this Vector3 dir, float angle)
        {
            angle *= Mathf.Deg2Rad;
            return new Vector3(
                dir.x,
                dir.y * Mathf.Cos(angle) - dir.z * Mathf.Sin(angle),
                dir.y * Mathf.Sin(angle) + dir.z * Mathf.Cos(angle)
            );
        }

        /// <summary>
        /// 绕 Y 轴旋转
        /// </summary>
        public static Vector3 RotateY(this Vector3 dir, float angle)
        {
            angle *= Mathf.Deg2Rad;
            return new Vector3(
                dir.x * Mathf.Cos(angle) + dir.z * Mathf.Sin(angle),
                dir.y,
                -dir.x * Mathf.Sin(angle) + dir.z * Mathf.Cos(angle)
            );
        }

        /// <summary>
        /// 绕 Z 轴旋转
        /// </summary>
        public static Vector3 RotateZ(this Vector3 dir, float angle)
        {
            angle *= Mathf.Deg2Rad;
            return new Vector3(
                dir.x * Mathf.Cos(angle) - dir.y * Mathf.Sin(angle),
                dir.x * Mathf.Sin(angle) + dir.y * Mathf.Cos(angle),
                dir.z
            );
        }

        /// <summary>
        /// 修改 X 分量
        /// </summary>
        public static Vector3 WithX(this Vector3 v, float x) => new(x, v.y, v.z);

        /// <summary>
        /// 修改 Y 分量
        /// </summary>
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        /// <summary>
        /// 修改 Z 分量
        /// </summary>
        public static Vector3 WithZ(this Vector3 v, float z) => new(v.x, v.y, z);

        /// <summary>
        /// 转换为 Vector2（丢弃 Z）
        /// </summary>
        public static Vector2 ToVector2(this Vector3 v) => new(v.x, v.y);

        /// <summary>
        /// 转换为 XZ 平面的 Vector2
        /// </summary>
        public static Vector2 ToVector2XZ(this Vector3 v) => new(v.x, v.z);

        /// <summary>
        /// 展平为 XZ 平面（Y = 0）
        /// </summary>
        public static Vector3 Flatten(this Vector3 v) => new(v.x, 0, v.z);

        /// <summary>
        /// 展平为 XZ 平面（指定 Y）
        /// </summary>
        public static Vector3 Flatten(this Vector3 v, float y) => new(v.x, y, v.z);

        /// <summary>
        /// 绝对值
        /// </summary>
        public static Vector3 Abs(this Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// 符号
        /// </summary>
        public static Vector3 Sign(this Vector3 v) => new(Mathf.Sign(v.x), Mathf.Sign(v.y), Mathf.Sign(v.z));

        /// <summary>
        /// 计算从当前点到目标点的方向（归一化）
        /// </summary>
        public static Vector3 DirectionTo(this Vector3 from, Vector3 to) => (to - from).normalized;

        /// <summary>
        /// 计算到目标点的距离
        /// </summary>
        public static float DistanceTo(this Vector3 from, Vector3 to) => Vector3.Distance(from, to);

        /// <summary>
        /// 计算两点的中点
        /// </summary>
        public static Vector3 MidPoint(this Vector3 a, Vector3 b) => (a + b) * 0.5f;

        /// <summary>
        /// 限制向量长度
        /// </summary>
        public static Vector3 ClampMagnitude(this Vector3 v, float maxLength) => Vector3.ClampMagnitude(v, maxLength);

        #endregion
    }
}
