using System;

namespace Azathrix.GameKit.Runtime.Utils
{
    /// <summary>
    /// 时间工具类
    /// </summary>
    public static class TimeUtils
    {
        /// <summary>
        /// 格式化时间（秒转为 mm:ss 格式）
        /// </summary>
        public static string FormatTime(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// 格式化时间（秒转为 hh:mm:ss 格式）
        /// </summary>
        public static string FormatTimeHMS(float seconds)
        {
            int hours = (int)(seconds / 3600);
            int mins = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);
            return $"{hours:D2}:{mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// 格式化 TimeSpan
        /// </summary>
        public static string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
            return $"{span.Minutes:D2}:{span.Seconds:D2}";
        }

        /// <summary>
        /// 格式化倒计时（带毫秒）
        /// </summary>
        public static string FormatCountdown(float remainingSeconds)
        {
            if (remainingSeconds <= 0)
                return "00:00";

            int mins = (int)(remainingSeconds / 60);
            int secs = (int)(remainingSeconds % 60);
            int ms = (int)((remainingSeconds % 1) * 100);

            if (mins > 0)
                return $"{mins:D2}:{secs:D2}";
            return $"{secs:D2}.{ms:D2}";
        }

        /// <summary>
        /// 格式化为友好时间（如 "3分钟前"）
        /// </summary>
        public static string FormatRelativeTime(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;

            if (span.TotalSeconds < 60)
                return "刚刚";
            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours}小时前";
            if (span.TotalDays < 30)
                return $"{(int)span.TotalDays}天前";
            if (span.TotalDays < 365)
                return $"{(int)(span.TotalDays / 30)}个月前";
            return $"{(int)(span.TotalDays / 365)}年前";
        }

        /// <summary>
        /// 获取今天的开始时间
        /// </summary>
        public static DateTime GetTodayStart()
        {
            return DateTime.Today;
        }

        /// <summary>
        /// 获取今天的结束时间
        /// </summary>
        public static DateTime GetTodayEnd()
        {
            return DateTime.Today.AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// 判断是否是同一天
        /// </summary>
        public static bool IsSameDay(DateTime a, DateTime b)
        {
            return a.Date == b.Date;
        }
    }
}
