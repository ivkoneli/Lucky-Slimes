using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>Floating damage number: rises and fades, then destroys itself.</summary>
    public class DamageNumber : MonoBehaviour
    {
        public float life = 0.7f;
        public float rise = 90f;

        Text _text;
        RectTransform _rt;
        Vector2 _start;
        float _age;

        public void Init(Text text)
        {
            _text = text;
            _rt = text.rectTransform;
            _start = _rt.anchoredPosition;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float k = _age / life;
            if (_rt != null) _rt.anchoredPosition = _start + new Vector2(0f, rise * k);
            if (_text != null) { var c = _text.color; c.a = Mathf.Clamp01(1f - k); _text.color = c; }
            if (_age >= life) Destroy(gameObject);
        }
    }
}
