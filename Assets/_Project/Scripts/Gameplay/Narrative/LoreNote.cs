using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Something in the secret room that remembers: walk up to it and its note shows on parchment.
    /// Only the closest note is shown; a "!" floats above the ones not read yet.
    /// </summary>
    public class LoreNote : MonoBehaviour
    {
        static LoreNote active;

        string titleKey, textKey;
        float radius;
        bool read;
        System.Action onRead;
        Transform marker;
        Vector2 markerBase;

        public static LoreNote Create(Transform parent, Vector2 pos, float radius, string titleKey, string textKey, System.Action onRead = null)
        {
            var go = new GameObject("Lore_" + titleKey);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var n = go.AddComponent<LoreNote>();
            n.titleKey = titleKey;
            n.textKey = textKey;
            n.radius = radius;
            n.onRead = onRead;
            n.markerBase = pos + new Vector2(0f, 1.9f);
            n.marker = WorldLabel.Create(go.transform, n.markerBase, "!", UiKit.Gold, 46, 1.3f).transform;
            return n;
        }

        float Distance
        {
            get
            {
                var p = PlayerController.I;
                return p ? Vector2.Distance(p.Feet, transform.position) : float.MaxValue;
            }
        }

        void Update()
        {
            if (marker) marker.position = markerBase + new Vector2(0f, Mathf.Sin(Time.time * 4f) * 0.12f);

            float d = Distance;
            if (active == this)
            {
                if (d > radius + 0.3f) Hide();
                return;
            }
            if (d > radius || Hud.I == null) return;
            if (active != null && active.Distance <= d) return;

            active = this;
            Hud.I.ShowNote(Loc.T(titleKey), Loc.T(textKey));
            Sfx.Play("card", 0.5f);
            if (read) return;
            read = true;
            if (marker) Destroy(marker.gameObject);
            onRead?.Invoke();
        }

        void Hide()
        {
            if (active != this) return;
            active = null;
            if (Hud.I) Hud.I.HideNote();
        }

        void OnDestroy() => Hide();
    }
}
