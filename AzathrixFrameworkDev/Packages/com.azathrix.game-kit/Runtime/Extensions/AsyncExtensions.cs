using System;
using Cysharp.Threading.Tasks;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 异步扩展方法
    /// </summary>
    public static class AsyncExtensions
    {
        /// <summary>
        /// 延迟执行
        /// </summary>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="action">执行的动作</param>
        public static async UniTask PostAction(this float delay, Action action)
        {
            await UniTask.Delay(delay.ToTimeSpan());
            action?.Invoke();
        }

        /// <summary>
        /// 延迟执行（带返回值）
        /// </summary>
        public static async UniTask<T> PostAction<T>(this float delay, Func<T> func)
        {
            await UniTask.Delay(delay.ToTimeSpan());
            return func != null ? func() : default;
        }

        /// <summary>
        /// 将秒数转换为 TimeSpan
        /// </summary>
        public static TimeSpan ToTimeSpan(this float seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// 将秒数转换为 TimeSpan
        /// </summary>
        public static TimeSpan ToTimeSpan(this double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// 将毫秒转换为 TimeSpan
        /// </summary>
        public static TimeSpan ToTimeSpanMs(this int milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}