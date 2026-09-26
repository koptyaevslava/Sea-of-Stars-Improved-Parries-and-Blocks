using System;
using Il2CppInterop.Runtime;
using Sabotage.Localization;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ComboHud
{
    private static GameObject root;
    private static GameObject accentRoot;
    private static RectTransform counterRect;
    private static RectTransform accentRect;
    private static TextMeshProUGUI counter;
    private static TextMeshProUGUI accent;
    private static float pulseStarted = -1f;
    private static bool visible;
    private static int pendingCount;
    private static bool pendingReady;
    private static int lastCount = -1;
    private static bool lastReady;

    public static void Show(int count, bool ready, bool pulse = false)
    {
        visible = true;
        pendingCount = Math.Max(0, count);
        pendingReady = ready;
        if (pulse) pulseStarted = Time.unscaledTime;
        EnsureCreated();
        ApplyState();
    }

    public static void Update(int count, bool ready)
    {
        if (!visible) return;
        pendingCount = Math.Max(0, count);
        pendingReady = ready;
        EnsureCreated();
        ApplyState();
        AnimatePulse();
    }

    public static void Tick()
    {
        if (!visible) return;
        EnsureCreated();
        ApplyState();
        AnimatePulse();
    }

    public static void Hide()
    {
        visible = false;
        pulseStarted = -1f;
        lastCount = -1;
        lastReady = false;
        if (root != null) root.SetActive(false);
        if (accentRoot != null) accentRoot.SetActive(false);
    }

    public static void Dispose()
    {
        if (root != null) Object.Destroy(root);
        if (accentRoot != null) Object.Destroy(accentRoot);
        root = null;
        accentRoot = null;
        counter = null;
        accent = null;
        counterRect = null;
        accentRect = null;
    }

    private static void EnsureCreated()
    {
        if (root != null) return;

        TextMeshProUGUI source = FindActiveNativeCounter();
        if (source == null) return;
        Canvas canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        root = Object.Instantiate(source.gameObject, canvas.transform);
        root.name = "ParriesAndBlocks.SpecialCombo";
        root.transform.SetAsLastSibling();
        foreach (var localizer in root.GetComponentsInChildren<TextLocalizer>(true)) localizer.enabled = false;

        counter = root.GetComponent<TextMeshProUGUI>();
        counterRect = root.GetComponent<RectTransform>();
        if (counter == null || counterRect == null)
        {
            Object.Destroy(root);
            root = null;
            counter = null;
            counterRect = null;
            return;
        }

        counterRect.anchorMin = new Vector2(1f, 0.5f);
        counterRect.anchorMax = new Vector2(1f, 0.5f);
        counterRect.pivot = new Vector2(1f, 0.5f);
        counterRect.anchoredPosition = new Vector2(-18f, 0f);
        counterRect.sizeDelta = new Vector2(120f, 40f);
        counterRect.localRotation = Quaternion.identity;
        counterRect.localScale = Vector3.one;

        counter.raycastTarget = false;
        counter.enableAutoSizing = false;
        counter.fontSize *= 2.5f;
        counter.enableWordWrapping = false;
        counter.richText = false;
        counter.alignment = TextAlignmentOptions.MidlineRight;
        counter.fontStyle = FontStyles.Normal;
        counter.color = Color.white;

        accentRoot = Object.Instantiate(root, canvas.transform);
        accentRoot.name = "ParriesAndBlocks.SpecialComboAccent";
        accent = accentRoot.GetComponent<TextMeshProUGUI>();
        accentRect = accentRoot.GetComponent<RectTransform>();
        if (accent == null || accentRect == null)
        {
            Object.Destroy(accentRoot);
            accentRoot = null;
            accent = null;
            accentRect = null;
        }
        else
        {
            accentRect.anchoredPosition = counterRect.anchoredPosition + new Vector2(1.25f, -1.25f);
            accent.color = UiColor(new Color32(0, 210, 255, 255));
            accentRoot.transform.SetSiblingIndex(root.transform.GetSiblingIndex());
            root.transform.SetAsLastSibling();
            accentRoot.SetActive(visible);
        }
        lastCount = -1;
        lastReady = !pendingReady;
        root.SetActive(visible);
    }

    private static TextMeshProUGUI FindActiveNativeCounter()
    {
        try
        {
            TextMeshProUGUI fallback = null;
            foreach (var resource in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TimedHitPlayerUI>()))
            {
                TimedHitPlayerUI ui = resource.TryCast<TimedHitPlayerUI>();
                TextMeshProUGUI candidate = ui?.resultCountField;
                if (candidate == null || candidate.font == null || candidate.fontSharedMaterial == null) continue;
                fallback ??= candidate;
                if (candidate.gameObject.activeInHierarchy) return candidate;
            }
            return fallback;
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyState()
    {
        if (root == null || counter == null) return;
        root.SetActive(visible);
        if (accentRoot != null) accentRoot.SetActive(visible);
        if (!visible) return;
        if (pendingCount != lastCount)
        {
            counter.text = "X " + pendingCount;
            if (accent != null) accent.text = counter.text;
            lastCount = pendingCount;
        }
        if (pendingReady != lastReady)
        {
            counter.color = pendingReady ? UiColor(new Color32(255, 204, 52, 255)) : Color.white;
            if (accent != null)
            {
                accent.color = pendingReady
                    ? UiColor(new Color32(255, 126, 24, 255))
                    : UiColor(new Color32(0, 210, 255, 255));
            }
            if (pendingReady)
            {
                if (pulseStarted < 0f) pulseStarted = Time.unscaledTime;
            }
            else
            {
                pulseStarted = -1f;
            }
            lastReady = pendingReady;
        }
    }

    private static void AnimatePulse()
    {
        if (counterRect == null) return;
        if (!pendingReady)
        {
            counterRect.localScale = Vector3.one;
            if (accentRect != null) accentRect.localScale = Vector3.one;
            return;
        }
        if (pulseStarted < 0f) pulseStarted = Time.unscaledTime;
        const float period = 0.75f;
        float phase = (Time.unscaledTime - pulseStarted) * (Mathf.PI * 2f / period) - Mathf.PI * 0.5f;
        float wave = (Mathf.Sin(phase) + 1f) * 0.5f;
        float scale = 1f + 0.18f * wave;
        Vector3 pulseScale = new Vector3(scale, scale, 1f);
        counterRect.localScale = pulseScale;
        if (accentRect != null) accentRect.localScale = pulseScale;
    }

    private static Color UiColor(Color authoredSrgb)
        => QualitySettings.activeColorSpace == ColorSpace.Linear ? authoredSrgb.linear : authoredSrgb;
}
