using UnityEngine;
using TMPro;

// TEMPORARY test/screenshot tool — drag your 4 text components and 4
// CosmeticDefinition assets in, hit Play, screenshot each one. Delete this
// script (and the 4 test text objects) once you're done — not meant to
// ship. Same per-character vertex-color painting logic as LeaderboardRowUI,
// just standalone so you don't need full row prefabs set up to preview.
public class CosmeticPreviewTester : MonoBehaviour
{
    [System.Serializable]
    public class PreviewSlot
    {
        public TMP_Text text;
        public CosmeticDefinition cosmetic;
    }

    [Tooltip("Drag your 4 temporary text components + the 4 CosmeticDefinition assets you want to preview.")]
    public PreviewSlot[] slots = new PreviewSlot[4];

    void OnEnable()
    {
        Canvas.willRenderCanvases += RepaintAll;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= RepaintAll;
    }

    void RepaintAll()
    {
        foreach (PreviewSlot slot in slots)
        {
            if (slot.text == null || slot.cosmetic == null) continue;
            Paint(slot.text, slot.cosmetic);
        }
    }

    void Paint(TMP_Text text, CosmeticDefinition def)
    {
        switch (def.nicknameStyle)
        {
            case NicknameStyleType.Static:
                ApplySolidColor(text, def.nicknameTopColor);
                break;

            case NicknameStyleType.GoldenShine:
                ApplyGoldenShine(text, def);
                break;

            case NicknameStyleType.ColorFade:
                ApplyColorFade(text, def);
                break;

            case NicknameStyleType.Rainbow:
                ApplyRainbow(text, def);
                break;
        }
    }

    void ApplySolidColor(TMP_Text text, Color32 color)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            WriteCharColor(info, ch, color);
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    void ApplyGoldenShine(TMP_Text text, CosmeticDefinition def)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        int count = info.characterCount;
        if (count == 0) return;

        float cyclePos = Mathf.Repeat(Time.time, Mathf.Max(def.shineIntervalSeconds, 0.01f));
        float sweepT = cyclePos <= def.shineDurationSeconds ? cyclePos / Mathf.Max(def.shineDurationSeconds, 0.01f) : -1f;

        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;

            Color32 col = def.nicknameTopColor;
            if (sweepT >= 0f)
            {
                float charPos = count <= 1 ? 0f : (float)i / (count - 1);
                float brightness = Mathf.Clamp01(1f - Mathf.Abs(charPos - sweepT) / 0.15f);
                col = Color32.Lerp(def.nicknameTopColor, def.shineColor, brightness);
            }
            WriteCharColor(info, ch, col);
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    void ApplyColorFade(TMP_Text text, CosmeticDefinition def)
    {
        Color32[] sequence =
        {
            new Color32(60, 120, 255, 255),
            new Color32(160, 60, 220, 255),
            new Color32(230, 50, 50, 255),
            new Color32(255, 140, 30, 255),
            new Color32(255, 220, 40, 255),
            new Color32(70, 200, 90, 255),
        };

        float loop = Mathf.Max(def.fadeLoopSeconds, 0.01f);
        float t = Mathf.Repeat(Time.time, loop) / loop * sequence.Length;
        int i0 = Mathf.FloorToInt(t) % sequence.Length;
        int i1 = (i0 + 1) % sequence.Length;
        float localT = t - Mathf.Floor(t);

        ApplySolidColor(text, Color32.Lerp(sequence[i0], sequence[i1], localT));
    }

    void ApplyRainbow(TMP_Text text, CosmeticDefinition def)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        int count = info.characterCount;
        if (count == 0) return;

        float travel = Mathf.Max(def.rainbowTravelSeconds, 0.01f);
        float phase = Mathf.Repeat(Time.time, travel) / travel;

        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;

            float charPos = count <= 1 ? 0f : (float)i / (count - 1);
            float hue = Mathf.Repeat(charPos - phase, 1f);
            WriteCharColor(info, ch, Color.HSVToRGB(hue, 0.85f, 1f));
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    void WriteCharColor(TMP_TextInfo info, TMP_CharacterInfo ch, Color32 color)
    {
        int vertIndex = ch.vertexIndex;
        Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
        colors[vertIndex + 0] = color;
        colors[vertIndex + 1] = color;
        colors[vertIndex + 2] = color;
        colors[vertIndex + 3] = color;
    }
}
