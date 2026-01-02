using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// Animator 扩展方法
    /// </summary>
    public static class AnimatorExtensions
    {
        #region 协程版本

        /// <summary>
        /// 播放动画并等待完成（协程）
        /// </summary>
        public static IEnumerator PlayCoroutine(this Animator animator, string animation)
        {
            animator.Play(animation);
            yield return animator.WaitAnimation(animation);
        }

        /// <summary>
        /// 等待动画完成（协程）
        /// </summary>
        public static IEnumerator WaitAnimation(this Animator animator, string animation)
        {
            yield return null;
            while (true)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(animation) || state.normalizedTime % 1 >= 0.99f)
                    yield break;
                yield return null;
            }
        }

        /// <summary>
        /// 等待动画播放到指定进度（协程）
        /// </summary>
        public static IEnumerator CheckAnimationEnd(this Animator animator, float progress)
        {
            yield return null;
            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            while (t <= progress)
            {
                t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }
        }

        /// <summary>
        /// 等待动画完成（协程，带回调）
        /// </summary>
        public static IEnumerator CheckAnimationEnd(this Animator animator, Action callback)
        {
            yield return null;
            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            while (t <= 0.99f)
            {
                t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }
            callback?.Invoke();
        }

        #endregion

        #region UniTask 版本

        /// <summary>
        /// 播放动画并等待完成（异步）
        /// </summary>
        public static async UniTask PlayAsync(this Animator animator, string clipName)
        {
            animator.Play(clipName, 0, 0);
            await animator.WaitAnimationAsync(clipName);
        }

        /// <summary>
        /// 等待动画完成（异步）
        /// </summary>
        public static async UniTask WaitAnimationAsync(this Animator animator, string clipName)
        {
            if (animator == null) return;
            await UniTask.Yield();
            while (true)
            {
                await UniTask.Yield();
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(clipName) || state.normalizedTime % 1 >= 0.99f)
                    return;
            }
        }

        /// <summary>
        /// 播放动画并等待完成（异步，可取消）
        /// </summary>
        public static async UniTask PlayAsync(this Animator animator, string clipName, CancellationToken cancellationToken)
        {
            animator.Play(clipName);
            await animator.WaitAnimationAsync(clipName, cancellationToken);
        }

        /// <summary>
        /// 等待动画完成（异步，可取消）
        /// </summary>
        public static async UniTask WaitAnimationAsync(this Animator animator, string clipName, CancellationToken cancellationToken)
        {
            if (animator == null) return;
            while (true)
            {
                if (await UniTask.Yield(cancellationToken).SuppressCancellationThrow())
                    return;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(clipName) || state.normalizedTime % 1 >= 0.99f)
                    return;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取动画片段长度
        /// </summary>
        public static float GetClipLength(this Animator animator, string clipName)
        {
            if (animator == null || string.IsNullOrEmpty(clipName) || animator.runtimeAnimatorController == null)
                return 0;

            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0) return 0;

            foreach (var clip in clips)
            {
                if (clip != null && clip.name == clipName)
                    return clip.length;
            }
            return 0f;
        }

        /// <summary>
        /// 获取当前动画进度
        /// </summary>
        public static float GetCurrentProgress(this Animator animator, int layer = 0)
        {
            return animator.GetCurrentAnimatorStateInfo(layer).normalizedTime % 1;
        }

        /// <summary>
        /// 判断当前是否在播放指定动画
        /// </summary>
        public static bool IsPlaying(this Animator animator, string stateName, int layer = 0)
        {
            return animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName);
        }

        #endregion
    }
}
