using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// A combatant (hero or enemy): HP, damage, and a thick HP bar whose fill width tracks % HP,
    /// with "cur/max" text. Enemies carry a goldReward granted on death.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        public float hp, maxHp, damage;
        public bool faceRight;
        public int goldReward;

        public RectTransform hpFillRect;   // left-anchored; width = fullWidth * %hp
        public float hpFillWidth;
        public Text hpText;

        public bool Alive => hp > 0f;

        public void Init(float maxHpValue, float dmg)
        {
            maxHp = maxHpValue;
            hp = maxHpValue;
            damage = dmg;
            UpdateBar();
        }

        public void TakeDamage(float dmg)
        {
            hp -= dmg;
            if (hp < 0f) hp = 0f;
            UpdateBar();
        }

        public void ResetFull()
        {
            hp = maxHp;
            gameObject.SetActive(true);
            UpdateBar();
        }

        void UpdateBar()
        {
            float pct = maxHp > 0f ? hp / maxHp : 0f;
            if (hpFillRect != null) hpFillRect.sizeDelta = new Vector2(hpFillWidth * pct, hpFillRect.sizeDelta.y);
            if (hpText != null) hpText.text = Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(maxHp);
        }
    }
}
