using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Indexing : MonoBehaviour
{
    public static Indexing INSTANCE;
    private Dictionary<int, List<Reaction>> reactionMap;
    [NonSerialized] public Dictionary<int, HashSet<Offset>> fadeMap;
    [NonSerialized] public Dictionary<int, Color32> colorMap; // temp, change to sprite/scriptable object later
    [NonSerialized] public Dictionary<int, float> manaCosts;
    [NonSerialized] public Dictionary<int, float> damageMap;

    /*
     * IDS:
     * 0: Nothing
     * 100 - 199: Fire
     * 200 - 299: Water
     * 300 - 399: Electricity
     * 400 - 499: Stone
     * 
     * 
     * Base:
     * Fire 100
     * Water 200
     * Electricity 300
     * Stone 400
     * 
     * Spent:
     * Fire 101
     * Water 201
     * Electricity 301
     * Stone 401
     * 
     * Fader1:
     * Fire 110
     * Water 210
     * Electricity 310
     * Stone 410
     * 
     * Fader 2:
     * Fire 111
     * Water 211
     * Electricity 311
     * Stone 411
     * 
     * Priority:
     * Runs from lowest to highest
     * 
     */

    // Input types ending in * match every reaction-capable state in that element family.
    // Plain input types are exact matches. Outputs are always exact tile types.
    // I = Input (x,y,type), O = Output (x,y,type,priority), D = Directions, E = End
    private const string CastingInfo = @"START
FIRE
I (1,0,100) O (1,0,0,0) (1,0,101,50) (1,1,104,50) (1,-1,104,50) (2,0,104,50) D (0,1,2,3) E
I (1,1,100) O (1,1,0,0) (2,1,104,54) (1,2,104,54) (0,1,101,54) (1,0,101,54) D (0,1,2,3) E

I (0,0,101) O (0,0,102,-1) D (0) E
I (0,0,102) O (0,0,103,-1) D (0) E
I (0,0,103) O (0,0,104,-1) D (0) E
I (0,0,104) O (0,0,105,-1) D (0) E
I (0,0,105) O (0,0,106,-1) D (0) E
I (0,0,106) O (0,0,107,-1) D (0) E
I (0,0,107) O (0,0,0,-1) D (0) E

I (1,0,200*) O (0,0,0,0) (1,0,210,51) (2,0,210,51) (3,0,210,51) (4,0,210,51) (5,0,210,51) D (0,1,2,3) E
I (1,0,300*) O (0,0,0,0) (1,0,310,52) (2,1,310,52) (2,-1,310,52) D (0,1,2,3) E
I (1,0,400*) O (0,0,0,0) (1,0,410,53) (1,1,410,53) (1,-1,410,53) D (0,1,2,3) E

I (0,0,200*) O (0,0,0,0) (2,0,210,55) (1,1,210,55) (0,2,210,55) (-1,1,210,55) (1,-1,210,55) (-2,0,210,55) (-1,-1,210,55) (0,-2,210,55) D (0,1,2,3) E
I (0,0,300*) O (0,0,0,0) (3,0,310,56) (4,0,310,56) (0,3,310,56) (0,4,310,56) (-3,0,310,56) (-4,0,310,56) (0,-3,310,56) (0,-4,310,56) D (0,1,2,3) E
I (0,0,400*) O (0,0,0,0) (0,0,410,56) D (0,1,2,3) E

WATER
I (1,0,200) O (1,0,0,0) (1,0,201,51) (2,0,201,51) (3,0,201,51) (4,0,201,51) D (0,1,2,3) E
I (1,1,200) O (1,1,0,0) (1,2,201,50) (2,1,201,50) (2,2,201,50) D (0,1,2,3) E

ELECTRICITY
I (1,0,300) O (1,0,0,0) (2,1,301,50) (3,2,302,50) (4,3,302,50) (2,-1,301,50) (3,-2,302,50) (4,-3,302,50) D (0,1,2,3) E
I (1,1,300) O (1,1,0,0) (1,2,301,55) (1,3,302,55) (2,1,301,55) (3,1,302,55) D (0,1,2,3) E

I (0,0,301) O (0,0,302,-1) D (0) E
I (0,0,302) O (0,0,0,-1) D (0) E

I (1,0,200*) O (0,0,0,0) (1,0,211,51) (2,0,301,51) (3,0,301,51) D (0,1,2,3) E
I (1,1,200*) O (0,0,0,0) (1,1,211,53) (2,2,301,53) (3,3,301,53) D (0,1,2,3) E

END
";

    /* UNUSED
    I (0,0,300*) O (2,2,310,52) (3,3,310,52) (-2,2,310,52) (-3,3,310,52) (2,-2,310,52) (3,-3,310,52) (-2,-2,310,52) (-3,-3,310,52) D (0,1,2,3) E

    I (0,0,201) O (0,0,202,-1) D (0) E
    I (0,0,202) O (0,0,203,-1) D (0) E
    I (0,0,203) O (0,0,204,-1) D (0) E
    I (0,0,204) O (0,0,205,-1) D (0) E
    I (0,0,205) O (0,0,206,-1) D (0) E
    I (0,0,206) O (0,0,207,-1) D (0) E
    I (0,0,207) O (0,0,0,-1) D (0) E

    */

    private void Awake()
    {
        if (INSTANCE != null && INSTANCE != this)
        {
            Destroy(this);
            return;
        }

        INSTANCE = this;
        Init();
    }

    private void Init()
    {
        reactionMap = new()
        {
            [100] = new List<Reaction>(),
            [200] = new List<Reaction>(),
            [300] = new List<Reaction>()
        };
        fadeMap = new()
        {
            [110] = new HashSet<Offset>()
            {
                Rotate(new Offset(1, 0, 111), 0),
                Rotate(new Offset(1, 0, 111), 1),
                Rotate(new Offset(1, 0, 111), 2),
                Rotate(new Offset(1, 0, 111), 3),
            },
            [210] = new HashSet<Offset>(){
                Rotate(new Offset(1, 0, 211), 0),
                Rotate(new Offset(1, 0, 211), 1),
                Rotate(new Offset(1, 0, 211), 2),
                Rotate(new Offset(1, 0, 211), 3),
            },
            [310] = new HashSet<Offset>(){
                Rotate(new Offset(1, 0, 311), 0),
                Rotate(new Offset(1, 0, 311), 1),
                Rotate(new Offset(1, 0, 311), 2),
                Rotate(new Offset(1, 0, 311), 3),
            },
            [410] = new HashSet<Offset>(){
                Rotate(new Offset(1, 0, 411), 0),
                Rotate(new Offset(1, 0, 411), 1),
                Rotate(new Offset(1, 0, 411), 2),
                Rotate(new Offset(1, 0, 411), 3),
            },
        };

        colorMap = new Dictionary<int, Color32>()
        {
            [100] = new Color32(179, 54, 6, 255),
            [101] = new Color32(226, 121, 30, 255),
            [102] = new Color32(226, 121, 30, 227),
            [103] = new Color32(226, 121, 30, 198),
            [104] = new Color32(226, 121, 30, 170),
            [105] = new Color32(226, 121, 30, 142),
            [106] = new Color32(226, 121, 30, 113),
            [107] = new Color32(226, 121, 30, 85),
            [110] = new Color32(232, 158, 49, 255),
            [111] = new Color32(248, 220, 85, 255),

            [200] = new Color32(47, 108, 217, 255),
            [201] = new Color32(73, 178, 242, 255),
            [202] = new Color32(73, 178, 242, 227),
            [203] = new Color32(73, 178, 242, 198),
            [204] = new Color32(73, 178, 242, 170),
            [205] = new Color32(73, 178, 242, 142),
            [206] = new Color32(73, 178, 242, 113),
            [207] = new Color32(73, 178, 242, 85),
            [210] = new Color32(146, 208, 233, 255),
            [211] = new Color32(186, 231, 243, 255),

            [300] = new Color32(85, 37, 134, 255),
            [301] = new Color32(128, 79, 179, 255),
            [302] = new Color32(128, 79, 179, 200),
            [310] = new Color32(153, 105, 199, 255),
            [311] = new Color32(181, 137, 214, 255),

            [400] = new Color32(139, 143, 142, 255),
            [401] = new Color32(166, 159, 148, 255),
            [410] = new Color32(255, 154, 60, 255),
            [411] = new Color32(255, 111, 60, 255),

            [0] = new Color32(200, 200, 200, 255),
        };

        manaCosts = new Dictionary<int, float>
        {
            [100] = 8f,
            [200] = 6f,
            [300] = 10f,
            [400] = 2f,
        };

        damageMap = new Dictionary<int, float>
        {
            [100] = 10f,
            [101] = 10f,

            [200] = 4f,
            [201] = 4f,
            [210] = 16f,
            [211] = 8f,

            [300] = 1f,
            [301] = 1f,
            [302] = 1f,
            [310] = 4f,
            [311] = 2f,

            [410] = 20f,
            [411] = 16f,
        };

        // Parsing spells
        string[] tokens = CastingInfo.Split(
            new char[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        int curr = 0;
        List<Requirement> input = new List<Requirement>();
        List<Offset> output = new List<Offset>();
        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i];
            
            switch (t)
            {
                case "START" or "END":
                    continue;
                case "FIRE":
                    curr = 100;
                    continue;
                case "WATER":
                    curr = 200;
                    continue;
                case "ELECTRICITY":
                    curr = 300;
                    continue;

                case "I":
                    input = new List<Requirement>();
                    i++;

                    while (!"O".Equals(tokens[i]) &&
                           !"D".Equals(tokens[i]) &&
                           !"E".Equals(tokens[i]))
                    {
                        string[] partsI = tokens[i++].Trim('(', ')').Split(',');
                        int x = int.Parse(partsI[0]);
                        int y = int.Parse(partsI[1]);
                        string typeToken = partsI[2];
                        bool matchFamily = typeToken.EndsWith("*", StringComparison.Ordinal);
                        if (matchFamily)
                        {
                            typeToken = typeToken.Substring(0, typeToken.Length - 1);
                        }

                        int type = int.Parse(typeToken);
                        if (matchFamily && (type < 100 || type % 100 != 0))
                        {
                            throw new FormatException($"Family requirement '{partsI[2]}' must use a nonzero base type such as 100*.");
                        }

                        input.Add(new Requirement(x, y, type, matchFamily));
                    }

                    i--;
                    continue;

                case "O":
                    output = new List<Offset>();
                    i++;

                    while (!"D".Equals(tokens[i]) &&
                           !"E".Equals(tokens[i]))
                    {
                        string[] partsO = tokens[i++].Trim('(', ')').Split(',');
                        int x = int.Parse(partsO[0]);
                        int y = int.Parse(partsO[1]);
                        int type = int.Parse(partsO[2]);
                        int priority = int.Parse(partsO[3]);

                        output.Add(new Offset(x, y, type, priority));
                    }

                    i--;
                    continue;

                case "D":
                    i++;
                    string[] parts = tokens[i++].Trim('(', ')').Split(',');
                    
                    foreach (string part in parts)
                    {
                        int x = int.Parse(part);
                        reactionMap[curr].Add(new Reaction(
                            input.Select(o => Rotate(o, x)),
                            output.Select(o => Rotate(o, x))));
                    }

                    continue;

                case "E":
                    continue;
            }
        }
    }

    public IReadOnlyList<Reaction> GetReactions(int elementType)
    {
        if (reactionMap.TryGetValue(elementType, out List<Reaction> reactions))
        {
            return reactions;
        }

        return Array.Empty<Reaction>();
    }

    private Offset Rotate(Offset offset, int times)
    {
        for (int i = 0; i < times; i++)
        {
            offset = new Offset(-offset.y, offset.x, offset.type, offset.priority);
        }
        return offset;
    }

    private Requirement Rotate(Requirement requirement, int times)
    {
        for (int i = 0; i < times; i++)
        {
            requirement = new Requirement(
                -requirement.y,
                requirement.x,
                requirement.type,
                requirement.matchFamily);
        }

        return requirement;
    }

    // I split up the logic here but whatever 
    public void ModifyFade(HashSet<Offset> o, int r, int c, int type, int[,] before, ref int[,] g)
    {
        foreach (Offset offset in o)
        {
            if (!Assets.Scripts.GridHelper.IsInBounds(g, r + offset.y, c + offset.x))
            {
                continue;
            }
            int target = before[r + offset.y, c + offset.x];
            switch (type) {
                case 110:
                    if (target % 100 > 10 || target < 100) // fire
                    {
                        g[r + offset.y, c + offset.x] = 111;
                    }
                    continue;
                case 210:
                    if (target % 100 > 10 || target < 100 || target / 100 == 1) // water replaces fire
                    {
                        g[r + offset.y, c + offset.x] = 211;
                    }
                    continue;
                case 310:
                    if (target % 100 > 10 || target < 100 || target / 100 == 2) // electricity replaces water
                    {
                        g[r + offset.y, c + offset.x] = 311;
                    }
                    continue;
                case 410:
                    if (target % 100 > 10 || target < 100) // stone
                    {
                        g[r + offset.y, c + offset.x] = 411;
                    }
                    continue;
            }
            
        }
        g[r, c] = 0;
    }


    // CONVERSION: X = C, Y = R
    public sealed class Reaction
    {
        public IReadOnlyCollection<Requirement> Requirements { get; }
        public IReadOnlyCollection<Offset> Outputs { get; }

        public Reaction(
            IEnumerable<Requirement> requirements,
            IEnumerable<Offset> outputs)
        {
            Requirements = requirements.Distinct().ToArray();
            Outputs = outputs.Distinct().ToArray();
        }
    }

    public record Requirement
    {
        public int x;
        public int y;
        public int type;
        public bool matchFamily;

        public Requirement(int x, int y, int type, bool matchFamily)
        {
            this.x = x;
            this.y = y;
            this.type = type;
            this.matchFamily = matchFamily;
        }

        public bool Matches(int actualType)
        {
            if (!matchFamily)
            {
                return actualType == type;
            }

            return actualType >= 100 &&
                   actualType % 100 < 10 &&
                   actualType / 100 == type / 100;
        }
    }

    public record Offset
    {
        public int x;
        public int y;
        public int type;
        public int priority;

        public Offset(int x, int y, int type, int priority = 0)
        {
            this.x = x;
            this.y = y;
            this.type = type;
            this.priority = priority;
        }
    }

}
