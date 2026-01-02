using System;
using System.Collections.Generic;
using Azathrix.Framework.Core.Attributes;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Interfaces.SystemEvents;
using Azathrix.GameKit.Runtime.Extensions;
using UnityEngine;

namespace Azathrix.Timer.Runtime
{
    /// <summary>
    /// 定时器管理器
    /// <para>统一管理所有定时器的创建、更新和销毁</para>
    /// <para>作为框架系统自动注册，通过 AzathrixFramework.GetSystem&lt;TimerManager&gt;() 获取实例</para>
    /// </summary>
    /// <remarks>
    /// <para>支持以下定时器类型：</para>
    /// <list type="bullet">
    /// <item><description>延迟执行 - 指定时间后执行一次</description></item>
    /// <item><description>重复执行 - 按间隔重复执行指定次数或无限次</description></item>
    /// <item><description>帧延迟 - 指定帧数后执行</description></item>
    /// <item><description>条件等待 - 等待条件满足后执行</description></item>
    /// <item><description>进度回调 - 每帧回调当前进度</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 获取管理器实例
    /// var timerManager = AzathrixFramework.GetSystem&lt;TimerManager&gt;();
    ///
    /// // 1. 延迟执行
    /// timerManager.Delay(2f, () => Debug.Log("2秒后执行"));
    ///
    /// // 2. 重复执行（每0.5秒执行一次，共10次）
    /// timerManager.Repeat(0.5f, () => Debug.Log("重复执行"), 10);
    ///
    /// // 3. 无限重复（-1表示无限）
    /// var timer = timerManager.Repeat(1f, () => Debug.Log("每秒执行"));
    /// // 需要时手动取消
    /// timer.Cancel();
    ///
    /// // 4. 帧延迟
    /// timerManager.DelayFrames(60, () => Debug.Log("60帧后执行"));
    ///
    /// // 5. 条件等待
    /// timerManager.WaitUntil(() => player.IsReady, () => Debug.Log("玩家准备好了"));
    ///
    /// // 6. 带进度的定时器（用于动画、进度条等）
    /// timerManager.Create(3f, progress => slider.value = progress, () => Debug.Log("完成"));
    ///
    /// // 7. 使用 Builder 进行复杂配置
    /// timerManager.GetBuilder()
    ///     .SetDuration(3f)
    ///     .SetOnUpdate(p => slider.value = p)
    ///     .SetOnComplete(() => Debug.Log("完成"))
    ///     .UseRealTime()           // 不受 TimeScale 影响
    ///     .BindTo(gameObject)      // 绑定到 GameObject，销毁时自动取消
    ///     .Build();
    ///
    /// // 8. 暂停和恢复
    /// var myTimer = timerManager.Delay(5f, () => Debug.Log("完成"));
    /// myTimer.Pause();   // 暂停
    /// myTimer.Resume();  // 恢复
    ///
    /// // 9. 全局控制
    /// timerManager.PauseAll();   // 暂停所有定时器
    /// timerManager.ResumeAll();  // 恢复所有定时器
    /// timerManager.CancelAll();  // 取消所有定时器
    /// </code>
    /// </example>
    [AutoRegister]
    [SystemPriority(-100)]
    public class TimerManager : ISystem, ISystemUpdate
    {
        private readonly List<Timer> _timers = new();
        private readonly List<Timer> _toAdd = new();
        private readonly List<Timer> _toRemove = new();
        private readonly Dictionary<GameObject, List<Timer>> _goTimers = new();
        private bool _isUpdating;

        /// <summary>获取当前活跃的定时器数量</summary>
        public int ActiveCount => _timers.Count;

        #region 简单方法

        /// <summary>
        /// 延迟执行
        /// </summary>
        /// <param name="duration">延迟时间（秒）</param>
        /// <param name="onComplete">完成时的回调</param>
        /// <param name="useRealTime">是否使用真实时间（不受 TimeScale 影响）</param>
        /// <returns>创建的定时器实例，可用于暂停、取消等操作</returns>
        /// <example>
        /// <code>
        /// timerManager.Delay(2f, () => Debug.Log("2秒后执行"));
        /// timerManager.Delay(1f, () => Debug.Log("暂停时也会计时"), useRealTime: true);
        /// </code>
        /// </example>
        public Timer Delay(float duration, Action onComplete, bool useRealTime = false)
        {
            return CreateInternal(new TimerBuildContext
            {
                Duration = duration,
                Interval = duration,
                OnComplete = onComplete,
                UseRealTime = useRealTime
            });
        }

