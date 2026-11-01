using UnityEngine;

namespace RulesShift
{
    /// <summary>Text in the world (door labels). Stays readable when the camera is upside down.</summary>
    public class WorldLabel : MonoBehaviour
    {
        public static TextMesh Create(Transform parent, Vector2 pos, string text, Color color, int order, float size = 1f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            var font = GameAssets.I ? GameAssets.I.BodyFont : null;
            if (font)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.1f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sortingOrder = order;
            go.AddComponent<WorldLabel>();
            return tm;
        }

        void LateUpdate()
        {
            if (CameraFollow.I) transform.rotation = CameraFollow.I.transform.rotation;
        }
    }
}
