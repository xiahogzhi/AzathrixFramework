using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 注册构建上下文
    /// </summary>
    public class RegisterContext : IBuildContext<RegisterContext>
    {
        public string Key { set; get; }
        public GameObject Prefab { set; get; }
        public Transform Parent { set; get; }
        public int PrewarmCount { set; get; }
        public int MaxSize { set; get; }= 1000;

        public RegisterContext Clone()
        {
            return new RegisterContext
            {
                Key = Key,
                Prefab = Prefab,
                Parent = Parent,
                PrewarmCount = PrewarmCount,
                MaxSize = MaxSize
            };
        }
    }
}
