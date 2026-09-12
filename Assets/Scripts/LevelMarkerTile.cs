using Assets.Scripts.ScriptableObjects;
using UnityEngine;

namespace Assets.Scripts
{
    public enum LevelSpawnKind { None, Player, Enemy }

    [CreateAssetMenu(fileName = "Level Marker", menuName = "Grid Mage/Level Marker")]
    public sealed class LevelMarkerTile : UnityEngine.Tilemaps.Tile
    {
        public bool walkable = true;
        public bool blocksSight;
        public bool allowsSpells = true;
        public LevelSpawnKind spawnKind;
        public EnemyData enemy;
        [Min(0f), Tooltip("Zero spawns once. Positive values repeat at this interval in seconds.")]
        public float spawnInterval;
    }
}
