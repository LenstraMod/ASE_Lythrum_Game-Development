using System.Collections.Generic;
using UnityEngine;

// The boss's fire breath, Naruto style: a thick, continuous stream of fire pouring out of the mouth toward the player.
// Every puff of fire flies straight out and grows as it goes, so the stream bends when the aim moves. The aim only turns
// slowly (aimTurnRate), so the player gets out of it by running sideways. Touching any puff burns the player (one tick every tickInterval).
public class BossFlameStream : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        public float duration = 3f;             // seconds the boss keeps breathing
        public float aimTurnRate = 30f;         // degrees per second the stream turns toward the player (player runs 5 units/s)
        public float puffSpeed = 9f;            // how far the fire reaches: speed x lifetime
        public float puffLifetime = 1f;
        public float puffsPerSecond = 45f;      // thicker stream = more puffs
        public float startScale = 5f;           // sprite scale when a puff leaves the mouth ...
        public float endScale = 22f;            // ... and when it fades out (the art is tiny, 12 px)
        public float tickInterval = 0.7f;       // seconds between burns while touching the fire
        public float damage = 0.4f;             // health lost per burn (the player has 3)
        [Range(0.2f, 1f)] public float hitSize = 0.8f;   // share of a puff's visible size that burns

        // A bigger version of this stream: wider and denser, and it reaches a little further.
        public Settings Scaled(float size)
        {
            Settings copy = (Settings)MemberwiseClone();
            copy.startScale *= size;
            copy.endScale *= size;
            copy.puffsPerSecond *= size;
            copy.puffSpeed *= 1f + (size - 1f) * 0.5f;
            return copy;
        }
    }

    class Puff
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 velocity;
        public float age;
    }

    Transform origin, target;
    PlayerHealth health;
    Sprite[] frames;
    Settings s;

    readonly List<Puff> puffs = new List<Puff>();
    float aimAngle, elapsed, emitBudget, nextTick;

    public void Init(Transform origin, Transform target, PlayerHealth health, Sprite[] frames, Settings settings)
    {
        this.origin = origin;
        this.target = target;
        this.health = health;
        this.frames = frames;
        s = settings;

        Vector2 toTarget = target.position - origin.position;
        aimAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;   // starts aimed at the player
    }

    void Update()
    {
        if (TimeStopManager.IsTimeStopped) return;      // Time Stop freezes the fire where it is

        float dt = Time.deltaTime;
        elapsed += dt;

        bool breathing = elapsed < s.duration && origin != null && target != null;
        if (breathing)
        {
            Vector2 toTarget = target.position - origin.position;
            float wanted = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            aimAngle = Mathf.MoveTowardsAngle(aimAngle, wanted, s.aimTurnRate * dt);

            emitBudget += s.puffsPerSecond * dt;
            while (emitBudget >= 1f)
            {
                emitBudget -= 1f;
                Emit();
            }
        }

        UpdatePuffs(dt);
        if (!breathing && puffs.Count == 0) Destroy(gameObject);   // done once the last fire has faded
    }

    void Emit()
    {
        GameObject go = new GameObject("Flame Puff");
        go.transform.SetParent(transform, false);
        go.transform.position = origin.position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));   // random spin: the fire looks like it billows
        go.transform.localScale = Vector3.one * s.startScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[Random.Range(0, frames.Length)];
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 5;

        float angle = (aimAngle + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;          // a little spread
        float speed = s.puffSpeed * Random.Range(0.85f, 1.15f);
        puffs.Add(new Puff { t = go.transform, sr = sr, velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed });
    }

    void UpdatePuffs(float dt)
    {
        bool burn = false;
        float spriteSize = frames[0].bounds.size.x;

        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            Puff p = puffs[i];
            p.age += dt;
            float t = p.age / s.puffLifetime;
            if (t >= 1f)
            {
                Destroy(p.t.gameObject);
                puffs.RemoveAt(i);
                continue;
            }

            p.t.position += (Vector3)(p.velocity * dt);
            float scale = Mathf.Lerp(s.startScale, s.endScale, t);
            p.t.localScale = Vector3.one * scale;

            Color c = p.sr.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;     // fade out over the last 40% of its life
            p.sr.color = c;

            float radius = spriteSize * scale * 0.5f * s.hitSize;
            if (target != null && Vector2.Distance(target.position, p.t.position) <= radius) burn = true;
        }

        if (burn && health != null && Time.time >= nextTick)
        {
            nextTick = Time.time + s.tickInterval;
            health.TakeDamage(s.damage);
        }
    }
}
