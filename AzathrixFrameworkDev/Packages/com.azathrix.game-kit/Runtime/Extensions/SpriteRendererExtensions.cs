using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// SpriteRenderer 扩展方法
    /// </summary>
    public static class SpriteRendererExtensions
    {
        #region 颜色

        /// <summary>
        /// 设置透明度
        /// </summary>
        public static void SetAlpha(this SpriteRenderer sr, float alpha)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        /// <summary>
        /// 设置颜色（保持透明度）
        /// </summary>
        public static void SetColorKeepAlpha(this SpriteRenderer sr, Color color)
        {
            if (sr == null) return;
            color.a = sr.color.a;
            sr.color = color;
        }

        /// <summary>
        /// 设置颜色 RGB（保持透明度）
        /// </summary>
        public static void SetRGB(this SpriteRenderer sr, float r, float g, float b)
        {
            if (sr == null) return;
            sr.color = new Color(r, g, b, sr.color.a);
        }

        #endregion

        #region 淡入淡出

        /// <summary>
        /// 淡入
        /// </summary>
        public static async UniTask FadeIn(this SpriteRenderer sr, float duration = 1f)
        {
            if (sr == null) return;
            await FadeTo(sr, 1f, duration);
        }

        /// <summary>
        /// 淡出
        /// </summary>
        public static async UniTask FadeOut(this SpriteRenderer sr, float duration = 1f)
        {
            if (sr == null) return;
            await FadeTo(sr, 0f, duration);
        }

        /// <summary>
        /// 淡入淡出到指定透明度
        /// </summary>
        public static async UniTask FadeTo(this SpriteRenderer sr, float targetAlpha, float duration)
        {
            if (sr == null) return;

            float startAlpha = sr.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                sr.SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration));
                await UniTask.Yield();
            }
            sr.SetAlpha(targetAlpha);
        }

        #endregion

        #region 翻转

        /// <summary>
        /// 设置水平翻转
        /// </summary>
        public static void SetFlipX(this SpriteRenderer sr, bool flip)
        {
            if (sr != null) sr.flipX = flip;
        }

        /// <summary>
        /// 设置垂直翻转
        /// </summary>
        public static void SetFlipY(this SpriteRenderer sr, bool flip)
        {
            if (sr != null) sr.flipY = flip;
        }

        /// <summary>
        /// 切换水平翻转
        /// </summary>
        public static void ToggleFlipX(this SpriteRenderer sr)
        {
            if (sr != null) sr.flipX = !sr.flipX;
        }

        /// <summary>
        /// 根据方向设置翻转（正数向右，负数向左）
        /// </summary>
        public static void SetFlipByDirection(this SpriteRenderer sr, float direction)
        {
            if (sr != null && direction != 0)
                sr.flipX = direction < 0;
        }

        #endregion

        #region 尺寸

        /// <summary>
        /// 获取 Sprite 世界尺寸
        /// </summary>
        public static Vector2 GetWorldSize(this SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return Vector2.zero;
            return sr.bounds.size;
        }

        /// <summary>
        /// 设置排序层
        /// </summary>
        public static void SetSortingLayer(this SpriteRenderer sr, string layerName)
        {
            if (sr != null) sr.sortingLayerName = layerName;
        }

        /// <summary>
        /// 设置排序顺序
        /// </summary>
        public static void SetSortingOrder(this SpriteRenderer sr, int order)
        {
            if (sr != null) sr.sortingOrder = order;
        }

        #endregion
    }
}
