using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-200)]
public class ParticleVFX : MonoBehaviour
{
    public static ParticleVFX INSTANCE;
    [SerializeField] private ParticleCatalog catalog;
    private readonly Dictionary<int, ParticleCatalog.TileEntry> tiles = new();
    private readonly Dictionary<string, ParticleSystem> reactions = new();
    private readonly Dictionary<int, ParticleSystem> damage = new();
    private readonly Dictionary<ParticleSystem, Stack<Instance>> pools = new();
    private readonly List<Instance> bursts = new();
    private Transform storage;

    public class Burst
    {
        public string id;
        public int row, col, direction;
        public readonly HashSet<Vector2Int> cells = new();

        public Burst(string id, int row, int col, int direction = 0)
        {
            this.id = id; this.row = row; this.col = col; this.direction = direction;
        }
    }

    // Tiles retain an opaque handle; only this manager owns playback and pool state.
    public abstract class Effect { }

    private sealed class Instance : Effect
    {
        public ParticleSystem root, prefab;
        public ParticleSystem[] systems;
        public ParticlePattern pattern;
        public int type;
        public Transform followTarget;
        public Transform anchor;
        public readonly Dictionary<string, ParticleSystem> emitters = new();
    }

    private void Awake()
    {
        INSTANCE = this;
        storage = new GameObject("Particle Pool").transform;
        storage.SetParent(transform, false);
        storage.gameObject.SetActive(false);
        if (catalog == null) return;
        foreach (var entry in catalog.tiles) tiles[entry.type] = entry;
        foreach (var entry in catalog.reactions) reactions[entry.id] = entry.prefab;
        foreach (var entry in catalog.damage) damage[entry.element] = entry.prefab;
    }

    public void SetTile(Transform tile, int type, ref Effect handle)
    {
        var effect = (Instance)handle;
        if (!tiles.TryGetValue(type, out var stage) || stage.prefab == null)
        {
            Release(effect);
            handle = null;
            return;
        }
        if (effect != null && effect.type == type) return;
        if (effect != null && effect.prefab == stage.prefab)
        {
            SetStage(effect, stage);
            effect.type = type;
            return;
        }
        Release(effect);
        handle = effect = Rent(stage.prefab);
        effect.type = type;
        effect.root.transform.SetParent(tile, false);
        effect.root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        SetStage(effect, stage);
        effect.root.gameObject.SetActive(true);
        effect.root.Play(true);
    }

    private static void SetStage(Instance effect, ParticleCatalog.TileEntry stage)
    {
        if (effect.emitters.Count == 0)
            foreach (var system in effect.systems)
                if (system != effect.root) effect.emitters.Add(system.name, system);
        effect.root.transform.localScale = stage.scale;
        foreach (var part in stage.parts) part.Apply(effect.emitters[part.emitter]);
    }

    public void Play(Burst burst, float tickDuration)
    {
        if (burst.cells.Count == 0 || !reactions.TryGetValue(burst.id, out ParticleSystem prefab) || prefab == null) return;
        Instance effect = Rent(prefab);
        GameLogic game = GameLogic.INSTANCE;
        Transform t = effect.root.transform;
        t.SetParent(game.gridParent, false);
        t.localPosition = GridHelper.INSTANCE.GetLocalPosition(burst.row, burst.col, game.spacing, game.offset);
        t.localRotation = Quaternion.Euler(0, 0, -90 * burst.direction);
        t.localScale = prefab.transform.localScale * game.spacing;
        // Resolve emitter orientation after positioning the rotated layout, including pooled reuse.
        if (!effect.pattern.Apply(burst)) { Release(effect); return; }
        foreach (ParticleSystem system in effect.systems)
        {
            var main = system.main;
            main.simulationSpeed = 1f / Mathf.Max(.1f, tickDuration);
        }
        effect.root.gameObject.SetActive(true);
        foreach (ParticleSystem system in effect.systems)
        {
            if (system.gameObject.activeInHierarchy) system.Play(false);
            else system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        bursts.Add(effect);
    }

    public void PlayDamage(Transform enemy, int element)
    {
        if (enemy == null || !damage.TryGetValue(element, out var prefab) || prefab == null) return;
        Instance effect = Rent(prefab);
        effect.followTarget = enemy;
        Transform root = effect.root.transform;
        root.SetParent(transform, false);
        root.SetPositionAndRotation(enemy.position, Quaternion.identity);
        root.localScale = prefab.transform.localScale;

        // Draw over the victim at its feet, while preserving world-Y occlusion by other objects.
        var feet = enemy.GetComponentInChildren<SortingGroup>();
        Vector3 ground = feet != null ? feet.transform.position : enemy.position;
        ground.y -= .001f;
        Vector3 anchor = root.InverseTransformPoint(ground);
        effect.anchor.localPosition = anchor;
        effect.anchor.GetChild(0).localPosition = -anchor; // Visuals remain centered on the victim.
        effect.root.gameObject.SetActive(true);
        effect.root.Play(true);
        bursts.Add(effect);
    }

    private Instance Rent(ParticleSystem prefab)
    {
        if (!pools.TryGetValue(prefab, out Stack<Instance> pool)) pools[prefab] = pool = new();
        if (pool.Count > 0) return pool.Pop();
        ParticleSystem root = Instantiate(prefab, storage);
        root.gameObject.SetActive(false);
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        var effect = new Instance { root = root, prefab = prefab, systems = systems,
            pattern = root.GetComponent<ParticlePattern>(),
            anchor = root.GetComponentInChildren<SortingGroup>(true).transform };
        return effect;
    }

    public void Release(Effect handle)
    {
        var effect = (Instance)handle;
        if (effect == null || effect.root == null) return;
        effect.followTarget = null;
        effect.root.gameObject.SetActive(false);
        foreach (ParticleSystem system in effect.systems)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (system != effect.root) system.gameObject.SetActive(true);
        }
        effect.root.transform.SetParent(storage, false);
        pools[effect.prefab].Push(effect);
    }

    private void Update()
    {
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            Instance effect = bursts[i];
            bool alive = false;
            foreach (ParticleSystem system in effect.systems)
                if (system != effect.root && system.gameObject.activeInHierarchy && system.IsAlive(false))
                { alive = true; break; }
            if (alive) continue;
            Release(effect);
            bursts.RemoveAt(i);
        }
    }

    private void LateUpdate()
    {
        foreach (Instance effect in bursts)
            if (effect.followTarget != null) effect.root.transform.position = effect.followTarget.position;
    }

    public void ClearBursts()
    {
        foreach (Instance effect in bursts) Release(effect);
        bursts.Clear();
    }

    private void OnDestroy()
    {
        if (INSTANCE == this) INSTANCE = null;
    }
}
