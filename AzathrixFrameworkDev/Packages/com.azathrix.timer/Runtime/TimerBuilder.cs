using System;
using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.Timer.Runtime
{
    /// <summary>
    /// 定时器构建器
    /// <para>继承自 BuilderBase，提供链式调用配置定时器</para>
    /// <para>通过 TimerManager.GetBuilder() 获取实例</para>
    /// </summary>
    /// <remarks>
    /// <para>构建器使用对象池管理，Build() 后自动回收</para>
    /// <para>支持保存配置并复用：BuildContext() 导出配置，Get(context) 复用配置</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var timerManager = AzathrixFramework.GetSystem&lt;TimerManager&gt;();
    ///
    /// // 基础用法 - 延迟执行
    /// timerManager.GetBuilder()
    ///     .SetDuration(2f)
    ///     .SetOnComplete(() => Debug.Log("完成"))
    ///     .Build();
    ///
    /// // 重复执行
    /// timerManager.GetBuilder()
    ///     .SetInterval(0.5f)          // 设置间隔，自动启用重复模式
    ///     .SetRepeat(10)              // 重复10次
    ///     .SetOnComplete(() => Debug.Log("每次执行"))
    ///     .Build();
    ///
    /// // 带进度的定时器
    /// timerManager.GetBuilder()
    ///     .SetDuration(3f)
    ///     .SetOnUpdate(p => slider.value = p)  // 每帧回调进度 (0-1)
    ///     .SetOnComplete(() => Debug.Log("完成"))
    ///     .UseRealTime()              // 不受 TimeScale 影响
    ///     .BindTo(gameObject)         // 绑定到 GameObject，销毁时自动取消
    ///     .Build();
    ///
    /// // 帧计数模式
    /// timerManager.GetBuilder()
    ///     .SetFrames(60)              // 60帧后执行
    ///     .SetOnComplete(() => Debug.Log("60帧后"))
    ///     .Build();
    ///
    /// // 帧重复
    /// timerManager.GetBuilder()
    ///     .SetFrameRepeat(30, 5)      // 每30帧执行一次，共5次
    ///     .SetOnComplete(() => Debug.Log("执行"))
    ///     .Build();
    ///
    /// // 条件等待
    /// timerManager.GetBuilder()
    ///     .WaitUntil(() => player.IsReady)
    ///     .SetTimeout(10f)            // 超时时间
    ///     .SetOnComplete(() => Debug.Log("准备好了"))
    ///     .Build();
    ///
    /// // 保存配置复用
    /// var config = timerManager.GetBuilder()
    ///     .SetDuration(1f)
    ///     .UseRealTime()
    ///     .BuildContext();            // 导出配置，不创建定时器
    ///
    /// // 使用保存的配置
    /// timerManager.GetBuilder(config)
    ///     .SetOnComplete(() => Debug.Log("使用配置1"))
    ///     .Build();
    ///
    /// timerManager.GetBuilder(config)
    ///     .SetOnComplete(() => Debug.Log("使用配置2"))
    ///     .Build();
    /// </code>
    /// </example>
    public class TimerBuilder : BuilderBase<TimerBuilder, Timer, TimerBuildContext>
    {
        private TimerManager _manager;

        /// <summary>
        /// 从管理器获取构建器（内部使用）
        /// </summary>
        /// <param name="manager">定时器管理器</param>
        /// <returns>构建器实例</returns>
        public static TimerBuilder Get(TimerManager manager)
        {
            var builder = Get();
            builder._manager = manager;
            return builder;
        }

        /// <summary>
        /// 从管理器和已有配置获取构建器（内部使用）
        /// </summary>
        /// <param name="manager">定时器管理器</param>
        /// <param name="context">已保存的配置</param>
        /// <returns>构建器实例</returns>
        public static TimerBuilder Get(TimerManager manager, TimerBuildContext context)
        {
            var builder = Get(context);
            builder._manager = manager;
            return builder;
        }

        /// <summary>
        /// 设置持续时间
        /// </summary>
        /// <param name="duration">持续时间（秒）</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetDuration(float duration)
        {
            CheckIfDispose();
            Context.Duration = duration;
            Context.Interval = duration;
            return this;
        }

        /// <summary>
        /// 设置重复间隔
        /// <para>调用此方法会自动启用重复模式</para>
        /// </summary>
        /// <param name="interval">间隔时间（秒）</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetInterval(float interval)
        {
            CheckIfDispose();
            Context.Interval = interval;
            Context.Duration = interval;
            Context.IsRepeat = true;
            return this;
        }

        /// <summary>
        /// 设置重复次数
        /// </summary>
        /// <param name="count">重复次数，-1 表示无限重复</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetRepeat(int count = -1)
        {
            CheckIfDispose();
            Context.RepeatCount = count;
            Context.IsRepeat = true;
            return this;
        }

        /// <summary>
        /// 设置帧数（帧计数模式）
        /// <para>调用此方法会启用帧计数模式，按帧数而非时间计算</para>
        /// </summary>
        /// <param name="frames">帧数</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetFrames(int frames)
        {
            CheckIfDispose();
            Context.FrameCount = Mathf.Max(1, frames);
            Context.IsFrameMode = true;
            return this;
        }

        /// <summary>
        /// 设置帧重复
        /// <para>每隔指定帧数重复执行</para>
        /// </summary>
        /// <param name="frames">间隔帧数</param>
        /// <param name="repeatCount">重复次数，-1 表示无限重复</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetFrameRepeat(int frames, int repeatCount = -1)
        {
            CheckIfDispose();
            Context.FrameCount = Mathf.Max(1, frames);
            Context.IsFrameMode = true;
            Context.IsRepeat = true;
            Context.RepeatCount = repeatCount;
            return this;
        }

        /// <summary>
        /// 使用真实时间
        /// <para>不受 Time.timeScale 影响，游戏暂停时也会继续计时</para>
        /// </summary>
        /// <param name="use">是否使用真实时间</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder UseRealTime(bool use = true)
        {
            CheckIfDispose();
            Context.UseRealTime = use;
            return this;
        }

        /// <summary>
        /// 设置条件等待
        /// <para>每帧检查条件，条件返回 true 时触发完成回调</para>
        /// </summary>
        /// <param name="condition">条件判断函数</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder WaitUntil(Func<bool> condition)
        {
            CheckIfDispose();
            Context.Condition = condition;
            return this;
        }

        /// <summary>
        /// 设置条件等待（等待条件为 false）
        /// <para>与 WaitUntil 相反，条件返回 false 时触发</para>
        /// </summary>
        /// <param name="condition">条件判断函数</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder WaitWhile(Func<bool> condition)
        {
            CheckIfDispose();
            Context.Condition = () => !condition();
            return this;
        }

        /// <summary>
        /// 设置超时时间
        /// <para>用于条件等待模式，超时后定时器自动停止（不触发回调）</para>
        /// </summary>
        /// <param name="timeout">超时时间（秒），0 表示无超时</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetTimeout(float timeout)
        {
            CheckIfDispose();
            Context.Timeout = timeout;
            return this;
        }

        /// <summary>
        /// 设置完成回调
        /// <para>定时器完成时（或每次重复执行时）调用</para>
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetOnComplete(Action callback)
        {
            CheckIfDispose();
            Context.OnComplete = callback;
            return this;
        }

        /// <summary>
        /// 设置进度回调
        /// <para>每帧调用，参数为当前进度 (0-1)</para>
        /// </summary>
        /// <param name="callback">回调函数，参数为进度值</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder SetOnUpdate(Action<float> callback)
        {
            CheckIfDispose();
            Context.OnUpdate = callback;
            return this;
        }

        /// <summary>
        /// 绑定到 GameObject
        /// <para>当 GameObject 销毁时，定时器自动取消</para>
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder BindTo(GameObject go)
        {
            CheckIfDispose();
            Context.BindTarget = go;
            return this;
        }

        /// <summary>
        /// 绑定到 Component 所属的 GameObject
        /// </summary>
        /// <param name="component">目标组件</param>
        /// <returns>构建器实例（链式调用）</returns>
        public TimerBuilder BindTo(Component component)
        {
            CheckIfDispose();
            Context.BindTarget = component?.gameObject;
            return this;
        }

        /// <summary>
        /// 构建定时器（由 BuilderBase 调用）
        /// </summary>
        protected override Timer OnBuild()
        {
            return _manager.CreateInternal(Context);
        }

        /// <summary>
        /// 清理资源（由 BuilderBase 调用）
        /// </summary>
        protected override void OnDispose()
        {
            _manager = null;
            base.OnDispose();
        }
    }
}
