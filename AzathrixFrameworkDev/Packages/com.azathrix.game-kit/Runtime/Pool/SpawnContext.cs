using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 生成构建上下文
    /// </summary>
    public class SpawnContext : IBuildContext<SpawnContext>
    {
        public string Key { set; get; }
        public Vector3 Position { set; get; }
        public Quaternion Rotation { set; get; } = Quaternion.identity;
        public Transform Parent { set; get; }
        public Vector3? LocalScale { set; get; }

        public SpawnContext Clone()
        {
            return new SpawnContext
            {
                Key = Key,
                Position = Position,
                Rotation = Rotation,
                Parent = Parent,
                LocalScale = LocalScale
            };
        }
    }
}
