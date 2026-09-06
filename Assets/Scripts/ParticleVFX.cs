using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-200)]
public class ParticleVFX : MonoBehaviour
{
    public static ParticleVFX INSTANCE;
    [SerializeField] private ParticleCatalog catalog;
    private readonly Dictionary<int, ParticleSystem> tiles = new();
    private readonly Dictionary<string, ParticleSystem> reactions = new();
    private readonly Dictionary<int, ParticleSystem> damage = new();
    private readonly Dictionary<ParticleSystem, Stack<Effect>> pools = new();
    private readonly Dictionary<ParticleSystem, ParticleSystem[]> templates = new();
    private readonly List<Effect> bursts = new();
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

    public class Effect
    {
        public ParticleSystem root, prefab;
        public ParticleSystem[] systems;
        public ParticlePattern pattern;
        public int type;
        public Transform followTarget;
        public SortingGroup[] groups;
    }

    private void Awake()
    {
        INSTANCE = this;
        storage = new GameObject("Particle Pool").transform;
        storage.SetParent(transform, false);
        storage.gameObject.SetActive(false);
        if (catalog == null) return;
        foreach (var entry in catalog.tiles) tiles[entry.type] = entry.prefab;
        foreach (var entry in catalog.reactions) reactions[entry.id] = entry.prefab;
        foreach (var entry in catalog.damage) damage[entry.element] = entry.prefab;
    }

    public void SetTile(Transform tile, int type, ref Effect effect)
    {
        if (!tiles.TryGetValue(type, out ParticleSystem prefab) || prefab == null)
        {
            Release(effect);
            effect = null;
            return;
        }
        if (effect != null && effect.type == type) return;
        if (effect != null && Family(effect.type) == Family(type))
        {
            SetStage(effect, prefab);
            effect.type = type;
            return;
        }
        Release(effect);
        effect = Rent(prefab);
        effect.type = type;
        effect.root.transform.SetParent(tile, false);
        effect.root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        SetStage(effect, prefab);
        effect.root.gameObject.SetActive(true);
        effect.root.Play(true);
    }

    private static int Family(int type) => type < 100 ? type : type / 100 * 100 + (type % 100 >= 10 ? 10 : 0);

    private void SetStage(Effect effect, ParticleSystem prefab)
    {
        effect.root.transform.localScale = prefab.transform.localScale;
        if (!templates.TryGetValue(prefab, out ParticleSystem[] source))
            templates[prefab] = source = prefab.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < effect.systems.Length; i++)
        {
            var main = effect.systems[i].main;
            main.startColor = source[i].main.startColor;
            main.maxParticles = source[i].main.maxParticles;
            var color = effect.systems[i].colorOverLifetime;
            color.color = source[i].colorOverLifetime.color;
            var emission = effect.systems[i].emission;
            emission.rateOverTime = source[i].emission.rateOverTime;
        }
    }

    public void Play(Burst burst, float tickDuration)
    {
        if (burst.cells.Count == 0 || !reactions.TryGetValue(burst.id, out ParticleSystem prefab) || prefab == null) return;
        Effect effect = Rent(prefab);
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
        Effect effect = Rent(prefab);
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
        foreach (var group in effect.groups)
        {
            Vector3 delta = anchor - group.transform.localPosition;
            group.transform.localPosition = anchor;
            foreach (Transform child in group.transform) child.localPosition -= delta;
        }
        effect.root.gameObject.SetActive(true);
        effect.root.Play(true);
        bursts.Add(effect);
    }

    private Effect Rent(ParticleSystem prefab)
    {
        if (!pools.TryGetValue(prefab, out Stack<Effect> pool)) pools[prefab] = pool = new();
        if (pool.Count > 0) return pool.Pop();
        ParticleSystem root = Instantiate(prefab, storage);
        root.gameObject.SetActive(false);
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        var pattern = root.GetComponent<ParticlePattern>();
        WorldSorting.ConfigureParticles(root, systems, pattern);
        return new Effect { root = root, prefab = prefab, systems = systems, pattern = pattern,
            groups = root.GetComponentsInChildren<SortingGroup>(true) };
    }

    public void Release(Effect effect)
    {
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
            Effect effect = bursts[i];
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
        foreach (Effect effect in bursts)
            if (effect.followTarget != null) effect.root.transform.position = effect.followTarget.position;
    }

    public void ClearBursts()
    {
        foreach (Effect effect in bursts) Release(effect);
        bursts.Clear();
    }

    private void OnDestroy()
    {
        if (INSTANCE == this) INSTANCE = null;
    }
}
