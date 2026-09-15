using System;
using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;

/// <summary>Scene adapter for the authored reaction rules. Stage properties live in ElementDefinitions.</summary>
public sealed class Indexing : MonoBehaviour
{
    public static Indexing INSTANCE { get; private set; }
    [SerializeField] private TextAsset reactionDefinitions;
    private IReadOnlyDictionary<int, List<Reaction>> reactions;

    private void Awake()
    {
        if (INSTANCE != null && INSTANCE != this) { Destroy(this); return; }
        INSTANCE = this;
        if (reactionDefinitions == null) throw new InvalidOperationException("Assign reaction definitions on Indexing.");
        reactions = ReactionParser.Parse(reactionDefinitions.text);
    }
    public IReadOnlyList<Reaction> GetReactions(int type) =>
        reactions.TryGetValue(type, out var list) ? list : Array.Empty<Reaction>();
    private void OnDestroy() { if (INSTANCE == this) INSTANCE = null; }
}
