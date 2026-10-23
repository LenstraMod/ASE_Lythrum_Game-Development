using System.Collections;
using UnityEngine;

public enum BossPartType { LeftHand, RightHand, Head }

// Goes on each hittable boss part (the two hands and the head), next to its trigger collider.
// PlayerCombat calls Hit(); the part forwards it to the boss with its type (the boss decides the damage) and shows
// a short red flash and size punch when the hit counted. Break() fades a destroyed hand out; Restore() undoes it.
public class BossPart : MonoBehaviour
{
    [SerializeField] BossPartType type;
    [SerializeField] float flashTime = 0.08f;
    [SerializeField] float punchScale = 1.06f;
    [SerializeField] float breakTime = 0.5f;

    public BossPartType Type => type;

    static readonly Color FlashColor = new Color(1f, 0.4f, 0.4f);

    SkeletonBoss boss;
    SpriteRenderer sr;
    Collider2D col;
    Vector3 baseScale;
    Coroutine flash;

    void Awake()
    {
        boss = GetComponentInParent<SkeletonBoss>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
    }

    // Returns true if the boss took the hit (false while it is invulnerable, dead, or this part can't be hit).
    public bool Hit(int damage)
    {
        bool took = boss != null && boss.ReceiveHit(type, damage);
        if (took && isActiveAndEnabled)
        {
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }
        return took;
    }

    IEnumerator FlashRoutine()
    {
        SetTint(FlashColor);
        transform.localScale = baseScale * punchScale;
        yield return new WaitForSeconds(flashTime);
        SetTint(Color.white);
        transform.localScale = baseScale;
        flash = null;
    }

    // A destroyed hand turns red, swells a little and fades away, then switches off.
    public void Break()
    {
        if (flash != null) StopCoroutine(flash);
        col.enabled = false;
        StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        for (float t = 0f; t < breakTime; t += Time.deltaTime)
        {
            float k = t / breakTime;
            sr.color = new Color(1f, 0.4f, 0.4f, 1f - k);
            transform.localScale = baseScale * (1f + 0.15f * k);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    public void Restore()
    {
        StopAllCoroutines();
        flash = null;
        gameObject.SetActive(true);
        col.enabled = true;
        sr.color = Color.white;
        transform.localScale = baseScale;
    }

    void SetTint(Color tint)
    {
        sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);   // keep the fade-in alpha
    }
}
