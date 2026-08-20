using UnityEngine;

namespace RavenM
{
    public class SpawnCustomGameObjectPacket
    {
        public int SourceID;
        public int GameObjectID;
        public string PrefabHash;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
