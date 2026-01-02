using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// RectTransform 扩展方法
    /// </summary>
    public static class RectTransformExtensions
    {
        #region 锚点预设

        /// <summary>
        /// 设置为拉伸填充父对象
        /// </summary>
        public static void SetStretchFill(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 设置锚点到中心
        /// </summary>
        public static void SetAnchorCenter(this RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 设置锚点到左下角
        /// </summary>
        public static void SetAnchorBottomLeft(this RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
        }

        /// <summary>
        /// 设置锚点到右上角
        /// </summary>
        public static void SetAnchorTopRight(this RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = Vector2.one;
        }

        #endregion

        #region 尺寸

        /// <summary>
        /// 设置宽度
        /// </summary>
        public static void SetWidth(this RectTransform rt, float width)
        {
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        }

        /// <summary>
        /// 设置高度
        /// </summary>
        public static void SetHeight(this RectTransform rt, float height)
        {
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }

        /// <summary>
        /// 设置尺寸
        /// </summary>
        public static void SetSize(this RectTransform rt, Vector2 size)
        {
            rt.sizeDelta = size;
        }

        /// <summary>
        /// 设置尺寸
        /// </summary>
        public static void SetSize(this RectTransform rt, float width, float height)
        {
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// 获取世界空间尺寸
        /// </summary>
        public static Vector2 GetWorldSize(this RectTransform rt)
        {
            return new Vector2(rt.rect.width * rt.lossyScale.x, rt.rect.height * rt.lossyScale.y);
        }

        #endregion

        #region 位置

        /// <summary>
        /// 设置锚点位置 X
        /// </summary>
        public static void SetAnchoredPositionX(this RectTransform rt, float x)
        {
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        /// <summary>
        /// 设置锚点位置 Y
        /// </summary>
        public static void SetAnchoredPositionY(this RectTransform rt, float y)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        /// <summary>
        /// 设置 Pivot
        /// </summary>
        public static void SetPivot(this RectTransform rt, Vector2 pivot)
        {
            rt.pivot = pivot;
        }

        /// <summary>
        /// 设置 Pivot（保持位置不变）
        /// </summary>
        public static void SetPivotWithoutMoving(this RectTransform rt, Vector2 pivot)
        {
            var offset = pivot - rt.pivot;
            offset.Scale(rt.rect.size);
            rt.pivot = pivot;
            rt.anchoredPosition += offset;
        }

        #endregion

        #region 边距

        /// <summary>
        /// 设置左边距
        /// </summary>
        public static void SetLeft(this RectTransform rt, float left)
        {
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
        }

        /// <summary>
        /// 设置右边距
        /// </summary>
        public static void SetRight(this RectTransform rt, float right)
        {
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
        }

        /// <summary>
        /// 设置上边距
        /// </summary>
        public static void SetTop(this RectTransform rt, float top)
        {
            rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
        }

        /// <summary>
        /// 设置下边距
        /// </summary>
        public static void SetBottom(this RectTransform rt, float bottom)
        {
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置 RectTransform
        /// </summary>
        public static void Reset(this RectTransform rt)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        #endregion
    }
}
