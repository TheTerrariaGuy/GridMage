using Assets.Scripts;
using Unity.VisualScripting;
using UnityEngine;

public class ManaBar : MonoBehaviour
{
    [SerializeField] private GameObject bar;
    [SerializeField]  private float current, tspeed, vel;
    void Update()
    {
        // WE are writing bad code
        current = Mathf.SmoothDamp(current, GameLogic.INSTANCE.currMana, ref vel, tspeed);
        float fill = GameLogic.INSTANCE.maxMana > 0f
            ? Mathf.Clamp01(current / GameLogic.INSTANCE.maxMana) : 0f;
        bar.transform.localScale = new Vector3(1, fill, 1);
    }
}
