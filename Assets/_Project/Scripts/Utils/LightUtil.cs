using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RulesShift
{
    /// <summary>Point lights created at runtime (hero light, Neon glows).</summary>
    public static class LightUtil
    {
        static FieldInfo layersField;

        public static Light2D Point(Transform parent, Vector3 localPos, Color color, float radius, float intensity)
        {
            Light2D l = null;
            var prefab = GameAssets.I ? GameAssets.I.pointLightPrefab : null;
            if (prefab)
            {
                var go = Object.Instantiate(prefab, parent);
                go.transform.localPosition = localPos;
                l = go.GetComponent<Light2D>();
            }
            if (!l)
            {
                var go = new GameObject("Light2D");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                AllSortingLayers(l);
            }
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.3f;
            l.intensity = intensity;
            return l;
        }

        static void AllSortingLayers(Light2D l)
        {
            if (layersField == null)
                layersField = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (layersField == null) return;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            layersField.SetValue(l, ids);
        }
    }
}
