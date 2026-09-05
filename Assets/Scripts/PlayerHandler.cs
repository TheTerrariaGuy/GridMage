using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-50)]
    public class PlayerHandler : MonoBehaviour
    {
        public static PlayerHandler INSTANCE;
        public float hp;
        [SerializeField] public int r, c;

        void Start()
        {
            if (INSTANCE != null)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
            Vector3 tilePosition = GridHelper.INSTANCE.GetTileTransform(r, c).position;
            transform.position = new Vector3(tilePosition.x, tilePosition.y, -1f);
        }

        public void TakeDamage(float damage)
        {
            hp -= damage;
        }
    }
}
