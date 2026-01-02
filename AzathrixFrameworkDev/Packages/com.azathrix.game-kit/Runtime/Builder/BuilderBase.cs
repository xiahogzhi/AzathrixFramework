using System;
using Azathrix.GameKit.Runtime.Pool;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Builder
{
    /// <summary>
    /// 可克隆的构建上下文接口
    /// </summary>
    public interface IBuildContext<T> where T : class, new()
    {
        /// <summary>
        /// 克隆当前上下文
        /// </summary>
        T Clone();
    }

    /// <summary>
    /// 构建器基类
    /// <para>提供对象池复用、链式调用、构建钩子等功能</para>
    /// </summary>
    /// <typeparam name="T">具体构建器类型（CRTP）</typeparam>
    /// <typeparam name="K">构建产物类型</typeparam>
    /// <typeparam name="TContext">构建上下文类型，存储构建参数</typeparam>
    /// <example>
    /// <code>
    /// // 基础用法
    /// var result = MyBuilder.Get()
    ///     .SetSomeProperty(value)
    ///     .Build();
    ///
    /// // 保存配置复用
    /// var ctx = MyBuilder.Get()
    ///     .SetSomeProperty(value)
    ///     .BuildContext();  // 导出配置
    ///
    /// // 后续直接使用保存的配置
    /// var result1 = MyBuilder.Get(ctx).Build();
    /// var result2 = MyBuilder.Get(ctx).Build();
    /// </code>
    /// </example>
    public abstract class BuilderBase<T, K, TContext> : IDisposable
        where T : BuilderBase<T, K, TContext>, new()
        where TContext : class, IBuildContext<TContext>, new()
    {
        private static readonly ObjectPool<T> Pool = new(() => new T());

        /// <summary>是否已销毁</summary>
        public bool isDisposed { private set; get; }

        /// <summary>构建上下文，存储所有构建参数</summary>
        public TContext Context { get; private set; } = new();

        private Action<T> _onPreBuild;
        private Action<T, K> _onPostBuild;

        /// <summary>
        /// 从对象池获取构建器实例
        /// </summary>
        public static T Get() => Pool.Spawn();

        /// <summary>
        /// 从现有配置创建构建器实例
        /// </summary>
        /// <param name="context">已保存的配置</param>
        public static T Get(TContext context)
        {
            var builder = Pool.Spawn();
            builder.Context = context.Clone();
            return builder;
        }

        /// <summary>
        /// 导出当前配置的副本，用于后续复用
        /// </summary>
        /// <param name="autoDispose">是否自动销毁当前构建器</param>
        /// <returns>配置副本</returns>
        public TContext BuildContext(bool autoDispose = true)
        {
            CheckIfDispose();
            var ctx = Context.Clone();
            if (autoDispose)
                Dispose();
            return ctx;
        }

        /// <summary>
        /// 检查构建器是否已销毁
        /// </summary>
        protected void CheckIfDispose()
        {
            if (isDisposed)
                throw new Exception("构建器已销毁无法使用!");
        }

        /// <summary>
        /// 注册构建前回调
        /// </summary>
        /// <param name="action">构建前执行的回调</param>
        public T OnPreBuild(Action<T> action)
        {
            CheckIfDispose();
            _onPreBuild += action;
            return (T)this;
        }

        /// <summary>
        /// 注册构建后回调
        /// </summary>
        /// <param name="action">构建后执行的回调，参数为构建器和构建产物</param>
        public T OnPostBuild(Action<T, K> action)
        {
            CheckIfDispose();
            _onPostBuild += action;
            return (T)this;
        }

        /// <summary>
        /// 销毁构建器，归还到对象池
        /// </summary>
        public void Dispose()
        {
            CheckIfDispose();
            isDisposed = true;
            OnDispose();
            Pool.Despawn((T)this);
        }

        /// <summary>
        /// 销毁时的清理逻辑，子类可重写
        /// </summary>
        protected virtual void OnDispose()
        {
            Context = new TContext();
            _onPreBuild = null;
            _onPostBuild = null;
            isDisposed = false;
        }

        /// <summary>
        /// 执行构建
        /// </summary>
        /// <param name="autoDispose">构建完成后是否自动销毁并归还到池</param>
        /// <returns>构建产物</returns>
        public K Build(bool autoDispose = true)
        {
            CheckIfDispose();
            K t = default;
            try
            {
                _onPreBuild?.Invoke((T)this);
                t = OnBuild();
                _onPostBuild?.Invoke((T)this, t);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (autoDispose)
                Dispose();

            return t;
        }

        /// <summary>
        /// 实际构建逻辑，子类必须实现
        /// </summary>
        protected virtual K OnBuild()
        {
            return default;
        }
    }
}