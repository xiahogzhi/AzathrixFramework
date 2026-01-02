using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 颜色扩展方法
    /// </summary>
    public static class ColorExtensions
    {
        #region 通道修改

        /// <summary>
        /// 修改 Alpha 通道
        /// </summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>
        /// 修改 Alpha 通道（别名）
        /// </summary>
        public static Color SetAlpha(this Color color, float alpha) => WithAlpha(color, alpha);

        /// <summary>
        /// 修改 R 通道
        /// </summary>
        public static Color WithR(this Color color, float r)
        {
            return new Color(r, color.g, color.b, color.a);
        }

        /// <summary>
        /// 修改 G 通道
        /// </summary>
        public static Color WithG(this Color color, float g)
        {
            return new Color(color.r, g, color.b, color.a);
        }

        /// <summary>
        /// 修改 B 通道
        /// </summary>
        public static Color WithB(this Color color, float b)
        {
            return new Color(color.r, color.g, b, color.a);
        }

        /// <summary>
        /// 反色
        /// </summary>
        public static Color Invert(this Color color)
        {
            return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
        }

        #endregion

        #region Hex 转换

        /// <summary>
        /// 将 int32 转换为 RGBA 颜色
        /// </summary>
        public static Color HexToRgba(this int hex)
        {
            float r = ((hex >> 24) & 0xFF) / 255f;
            float g = ((hex >> 16) & 0xFF) / 255f;
            float b = ((hex >> 8) & 0xFF) / 255f;
            float a = (hex & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// 将 RGBA 颜色转换为 int32
        /// </summary>
        public static int RgbaToHex(this Color col)
        {
            int r = (int)(col.r * 255f) << 24;
            int g = (int)(col.g * 255f) << 16;
            int b = (int)(col.b * 255f) << 8;
            int a = (int)(col.a * 255f);
            return r | g | b | a;
        }

        /// <summary>
        /// 将 int32 转换为 RGB 颜色（Alpha = 1）
        /// </summary>
        public static Color HexToRGB(this int hex)
        {
            float r = ((hex >> 16) & 0xFF) / 255f;
            float g = ((hex >> 8) & 0xFF) / 255f;
            float b = (hex & 0xFF) / 255f;
            return new Color(r, g, b, 1);
        }

        /// <summary>
        /// 将 RGB 颜色转换为 int32
        /// </summary>
        public static int RGBToHex(this Color col)
        {
            int r = (int)(col.r * 255f) << 16;
            int g = (int)(col.g * 255f) << 8;
            int b = (int)(col.b * 255f);
            return r | g | b;
        }

        #endregion

        #region HTML 字符串

        /// <summary>
        /// 转换为 HTML RGB 字符串（如 "FF0000"）
        /// </summary>
        public static string ToHtmlStringRGB(this Color col)
        {
            return ColorUtility.ToHtmlStringRGB(col);
        }

        /// <summary>
        /// 转换为 HTML RGBA 字符串（如 "FF0000FF"）
        /// </summary>
        public static string ToHtmlStringRGBA(this Color col)
        {
            return ColorUtility.ToHtmlStringRGBA(col);
        }

        /// <summary>
        /// 从 HTML 字符串解析颜色
        /// </summary>
        public static Color FromHtmlString(this string htmlColor)
        {
            if (!htmlColor.StartsWith("#"))
                htmlColor = "#" + htmlColor;
            ColorUtility.TryParseHtmlString(htmlColor, out var color);
            return color;
        }

        #endregion

        #region 颜色调整

        /// <summary>
        /// 颜色插值
        /// </summary>
        public static Color Lerp(this Color from, Color to, float t)
        {
            return Color.Lerp(from, to, t);
        }

        /// <summary>
        /// 提亮颜色
        /// </summary>
        public static Color Brighten(this Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a
            );
        }

        /// <summary>
        /// 变暗颜色
        /// </summary>
        public static Color Darken(this Color color, float amount)
        {
            return color.Brighten(-amount);
        }

        /// <summary>
        /// 转换为灰度
        /// </summary>
        public static Color ToGrayscale(this Color color)
        {
            float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(gray, gray, gray, color.a);
        }

        /// <summary>
        /// 调整饱和度
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <param name="saturation">饱和度 (0=灰度, 1=原色)</param>
        public static Color WithSaturation(this Color color, float saturation)
        {
            Color gray = color.ToGrayscale();
            return Color.Lerp(gray, color, saturation);
        }

        #endregion
    }
}
