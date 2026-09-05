using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.ScriptableObjects
{
    // 
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Game Data/Enemy")]
    public class EnemyData : ScriptableObject 
    {
        public int type;
        public float maxHp, speed, damage, maxAcceleration;
        public float wander, instability, cornerCut;

    }
}
