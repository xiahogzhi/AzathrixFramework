using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// Bounds/Rect 扩展方法
    /// </summary>
    public static class BoundsExtensions
    {
        #region Rect 转换

        /// <summary>
        /// 将本地 Rect 转换为世界坐标
        /// </summary>
        public static Rect ToWorld(this Rect localRect, GameObject go)
        {
            var r = localRect;
            r.position += (Vector2)go.transform.position;
            return r;
        }

        /// <summary>
        /// 将本地 Rect 转换为世界坐标
        /// </summary>
        public static Rect ToWorld(this Rect localRect, MonoBehaviour mo)
        {
            if (mo == null) return localRect;
            var r = localRect;
            r.position += (Vector2)mo.transform.position;
            return r;
        }

        /// <summary>
        /// 将 Rect 转换为 Bounds（XZ 平面）
        /// </summary>
        public static Bounds ToLocalBounds(this Rect s)
        {
            return new Bounds(
                new Vector3(s.center.x, 0, s.center.y),
                new Vector3(s.size.x, 10, s.size.y)
            );
        }

        #endregion

        #region BoxCollider

        /// <summary>
        /// 设置 BoxCollider 的本地 Bounds
        /// </summary>
        public static void SetLocalBounds(this BoxCollider s, Bounds bounds)
        {
            s.center = bounds.center;
            s.size = bounds.size;
        }

        /// <summary>
        /// 获取 BoxCollider 的本地 Bounds
        /// </summary>
        public static Bounds GetLocalBounds(this BoxCollider s)
        {
            return new Bounds(s.center, s.size);
        }

        #endregion

        #region 调试绘制

        /// <summary>
        /// 绘制 Rect（仅编辑器）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void DrawRect(this Rect rect, Color color, float duration = 0)
        {
            Vector3 topLeft = new Vector3(rect.xMin, rect.yMin, 0);
            Vector3 topRight = new Vector3(rect.xMax, rect.yMin, 0);
            Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMax, 0);
            Vector3 bottomRight = new Vector3(rect.xMax, rect.yMax, 0);

            if (duration > 0)
            {
                Debug.DrawLine(topLeft, topRight, color, duration);
                Debug.DrawLine(topRight, bottomRight, color, duration);
                Debug.DrawLine(bottomRight, bottomLeft, color, duration);
                Debug.DrawLine(bottomLeft, topLeft, color, duration);
            }
            else
            {
                Debug.DrawLine(topLeft, topRight, color);
                Debug.DrawLine(topRight, bottomRight, color);
                Debug.DrawLine(bottomRight, bottomLeft, color);
                Debug.DrawLine(bottomLeft, topLeft, color);
            }
        }

        /// <summary>
        /// 绘制 Bounds（仅编辑器）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void DrawBounds(this Bounds bounds, Color color, float duration = 0)
        {
            var min = bounds.min;
            var max = bounds.max;

            // 底面
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), color, duration);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), color, duration);

            // 顶面
            Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), color, duration);
            Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), color, duration);

            // 竖边
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color, duration);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), color, duration);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), color, duration);
        }

        #endregion

        #region 碰撞计算

        /// <summary>
        /// 计算碰撞点的角度
        /// </summary>
        public static float CalcHitPointAngle(this Bounds bounds, Vector3 hitPoint)
        {
            var dir = (hitPoint - bounds.center).normalized;
            return Vector2.SignedAngle(dir, Vector2.up);
        }

        /// <summary>
        /// 计算与目标 Bounds 之间的碰撞点
        /// </summary>
        /// <param name="bounds">选中对象的 Bounds</param>
        /// <param name="targetBounds">目标对象的 Bounds</param>
        /// <param name="tendencyX">X 方向趋势 (-1 到 1)</param>
        /// <param name="tendencyY">Y 方向趋势 (-1 到 1)</param>
        public static Vector3 CalcHitPoint(this Bounds bounds, Bounds targetBounds, int tendencyX = 0, int tendencyY = 0)
        {
            Vector3 result = Vector3.zero;
            Vector2 tendency = new Vector2(tendencyX + 1f, tendencyY + 1f) * 0.5f;

            // 计算 X
            Vector2 selectRangeWidth = new Vector2(bounds.min.x, bounds.max.x);
            Vector2 targetRangeWidth = new Vector2(targetBounds.min.x, targetBounds.max.x);
            Vector2 width = CalcOverlapRange(selectRangeWidth, targetRangeWidth);
            result.x = Mathf.Lerp(width.x, width.y, tendency.x);

            // 计算 Y
            Vector2 selectRangeHeight = new Vector2(bounds.min.y, bounds.max.y);
            Vector2 targetRangeHeight = new Vector2(targetBounds.min.y, targetBounds.max.y);
            Vector2 height = CalcOverlapRange(selectRangeHeight, targetRangeHeight);
            result.y = Mathf.Lerp(height.x, height.y, tendency.y);

            // 计算 Z
            Vector2 targetRangeThickness = new Vector2(targetBounds.min.z, targetBounds.max.z);
            result.z = Mathf.Clamp(bounds.center.z, targetRangeThickness.x, targetRangeThickness.y);

            return result;
        }

        private static Vector2 CalcOverlapRange(Vector2 selectRange, Vector2 targetRange)
        {
            // 目标完全包围自己
            if (targetRange.x <= selectRange.x && targetRange.y >= selectRange.y)
                return selectRange;
            // 自己包围目标
            if (selectRange.x <= targetRange.x && selectRange.y >= targetRange.y)
                return targetRange;
            // 左侧重叠
            if (targetRange.x >= selectRange.x && targetRange.y >= selectRange.y)
                return new Vector2(targetRange.x, selectRange.y);
            // 右侧重叠
            if (targetRange.x <= selectRange.x && targetRange.y <= selectRange.y)
                return new Vector2(selectRange.x, targetRange.y);

            return selectRange;
        }

        #endregion
    }
}