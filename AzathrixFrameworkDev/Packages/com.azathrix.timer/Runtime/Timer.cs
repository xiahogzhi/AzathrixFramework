using System;
using UnityEngine;

namespace Azathrix.Timer.Runtime
{
    /// <summary>
    /// 定时器实例
    /// <para>由 TimerManager 创建和管理，不要直接实例化</para>
    /// </summary>
    /// <remarks>
    /// <para>定时器支持以下操作：</para>
    /// <list type="bullet">
    /// <item><description>Pause/Resume - 暂停和恢复</description></item>
    /// <item><description>Cancel - 取消定时器</description></item>
    /// <item><description>Complete - 立即完成并触发回调</description></item>
    /// <item><description>Reset - 重置定时器状态</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var timerManager = AzathrixFramework.GetSystem&lt;TimerManager&gt;();
    ///
    /// // 创建定时器并保存引用
    /// var timer = timerManager.Delay(5f, () => Debug.Log("完成"));
    ///
    /// // 暂停
    /// timer.Pause();
    ///
    /// // 恢复
    /// timer.Resume();
    ///
    /// // 查看状态
    /// Debug.Log($"进度: {timer.Progress}");      // 0-1
    /// Debug.Log($"已过时间: {timer.Elapsed}");   // 秒
    /// Debug.Log($"剩余时间: {timer.Remaining}"); // 秒
    /// Debug.Log($"是否运行: {timer.IsRunning}");
    /// Debug.Log($"是否暂停: {timer.IsPaused}");
    ///
    /// // 取消定时器
    /// timer.Cancel();
    ///
    /// // 或者立即完成（触发回调）
    /// timer.Complete();
    ///
    /// // 重置定时器（重新开始计时）
    /// timer.Reset();
    /// </code>
    /// </example>
    public class Timer
    {
        private readonly TimerManager _manager;
        private float _elapsed;
        private readonly float _duration;
        private readonly float _interval;
        private readonly Action _onComplete;
        private readonly Action<float> _onUpdate;
        private readonly bool _useRealTime;
        private readonly bool _isRepeat;
        private int _repeatCount;
        private readonly int _maxRepeat;
        private readonly bool _isFrameMode;
        private int _frameCount;
        private readonly int _targetFrames;
        private readonly Func<bool> _condition;
        private readonly float _timeout;

        /// <summary>
        /// 定时器是否正在运行
        /// <para>取消或完成后变为 false</para>
        /// </summary>
        public bool IsRunning { get; internal set; }

        /// <summary>
        /// 定时器是否暂停
        /// <para>暂停时不会更新计时</para>
        /// </summary>
        public bool IsPaused { get; internal set; }

        /// <summary>
        /// 当前进度 (0-1)
        /// <para>帧模式下为 当前帧数/目标帧数</para>
        /// <para>时间模式下为 已过时间/总时间</para>
        /// </summary>
        public float Progress => _isFrameMode
            ? Mathf.Clamp01((float)_frameCount / _targetFrames)
            : Mathf.Clamp01(_elapsed / _duration);

        /// <summary>
        /// 已经过的时间（秒）
        /// </summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// 剩余时间（秒）
        /// </summary>
        public float Remaining => Mathf.Max(0, _duration - _elapsed);

        /// <summary>
        /// 当前重复次数
        /// <para>重复模式下，每次执行后递增</para>
        /// </summary>
        public int CurrentRepeatCount => _repeatCount;

        /// <summary>
        /// 内部构造函数（由 TimerManager 调用）
        /// </summary>
        internal Timer(TimerManager manager, TimerBuildContext ctx)
        {
            _manager = manager;
            _duration = ctx.Condition != null ? ctx.Timeout : ctx.Duration;
            _interval = ctx.Interval > 0 ? ctx.Interval : ctx.Duration;
            _onComplete = ctx.OnComplete;
            _onUpdate = ctx.OnUpdate;
            _useRealTime = ctx.UseRealTime;
            _isRepeat = ctx.IsRepeat;
            _maxRepeat = ctx.RepeatCount;
            _isFrameMode = ctx.IsFrameMode;
            _targetFrames = ctx.FrameCount;
            _condition = ctx.Condition;
            _timeout = ctx.Timeout;
            IsRunning = true;
        }

        /// <summary>
        /// 暂停定时器
        /// </summary>
        /// <returns>定时器实例（链式调用）</returns>
        public Timer Pause()
        {
            IsPaused = true;
            return this;
        }

        /// <summary>
        /// 恢复定时器
        /// </summary>
        /// <returns>定时器实例（链式调用）</returns>
        public Timer Resume()
        {
            IsPaused = false;
            return this;
        }

        /// <summary>
        /// 重置定时器
        /// <para>清除已过时间和重复次数，重新开始计时</para>
        /// </summary>
        /// <returns>定时器实例（链式调用）</returns>
        public Timer Reset()
        {
            _elapsed = 0;
            _frameCount = 0;
            _repeatCount = 0;
            IsRunning = true;
            IsPaused = false;
            return this;
        }

        /// <summary>
        /// 取消定时器
        /// <para>停止定时器，不触发完成回调</para>
        /// </summary>
        public void Cancel()
        {
            IsRunning = false;
            _manager.RemoveTimer(this);
        }

        /// <summary>
        /// 立即完成定时器
        /// <para>触发进度回调（进度=1）和完成回调，然后取消定时器</para>
        /// </summary>
        public void Complete()
        {
            if (!IsRunning) return;
            _onUpdate?.Invoke(1f);
            _onComplete?.Invoke();
            Cancel();
        }

        /// <summary>
        /// 内部更新（由 TimerManager 调用）
        /// </summary>
        internal void Update()
        {
            if (_condition != null)
                UpdateCondition();
            else if (_isFrameMode)
                UpdateFrame();
            else
                UpdateTime();
        }

        private void UpdateTime()
        {
            var dt = _useRealTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _elapsed += dt;
            _onUpdate?.Invoke(Progress);

            if (_elapsed >= _interval)
            {
                _onComplete?.Invoke();
                if (_isRepeat)
                {
                    _elapsed = 0;
                    _repeatCount++;
                    if (_maxRepeat > 0 && _repeatCount >= _maxRepeat)
                        IsRunning = false;
                }
                else
                    IsRunning = false;
            }
        }

        private void UpdateFrame()
        {
            _frameCount++;
            _onUpdate?.Invoke(Progress);

            if (_frameCount >= _targetFrames)
            {
                _onComplete?.Invoke();
                if (_isRepeat)
                {
                    _frameCount = 0;
                    _repeatCount++;
                    if (_maxRepeat > 0 && _repeatCount >= _maxRepeat)
                        IsRunning = false;
                }
                else
                    IsRunning = false;
            }
        }

        private void UpdateCondition()
        {
            if (_timeout > 0)
            {
                var dt = _useRealTime ? Time.unscaledDeltaTime : Time.deltaTime;
                _elapsed += dt;
                if (_elapsed >= _timeout)
                {
                    IsRunning = false;
                    return;
                }
            }

            try
            {
                if (_condition())
                {
                    _onComplete?.Invoke();
                    IsRunning = false;
                }
            }
            catch
            {
                IsRunning = false;
            }
        }
    }
}