        /// <summary>
        /// 重复执行
        /// </summary>
        /// <param name="interval">执行间隔（秒）</param>
        /// <param name="onComplete">每次执行的回调</param>
        /// <param name="repeatCount">重复次数，-1 表示无限重复</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// // 每秒执行一次，共5次
        /// timerManager.Repeat(1f, () => Debug.Log("执行"), 5);
        ///
        /// // 无限重复，需要手动取消
        /// var timer = timerManager.Repeat(0.5f, () => Debug.Log("持续执行"));
        /// timer.Cancel(); // 取消
        /// </code>
        /// </example>
        public Timer Repeat(float interval, Action onComplete, int repeatCount = -1, bool useRealTime = false)
        {
            return CreateInternal(new TimerBuildContext
            {
                Duration = interval,
                Interval = interval,
                OnComplete = onComplete,
                IsRepeat = true,
                RepeatCount = repeatCount,
                UseRealTime = useRealTime
            });
        }

        /// <summary>
        /// 创建带进度回调的定时器
        /// <para>适用于动画、进度条、渐变等需要每帧更新的场景</para>
        /// </summary>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="onUpdate">每帧回调，参数为当前进度 (0-1)</param>
        /// <param name="onComplete">完成时的回调</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// // 3秒内从0渐变到1
        /// timerManager.Create(3f,
        ///     progress => slider.value = progress,
        ///     () => Debug.Log("渐变完成"));
        ///
        /// // 配合 Lerp 使用
        /// var startPos = transform.position;
        /// var endPos = targetPos;
        /// timerManager.Create(2f,
        ///     p => transform.position = Vector3.Lerp(startPos, endPos, p));
        /// </code>
        /// </example>
        public Timer Create(float duration, Action<float> onUpdate, Action onComplete = null, bool useRealTime = false)
        {
            return CreateInternal(new TimerBuildContext
            {
                Duration = duration,
                Interval = duration,
                OnUpdate = onUpdate,
                OnComplete = onComplete,
                UseRealTime = useRealTime
            });
        }

        /// <summary>
        /// 下一帧执行
        /// <para>等待一帧后执行，常用于确保某些初始化完成后再执行</para>
        /// </summary>
        /// <param name="onComplete">下一帧执行的回调</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// timerManager.NextFrame(() => Debug.Log("下一帧执行"));
        /// </code>
        /// </example>
        public Timer NextFrame(Action onComplete)
        {
            return CreateInternal(new TimerBuildContext
            {
                FrameCount = 1,
                IsFrameMode = true,
                OnComplete = onComplete
            });
        }

        /// <summary>
        /// 延迟指定帧数后执行
        /// </summary>
        /// <param name="frames">延迟的帧数</param>
        /// <param name="onComplete">完成时的回调</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// timerManager.DelayFrames(60, () => Debug.Log("60帧后执行"));
        /// </code>
        /// </example>
        public Timer DelayFrames(int frames, Action onComplete)
        {
            return CreateInternal(new TimerBuildContext
            {
                FrameCount = Mathf.Max(1, frames),
                IsFrameMode = true,
                OnComplete = onComplete
            });
        }

        /// <summary>
        /// 每隔指定帧数重复执行
        /// </summary>
        /// <param name="frames">间隔帧数</param>
        /// <param name="onComplete">每次执行的回调</param>
        /// <param name="repeatCount">重复次数，-1 表示无限重复</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// // 每30帧执行一次，共10次
        /// timerManager.RepeatFrames(30, () => Debug.Log("执行"), 10);
        /// </code>
        /// </example>
        public Timer RepeatFrames(int frames, Action onComplete, int repeatCount = -1)
        {
            return CreateInternal(new TimerBuildContext
            {
                FrameCount = Mathf.Max(1, frames),
                IsFrameMode = true,
                IsRepeat = true,
                RepeatCount = repeatCount,
                OnComplete = onComplete
            });
        }

        /// <summary>
        /// 等待条件满足后执行
        /// </summary>
        /// <param name="condition">条件判断函数，返回 true 时触发回调</param>
        /// <param name="onComplete">条件满足时的回调</param>
        /// <param name="timeout">超时时间（秒），0 表示无超时</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// // 等待玩家准备好
        /// timerManager.WaitUntil(() => player.IsReady, () => StartGame());
        ///
        /// // 带超时的等待
        /// timerManager.WaitUntil(() => isLoaded, () => OnLoaded(), timeout: 10f);
        /// </code>
        /// </example>
        public Timer WaitUntil(Func<bool> condition, Action onComplete, float timeout = 0, bool useRealTime = false)
        {
            return CreateInternal(new TimerBuildContext
            {
                Condition = condition,
                Timeout = timeout,
                OnComplete = onComplete,
                UseRealTime = useRealTime
            });
        }

