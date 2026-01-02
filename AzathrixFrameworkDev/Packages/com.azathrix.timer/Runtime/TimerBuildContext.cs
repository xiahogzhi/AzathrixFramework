using System;
using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.Timer.Runtime
{
    /// <summary>
    /// 定时器构建上下文
    /// </summary>
    public class TimerBuildContext : IBuildContext<TimerBuildContext>
    {
        public float Duration;
        public float Interval;
        public int RepeatCount = -1;
        public int FrameCount;
        public bool UseRealTime;
        public bool IsRepeat;
        public bool IsFrameMode;
        public Func<bool> Condition;
        public float Timeout;
        public Action OnComplete;
        public Action<float> OnUpdate;
        public GameObject BindTarget;

        public TimerBuildContext Clone() => new()
        {
            Duration = Duration,
            Interval = Interval,
            RepeatCount = RepeatCount,
            FrameCount = FrameCount,
            UseRealTime = UseRealTime,
            IsRepeat = IsRepeat,
            IsFrameMode = IsFrameMode,
            Condition = Condition,
            Timeout = Timeout,
            OnComplete = OnComplete,
            OnUpdate = OnUpdate,
            BindTarget = BindTarget
        };
    }
}
