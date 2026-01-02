using Azathrix.Framework.Core;
using Azathrix.Framework.Interfaces;

namespace Azathrix.GameKit.Runtime.Extensions
{
    public static class SystemExtensions
    {
        /// <summary>
        /// 获取系统实例
        /// </summary>
        /// <param name="system"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Get<T>(this T system) where T : class, ISystem
        {
            return AzathrixFramework.GetSystem<T>();
        }
    }
}