        /// <summary>
        /// 等待条件不满足后执行
        /// <para>与 WaitUntil 相反，等待条件变为 false 时触发</para>
        /// </summary>
        /// <param name="condition">条件判断函数，返回 false 时触发回调</param>
        /// <param name="onComplete">条件不满足时的回调</param>
        /// <param name="timeout">超时时间（秒），0 表示无超时</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>创建的定时器实例</returns>
        /// <example>
        /// <code>
        /// // 等待动画播放完成
        /// timerManager.WaitWhile(() => animator.IsPlaying, () => OnAnimationEnd());
        /// </code>
        /// </example>
        public Timer WaitWhile(Func<bool> condition, Action onComplete, float timeout = 0, bool useRealTime = false)
        {
            return WaitUntil(() => !condition(), onComplete, timeout, useRealTime);
        }

        #endregion

        #region Builder 方法

        /// <summary>
        /// 获取定时器构建器
        /// <para>用于复杂配置场景，支持链式调用</para>
        /// </summary>
        /// <returns>定时器构建器实例</returns>
        /// <example>
        /// <code>
        /// timerManager.GetBuilder()
        ///     .SetDuration(3f)
        ///     .SetOnUpdate(p => slider.value = p)
        ///     .SetOnComplete(() => Debug.Log("完成"))
        ///     .UseRealTime()
        ///     .BindTo(gameObject)
        ///     .Build();
        /// </code>
        /// </example>
        public TimerBuilder GetBuilder() => TimerBuilder.Get(this);

        /// <summary>
        /// 从已有配置创建构建器
        /// <para>用于复用配置</para>
        /// </summary>
        /// <param name="context">已保存的配置</param>
        /// <returns>定时器构建器实例</returns>
        /// <example>
        /// <code>
        /// // 保存配置
        /// var config = timerManager.GetBuilder()
        ///     .SetDuration(1f)
        ///     .UseRealTime()
        ///     .BuildContext();
        ///
        /// // 复用配置
        /// timerManager.GetBuilder(config)
        ///     .SetOnComplete(() => Debug.Log("使用保存的配置"))
        ///     .Build();
        /// </code>
        /// </example>
        public TimerBuilder GetBuilder(TimerBuildContext context) => TimerBuilder.Get(this, context);

        #endregion

        #region 内部创建

        internal Timer CreateInternal(TimerBuildContext ctx)
        {
            var timer = new Timer(this, ctx);

            if (_isUpdating)
                _toAdd.Add(timer);
            else
                _timers.Add(timer);

            if (ctx.BindTarget != null)
                BindToGameObject(timer, ctx.BindTarget);

            return timer;
        }

        private void BindToGameObject(Timer timer, GameObject go)
        {
            if (go == null) return;

            if (!_goTimers.TryGetValue(go, out var list))
            {
                list = new List<Timer>();
                _goTimers[go] = list;
                go.GetOrAddComponent<TimerDestroyer>().SetManager(this);
            }
            list.Add(timer);
        }

        #endregion

        #region 控制方法

        /// <summary>
        /// 取消所有定时器
        /// </summary>
        public void CancelAll()
        {
            foreach (var timer in _timers)
                timer.IsRunning = false;
            _timers.Clear();
            _toAdd.Clear();
            _toRemove.Clear();
            _goTimers.Clear();
        }

        /// <summary>
        /// 取消指定 GameObject 上绑定的所有定时器
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        public void CancelAll(GameObject go)
        {
            if (go == null || !_goTimers.TryGetValue(go, out var list)) return;

            foreach (var timer in list)
                timer.Cancel();
            _goTimers.Remove(go);
        }

        /// <summary>
        /// 暂停所有定时器
        /// </summary>
        public void PauseAll()
        {
            foreach (var timer in _timers)
                timer.IsPaused = true;
        }

        /// <summary>
        /// 恢复所有定时器
        /// </summary>
        public void ResumeAll()
        {
            foreach (var timer in _timers)
                timer.IsPaused = false;
        }

        #endregion

        #region 系统更新

        /// <summary>
        /// 系统更新（由框架自动调用）
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            _isUpdating = true;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var timer = _timers[i];
                if (!timer.IsRunning)
                {
                    _timers.RemoveAt(i);
                    continue;
                }

                if (timer.IsPaused) continue;

                timer.Update();
            }

            _isUpdating = false;

            foreach (var t in _toAdd) _timers.Add(t);
            foreach (var t in _toRemove) _timers.Remove(t);
            _toAdd.Clear();
            _toRemove.Clear();
        }

        #endregion

        internal void RemoveTimer(Timer timer)
        {
            if (_isUpdating)
                _toRemove.Add(timer);
            else
                _timers.Remove(timer);
        }
    }
}
