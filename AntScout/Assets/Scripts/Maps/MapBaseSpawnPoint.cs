using UnityEngine;

namespace AntScout.Maps
{
    /// <summary>
    /// Spatial base start location anchor placed within a MapEnvironment prefab.
    /// Represents an available base spawn position and rotation for player or AI colonies.
    /// Follows the StarCraft melee start location model, allowing maps to define 2, 4, or more
    /// symmetrical or asymmetrical spawn positions that can be assigned deterministically or randomized.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapBaseSpawnPoint : MonoBehaviour
    {
        [Tooltip("Human-readable spawn label (e.g. Spawn1, Spawn2). Defaults to GameObject name if empty.")]
        [SerializeField] private string _spawnLabel;

        public string SpawnLabel => !string.IsNullOrEmpty(_spawnLabel) ? _spawnLabel : name;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 2.5f);
            Gizmos.DrawRay(transform.position, transform.forward * 4.0f);
        }
#endif
    }
}
