using TMPro;
using UnityEngine;

// Melengkungkan teks TMP mengikuti busur lingkaran: huruf di tengah paling tinggi, huruf di ujung turun
// dan ikut miring. Dipakai untuk judul di pita banner panel kertas.
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class ArcText : MonoBehaviour
{
    // Radius busur dalam unit canvas. Makin kecil, makin melengkung; 0 = lurus.
    [SerializeField] private float radius = 550f;

    private TMP_Text text;

    void OnEnable()
    {
        text = GetComponent<TMP_Text>();
        text.OnPreRenderText += Bend;
        Refresh();
    }

    void OnDisable()
    {
        text.OnPreRenderText -= Bend;
        Refresh();
    }

    void OnValidate()
    {
        if (text != null) Refresh();
    }

    void Refresh()
    {
        text.havePropertiesChanged = true;
        text.SetVerticesDirty();
    }

    void Bend(TMP_TextInfo info)
    {
        if (radius <= 0f) return;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue;

            Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            int v = c.vertexIndex;

            // Titik acuan tiap huruf: tengah huruf di baseline. Posisi x relatif ke pivot (tengah teks).
            float midX = (vertices[v].x + vertices[v + 2].x) * 0.5f;
            var pivot = new Vector3(midX, c.baseLine, 0f);

            // Panjang busur = jarak horizontal semula, supaya jarak antar huruf tetap.
            float angle = midX / radius;
            var target = new Vector3(Mathf.Sin(angle) * radius, c.baseLine - (1f - Mathf.Cos(angle)) * radius, 0f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);

            for (int j = 0; j < 4; j++)
                vertices[v + j] = target + rotation * (vertices[v + j] - pivot);
        }
    }
}
