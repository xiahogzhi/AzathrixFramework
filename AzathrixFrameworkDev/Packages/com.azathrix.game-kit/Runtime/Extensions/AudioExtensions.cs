using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 音频扩展方法
    /// </summary>
    public static class AudioExtensions
    {
        #region AudioSource

        /// <summary>
        /// 播放音效（一次性）
        /// </summary>
        public static void PlayOneShot(this AudioSource source, AudioClip clip, float volumeScale = 1f)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// 安全播放
        /// </summary>
        public static void PlaySafe(this AudioSource source)
        {
            if (source != null && !source.isPlaying)
                source.Play();
        }

        /// <summary>
        /// 安全停止
        /// </summary>
        public static void StopSafe(this AudioSource source)
        {
            if (source != null && source.isPlaying)
                source.Stop();
        }

        /// <summary>
        /// 设置音量
        /// </summary>
        public static void SetVolume(this AudioSource source, float volume)
        {
            if (source != null)
                source.volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 设置静音
        /// </summary>
        public static void SetMute(this AudioSource source, bool mute)
        {
            if (source != null)
                source.mute = mute;
        }

        #endregion

        #region 淡入淡出

        /// <summary>
        /// 淡入播放
        /// </summary>
        /// <param name="source">音频源</param>
        /// <param name="targetVolume">目标音量</param>
        /// <param name="duration">淡入时长（秒）</param>
        public static async UniTask FadeIn(this AudioSource source, float targetVolume = 1f, float duration = 1f)
        {
            if (source == null) return;

            source.volume = 0f;
            source.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                await UniTask.Yield();
            }
            source.volume = targetVolume;
        }

        /// <summary>
        /// 淡出停止
        /// </summary>
        /// <param name="source">音频源</param>
        /// <param name="duration">淡出时长（秒）</param>
        public static async UniTask FadeOut(this AudioSource source, float duration = 1f)
        {
            if (source == null || !source.isPlaying) return;

            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                await UniTask.Yield();
            }
            source.Stop();
            source.volume = startVolume;
        }

        /// <summary>
        /// 交叉淡入淡出
        /// </summary>
        public static async UniTask CrossFade(this AudioSource from, AudioSource to, float duration = 1f)
        {
            if (to == null) return;

            float toTargetVolume = to.volume;
            to.volume = 0f;
            to.Play();

            await UniTask.WhenAll(
                from != null ? from.FadeOut(duration) : UniTask.CompletedTask,
                FadeToVolume(to, toTargetVolume, duration)
            );
        }

        private static async UniTask FadeToVolume(AudioSource source, float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                await UniTask.Yield();
            }
            source.volume = targetVolume;
        }

        #endregion

        #region AudioClip

        /// <summary>
        /// 获取音频时长（秒）
        /// </summary>
        public static float GetDuration(this AudioClip clip)
        {
            return clip != null ? clip.length : 0f;
        }

        #endregion
    }
}
