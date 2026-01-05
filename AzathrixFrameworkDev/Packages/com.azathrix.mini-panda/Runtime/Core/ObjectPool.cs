using System;
using System.Collections.Generic;

namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// 通用对象池，用于减少 GC 压力
    /// </summary>
    /// <typeparam name="T">池化对象类型，必须是引用类型</typeparam>
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool = new Stack<T>();
        private readonly Func<T> _createFn;
        private readonly Action<T> _resetFn;
        private readonly int _maxSize;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="createFn">对象创建函数</param>
        /// <param name="resetFn">对象重置函数（归还时调用）</param>
        /// <param name="maxSize">池最大容量</param>
        public ObjectPool(Func<T> createFn, Action<T> resetFn = null, int maxSize = 64)
        {
            _createFn = createFn ?? throw new ArgumentNullException(nameof(createFn));
            _resetFn = resetFn;
            _maxSize = maxSize;
        }

        /// <summary>
        /// 租用对象（从池中获取或创建新对象）
        /// </summary>
        public T Rent()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            return _createFn();
        }

        /// <summary>
        /// 归还对象到池中
        /// </summary>
        /// <param name="obj">要归还的对象</param>
        public void Return(T obj)
        {
            if (obj == null) return;

            if (_pool.Count < _maxSize)
            {
                _resetFn?.Invoke(obj);
                _pool.Push(obj);
            }
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear() => _pool.Clear();

        /// <summary>
        /// 获取池中当前对象数量
        /// </summary>
        public int Count => _pool.Count;
    }
}
