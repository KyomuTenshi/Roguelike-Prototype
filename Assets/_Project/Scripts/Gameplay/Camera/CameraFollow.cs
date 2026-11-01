using UnityEngine;

namespace RulesShift
{
    /// <summary>Smooth follow, clamped to the room, with screen shake and the Upside-down rotation.</summary>
    [DefaultExecutionOrder(100)]
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow I { get; private set; }

        public Transform target;
        public Rect bounds = new Rect(0, 0, 24, 14);
        public float smooth = 9f;
        public float ambientShake;
        public static bool ShakeEnabled = true;   // Settings → Тряска экрана

        Camera cam;
        Vector2 pos;
        float shake;
        float angle;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        public void Snap()
        {
            pos = Desired();
            angle = Rules.CameraAngle;
            Place(Vector2.zero);
        }

        Vector2 Desired()
        {
            Vector2 d = target ? (Vector2)target.position + new Vector2(0f, 0.7f) : bounds.center;
            float h = cam ? cam.orthographicSize : 7f;
            float w = cam ? h * cam.aspect : 12f;
            d.x = bounds.width <= w * 2f ? bounds.center.x : Mathf.Clamp(d.x, bounds.xMin + w, bounds.xMax - w);
            d.y = bounds.height <= h * 2f ? bounds.center.y : Mathf.Clamp(d.y, bounds.yMin + h, bounds.yMax - h);
            return d;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            pos = Vector2.Lerp(pos, Desired(), 1f - Mathf.Exp(-smooth * dt));
            shake = Mathf.MoveTowards(shake, 0f, dt * (shake * 5f + 0.15f));
            float s = Time.timeScale > 0f && ShakeEnabled ? shake + ambientShake : 0f;
            angle = Mathf.MoveTowardsAngle(angle, Rules.CameraAngle, 300f * dt);
            Place(Random.insideUnitCircle * s);
        }

        void Place(Vector2 offset)
        {
            transform.position = new Vector3(pos.x + offset.x, pos.y + offset.y, -10f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
