using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Sabotage.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ModsMenu
{
    private static ManualLogSource log;
    private static readonly Dictionary<IntPtr, MenuView> views = new();

    public static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        var init = AccessTools.Method(typeof(OptionsScreen), "Init")
            ?? throw new MissingMethodException(nameof(OptionsScreen), "Init");
        var update = AccessTools.Method(typeof(OptionsScreen), "Update")
            ?? throw new MissingMethodException(nameof(OptionsScreen), "Update");
        harmony.Patch(init,
            prefix: new HarmonyMethod(typeof(ModsMenu), nameof(BeforeInit)),
            postfix: new HarmonyMethod(typeof(ModsMenu), nameof(AfterInit)));
        harmony.Patch(update, postfix: new HarmonyMethod(typeof(ModsMenu), nameof(AfterUpdate)));
        log.LogInfo("Options menu hook installed.");
    }

    private static void BeforeInit(OptionsScreen __instance)
    {
        try { GetOrCreate(__instance); }
        catch (Exception ex) { log.LogError("Could not create Mods tab shell: " + ex); }
    }

    private static void AfterInit(OptionsScreen __instance)
    {
        try { GetOrCreate(__instance).InitializeContent(); }
        catch (Exception ex) { log.LogError("Could not initialize Mods tab content: " + ex); }
    }

    private static void AfterUpdate(OptionsScreen __instance)
    {
        if (views.TryGetValue(__instance.Pointer, out MenuView view)) view.Update();
    }

    private static MenuView GetOrCreate(OptionsScreen screen)
    {
        if (views.TryGetValue(screen.Pointer, out MenuView existing) && existing.IsAlive) return existing;
        var created = new MenuView(screen);
        views[screen.Pointer] = created;
        return created;
    }

    private sealed class MenuView
    {
        private readonly OptionsScreen screen;
        private readonly GameObject pageRoot;
        private readonly RectTransform pageRect;
        private readonly GameObject selectorRoot;
        private readonly TextMeshProUGUI selectorLabel;
        private readonly UITabPage page;
        private readonly List<SettingRow> rows = new();
        private TextMeshProUGUI title;
        private TextMeshProUGUI description;
        private ELanguage lastLanguage = (ELanguage)(-1);
        private bool initialized;
        private bool diagnosticsLogged;

        public bool IsAlive => pageRoot != null && selectorRoot != null;

        public MenuView(OptionsScreen owner)
        {
            screen = owner;
            selectorRoot = Object.Instantiate(owner.keyboardTabSelector, owner.keyboardTabSelector.transform.parent);
            selectorRoot.name = "Mods tab selector";
            int nextButtonIndex = owner.nextTabBtn != null
                ? owner.nextTabBtn.transform.GetSiblingIndex()
                : owner.keyboardTabSelector.transform.GetSiblingIndex() + 1;
            selectorRoot.transform.SetSiblingIndex(nextButtonIndex);
            DisableLocalizers(selectorRoot);
            selectorLabel = FirstText(selectorRoot);
            selectorLabel.text = T(TextKey.Mods, CurrentLanguage());

            pageRoot = NewRectObject("Mods tab", owner.generalTab.transform.parent);
            pageRect = pageRoot.GetComponent<RectTransform>();
            CopyRect(owner.generalTab.GetComponent<RectTransform>(), pageRect);
            CopyVerticalLayout(owner.generalTab.gameObject, pageRoot);
            page = pageRoot.AddComponent<UITabPage>();
            page.label = selectorLabel;
            pageRoot.SetActive(false);
            owner.tabController.tabs.Add(page);
            log.LogInfo("Added Mods tab shell before the native tab controller initialized.");
        }

        public void InitializeContent()
        {
            if (initialized) return;
            try
            {
                CreateSpacer();
                title = CreateTitle(NativeSectionHeader(), pageRoot.transform);
                CreateRows();
                description = CreateDescription(screen.refreshRateWarningObj, pageRoot.transform);
                page.defaultSelected = rows[0].Toggle.gameObject;
                initialized = true;
                RefreshLanguage(true);
                SyncFromConfig();
                LayoutRebuilder.MarkLayoutForRebuild(pageRect);
                log.LogInfo("Initialized Mods tab from the native 300x236 General-page layout and generic option control.");
            }
            catch
            {
                ResetContent();
                throw;
            }
        }

        private void CreateSpacer()
        {
            Transform nativeSpacer = screen.generalTab.transform.Find("Spacer");
            if (nativeSpacer != null)
            {
                GameObject spacer = Object.Instantiate(nativeSpacer.gameObject, pageRoot.transform);
                spacer.name = "Spacer";
                spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 12f);
                return;
            }
            GameObject fallback = NewRectObject("Spacer", pageRoot.transform);
            fallback.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 12f);
        }

        private void CreateRows()
        {
            var definitions = new[]
            {
                new RowDefinition(TextKey.Enabled, RowType.Boolean, 0.0f, 1.0f, 1.0f),
                new RowDefinition(TextKey.BlockWindow, RowType.Seconds, 0.1f, 10.0f, 0.1f),
                new RowDefinition(TextKey.AttackWindow, RowType.Seconds, 0.1f, 10.0f, 0.1f),
                new RowDefinition(TextKey.ComboTarget, RowType.Integer, 1.0f, 20.0f, 1.0f),
                new RowDefinition(TextKey.ReflectionHold, RowType.Seconds, 0.1f, 30.0f, 0.1f)
            };

            for (int i = 0; i < definitions.Length; i++)
            {
                GameObject rowObject = Object.Instantiate(screen.crashReportsBtn.gameObject, pageRoot.transform);
                rowObject.name = "Mod setting " + definitions[i].Key;
                DisableLocalizers(rowObject);
                var toggle = rowObject.GetComponent<UIMultiValueToggle>();
                toggle.mode = UIMultiValueToggle.UIMultiValueToggleMode.Strings;
                toggle.onToggled = new UnityEvent();
                toggle.onSelected = new UnityEvent();
                toggle.itemLabel.gameObject.SetActive(true);
                toggle.interactable = true;
                TextMeshProUGUI displayLabel = CreateRowLabel(toggle.label, rowObject.transform);
                toggle.label.gameObject.SetActive(false);
                RectTransform rect = rowObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0f, 28f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                rows.Add(new SettingRow(toggle, displayLabel, definitions[i]));
            }

            for (int i = 0; i < rows.Count; i++)
            {
                Navigation navigation = rows[i].Toggle.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = rows[(i + rows.Count - 1) % rows.Count].Toggle;
                navigation.selectOnDown = rows[(i + 1) % rows.Count].Toggle;
                rows[i].Toggle.navigation = navigation;
            }
        }

        public void Update()
        {
            if (!initialized) return;
            RefreshLanguage(false);
            foreach (SettingRow row in rows)
            {
                row.SyncVisualStyle(screen.crashReportsBtn.itemLabel);
                row.ApplyChange();
            }
            if (!diagnosticsLogged && pageRoot.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(pageRect);
                ValidateLayout();
                diagnosticsLogged = true;
            }
        }

        private void RefreshLanguage(bool force)
        {
            ELanguage language = CurrentLanguage();
            if (!force && language == lastLanguage) return;
            lastLanguage = language;
            selectorLabel.text = T(TextKey.Mods, language);
            title.text = T(TextKey.Title, language);
            description.text = T(TextKey.Description, language);
            CopyTextStyle(FirstText(screen.keyboardTabSelector), selectorLabel);
            CopyTextStyle(FirstText(NativeSectionHeader()), title);
            CopyTextStyle(FirstText(screen.refreshRateWarningObj), description);
            selectorLabel.ForceMeshUpdate(true, true);
            title.ForceMeshUpdate(true, true);
            description.ForceMeshUpdate(true, true);
            foreach (SettingRow row in rows) row.RefreshLanguage(language, screen.crashReportsBtn.itemLabel);
            LayoutRebuilder.MarkLayoutForRebuild(pageRect);
        }

        private void SyncFromConfig()
        {
            foreach (SettingRow row in rows) row.SyncFromConfig();
        }

        private void ValidateLayout()
        {
            float totalHeight = 12f + 12f + rows.Count * 28f;
            log.LogInfo($"Mods layout check: page={pageRect.rect.width:0.0}x{pageRect.rect.height:0.0}, " +
                $"children={pageRoot.transform.childCount}, plannedHeight={totalHeight:0.0}, rows={rows.Count}.");
            if (Mathf.Abs(pageRect.rect.width - 300f) > 0.1f || Mathf.Abs(pageRect.rect.height - 236f) > 0.1f)
                log.LogWarning("Mods page differs from the native 300x236 General-page geometry.");
            bool templateHasLanguageImage = screen.crashReportsBtn.transform.Find("Button/ValueContent/LanguageSelectImage") != null;
            log.LogInfo($"Mods control template={screen.crashReportsBtn.gameObject.name}, " +
                $"languageImage={templateHasLanguageImage}, selected={rows[0].Toggle.selectedIdx}, " +
                $"values={rows[0].Toggle.labels.Count}, rendered='{rows[0].Toggle.itemLabel.text}'.");
            if (templateHasLanguageImage)
                log.LogWarning("The selected Mods control template unexpectedly contains LanguageSelectImage.");
            for (int i = 0; i < rows.Count; i++) rows[i].ValidateGeometry(i);

            if (selectorRoot.transform.parent is RectTransform tabsRect)
            {
                float preferred = LayoutUtility.GetPreferredWidth(tabsRect);
                log.LogInfo($"Mods tab-bar check: available={tabsRect.rect.width:0.0}, preferred={preferred:0.0}.");
                if (preferred > tabsRect.rect.width + 0.1f)
                    log.LogWarning("Tab selectors exceed the native tab-bar width.");
            }
        }

        private void ResetContent()
        {
            rows.Clear();
            title = null;
            description = null;
            page.defaultSelected = null;
            initialized = false;
            for (int i = pageRoot.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(pageRoot.transform.GetChild(i).gameObject);
        }

        private GameObject NativeSectionHeader()
        {
            Transform header = screen.gamepadControlsTab.transform.Find("Player/Header");
            if (header == null)
                throw new InvalidOperationException("The native Player Controls section header was not found.");
            return header.gameObject;
        }
    }

    private sealed class SettingRow
    {
        public readonly UIMultiValueToggle Toggle;
        private readonly TextMeshProUGUI displayLabel;
        private readonly RowDefinition definition;
        private readonly Il2CppSystem.Collections.Generic.List<string> values = new();
        private int observedIndex = -1;

        public SettingRow(UIMultiValueToggle toggle, TextMeshProUGUI label, RowDefinition rowDefinition)
        {
            Toggle = toggle;
            displayLabel = label;
            definition = rowDefinition;
        }

        public void RefreshLanguage(ELanguage language, TextMeshProUGUI styleSource)
        {
            string labelText = T(definition.Key, language);
            values.Clear();
            if (definition.Type == RowType.Boolean)
            {
                values.Add(T(TextKey.Off, language));
                values.Add(T(TextKey.On, language));
            }
            else
            {
                int count = Mathf.RoundToInt((definition.Maximum - definition.Minimum) / definition.Step) + 1;
                for (int i = 0; i < count; i++)
                {
                    float value = definition.Minimum + i * definition.Step;
                    values.Add(definition.Type == RowType.Seconds
                        ? value.ToString("0.0") + " s"
                        : Mathf.RoundToInt(value).ToString());
                }
            }
            int index = CurrentIndex();
            var valueList = new Il2CppSystem.Collections.Generic.IList<string>(values.Pointer);
            Toggle.Init(valueList, index);
            Toggle.Enable();
            displayLabel.text = labelText;
            SyncVisualStyle(styleSource);
            displayLabel.ForceMeshUpdate(true, true);
            displayLabel.SetVerticesDirty();
            displayLabel.SetLayoutDirty();
            observedIndex = index;
        }

        public void SyncVisualStyle(TextMeshProUGUI source)
        {
            if (source == null || displayLabel == null) return;
            bool dirty = false;
            if (source.font != null && displayLabel.font != source.font)
            {
                displayLabel.font = source.font;
                dirty = true;
            }
            if (source.fontSharedMaterial != null && displayLabel.fontSharedMaterial != source.fontSharedMaterial)
            {
                displayLabel.fontSharedMaterial = source.fontSharedMaterial;
                dirty = true;
            }
            if (source.font != null && Toggle.itemLabel.font != source.font) Toggle.itemLabel.font = source.font;
            if (source.fontSharedMaterial != null && Toggle.itemLabel.fontSharedMaterial != source.fontSharedMaterial)
                Toggle.itemLabel.fontSharedMaterial = source.fontSharedMaterial;
            if (!Mathf.Approximately(displayLabel.fontSize, source.fontSize))
            {
                displayLabel.fontSize = source.fontSize;
                dirty = true;
            }
            displayLabel.fontStyle = source.fontStyle;
            displayLabel.color = source.color;
            displayLabel.rectTransform.localScale = Vector3.one;
            if (dirty) displayLabel.ForceMeshUpdate(true, true);
        }

        public void SyncFromConfig()
        {
            if (values.Count == 0) return;
            int index = CurrentIndex();
            Toggle.ShowItem(index);
            observedIndex = index;
        }

        public void ApplyChange()
        {
            int index = Toggle.selectedIdx;
            if (index == observedIndex) return;
            observedIndex = index;
            switch (definition.Key)
            {
                case TextKey.Enabled: ModSettings.Enabled.Value = index != 0; break;
                case TextKey.BlockWindow: ModSettings.BlockHoldDuration.Value = Value(index); break;
                case TextKey.AttackWindow: ModSettings.AttackHoldDuration.Value = Value(index); break;
                case TextKey.ComboTarget: ModSettings.SpecialComboTarget.Value = Mathf.RoundToInt(Value(index)); break;
                case TextKey.ReflectionHold: ModSettings.SpecialHoldDuration.Value = Value(index); break;
            }
        }

        public void ValidateGeometry(int index)
        {
            RectTransform row = Toggle.GetComponent<RectTransform>();
            RectTransform labelRect = displayLabel.rectTransform;
            Transform valueTransform = Toggle.transform.Find("Button/ValueContent");
            RectTransform valueRect = valueTransform?.GetComponent<RectTransform>();
            float labelRight = EdgeIn(row, labelRect, labelRect.rect.xMax);
            float valueLeft = valueRect == null ? float.NaN : EdgeIn(row, valueRect, valueRect.rect.xMin);
            float preferred = displayLabel.GetPreferredValues(displayLabel.text).x;
            log.LogInfo($"Mods row {index}: root={row.rect.width:0.0}x{row.rect.height:0.0}, " +
                $"labelRight={labelRight:0.0}, valueLeft={valueLeft:0.0}, labelPreferred={preferred:0.0}, " +
                $"labelScale={labelRect.localScale.x:0.000}, labelFont={displayLabel.fontSize:0.0}, " +
                $"value='{Toggle.itemLabel.text}'.");
            if (valueRect != null && labelRight > valueLeft + 0.1f)
                log.LogWarning($"Mods row {index} label overlaps its value control.");
        }

        private int CurrentIndex()
        {
            float current = definition.Key switch
            {
                TextKey.Enabled => ModSettings.Enabled.Value ? 1f : 0f,
                TextKey.BlockWindow => ModSettings.BlockHoldDuration.Value,
                TextKey.AttackWindow => ModSettings.AttackHoldDuration.Value,
                TextKey.ComboTarget => ModSettings.SpecialComboTarget.Value,
                TextKey.ReflectionHold => ModSettings.SpecialHoldDuration.Value,
                _ => definition.Minimum
            };
            if (definition.Type == RowType.Boolean) return Mathf.RoundToInt(current);
            return Mathf.Clamp(Mathf.RoundToInt((current - definition.Minimum) / definition.Step),
                0, Math.Max(0, values.Count - 1));
        }

        private float Value(int index) => definition.Minimum + index * definition.Step;
    }

    private readonly struct RowDefinition
    {
        public readonly TextKey Key;
        public readonly RowType Type;
        public readonly float Minimum;
        public readonly float Maximum;
        public readonly float Step;

        public RowDefinition(TextKey key, RowType type, float minimum, float maximum, float step)
        {
            Key = key;
            Type = type;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
        }
    }

    private enum RowType { Boolean, Seconds, Integer }
    private enum TextKey { Mods, Title, Description, Enabled, BlockWindow, AttackWindow, ReflectionWindow, BlockHold, ComboTarget, ReflectionHold, On, Off }

    private static ELanguage CurrentLanguage()
    {
        try { return LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : ELanguage.EN; }
        catch { return ELanguage.EN; }
    }

    private static string T(TextKey key, ELanguage language)
    {
        if (language == ELanguage.RU) return key switch
        {
            TextKey.Mods => "\u041c\u043e\u0434\u044b", TextKey.Title => "\u041f\u0410\u0420\u0418\u0420\u041e\u0412\u0410\u041d\u0418\u042f \u0418 \u0411\u041b\u041e\u041a\u0418", TextKey.Enabled => "\u0412\u043a\u043b\u044e\u0447\u0435\u043d\u043e",
            TextKey.Description => "\u0411\u043b\u043e\u043a \u0438 \u0434\u043e\u043f. \u0430\u0442\u0430\u043a\u0430 \u0441\u0440\u0430\u0431\u0430\u0442\u044b\u0432\u0430\u044e\u0442 \u043f\u0440\u0438 \u0443\u0434\u0435\u0440\u0436\u0430\u043d\u0438\u0438 \u043d\u0435 \u0434\u043e\u043b\u044c\u0448\u0435 \u0432\u044b\u0431\u0440\u0430\u043d\u043d\u043e\u0433\u043e \u0432\u0440\u0435\u043c\u0435\u043d\u0438. \u041f\u043e\u0441\u043b\u0435 \u043f\u043e\u0440\u043e\u0433\u0430 \u043a\u043e\u043c\u0431\u043e \u0443\u0434\u0435\u0440\u0436\u0430\u043d\u0438\u0435 \u0437\u0430\u0432\u0435\u0440\u0448\u0430\u0435\u0442 \u0441\u043f\u0435\u0446\u0430\u0442\u0430\u043a\u0443.",
            TextKey.BlockWindow => "\u0411\u043b\u043e\u043a", TextKey.AttackWindow => "\u0414\u043e\u043f. \u0430\u0442\u0430\u043a\u0430",
            TextKey.ComboTarget => "\u041f\u043e\u0440\u043e\u0433 \u043a\u043e\u043c\u0431\u043e", TextKey.ReflectionHold => "\u0417\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0438\u0435 \u0441\u043f\u0435\u0446\u0430\u0442\u0430\u043a\u0438",
            TextKey.On => "\u0414\u0430", TextKey.Off => "\u041d\u0435\u0442", _ => key.ToString()
        };
        if (language == ELanguage.FR || language == ELanguage.QC) return key switch
        {
            TextKey.Mods => "Mods", TextKey.Title => "PARADES ET BLOCAGES", TextKey.Enabled => "Activé",
            TextKey.Description => "Le blocage et l’attaque supplémentaire fonctionnent en maintenant la touche pendant la durée choisie au maximum. Après le seuil du combo, maintenir termine l’attaque spéciale.",
            TextKey.BlockWindow => "Blocage", TextKey.AttackWindow => "Attaque supplémentaire",
            TextKey.ComboTarget => "Seuil du combo", TextKey.ReflectionHold => "Fin de l’attaque spéciale",
            TextKey.On => "Oui", TextKey.Off => "Non", _ => key.ToString()
        };
        if (language == ELanguage.DE) return key switch
        {
            TextKey.Mods => "Mods", TextKey.Title => "PARADEN UND BLOCKEN", TextKey.Enabled => "Aktiviert",
            TextKey.Description => "Block und Zusatzangriff funktionieren beim Halten höchstens für die gewählte Zeit. Nach der Kombo-Schwelle schließt Halten den Spezialangriff ab.",
            TextKey.BlockWindow => "Block", TextKey.AttackWindow => "Zusatzangriff",
            TextKey.ComboTarget => "Kombo-Schwelle", TextKey.ReflectionHold => "Spezialangriff-Abschluss",
            TextKey.On => "Ein", TextKey.Off => "Aus", _ => key.ToString()
        };
        if (language == ELanguage.ES) return key switch
        {
            TextKey.Mods => "Mods", TextKey.Title => "PARADAS Y BLOQUEOS", TextKey.Enabled => "Activado",
            TextKey.Description => "El bloqueo y el ataque adicional funcionan al mantener el botón durante el tiempo seleccionado como máximo. Tras el umbral de combo, mantener completa el ataque especial.",
            TextKey.BlockWindow => "Bloqueo", TextKey.AttackWindow => "Ataque adicional",
            TextKey.ComboTarget => "Umbral de combo", TextKey.ReflectionHold => "Final de ataque especial",
            TextKey.On => "Sí", TextKey.Off => "No", _ => key.ToString()
        };
        if (language == ELanguage.IT) return key switch
        {
            TextKey.Mods => "Mod", TextKey.Title => "PARATE E BLOCCHI", TextKey.Enabled => "Attivo",
            TextKey.Description => "Il blocco e l’attacco aggiuntivo funzionano tenendo premuto per non più del tempo scelto. Dopo la soglia combo, tenere premuto completa l’attacco speciale.",
            TextKey.BlockWindow => "Blocco", TextKey.AttackWindow => "Attacco aggiuntivo",
            TextKey.ComboTarget => "Soglia combo", TextKey.ReflectionHold => "Fine attacco speciale",
            TextKey.On => "Sì", TextKey.Off => "No", _ => key.ToString()
        };
        if (language == ELanguage.ptBR) return key switch
        {
            TextKey.Mods => "Mods", TextKey.Title => "APAROS E BLOQUEIOS", TextKey.Enabled => "Ativado",
            TextKey.Description => "O bloqueio e o ataque adicional funcionam ao segurar o botão por até o tempo escolhido. Após o limite do combo, segurar conclui o ataque especial.",
            TextKey.BlockWindow => "Bloqueio", TextKey.AttackWindow => "Ataque adicional",
            TextKey.ComboTarget => "Limite do combo", TextKey.ReflectionHold => "Conclusão do ataque especial",
            TextKey.On => "Sim", TextKey.Off => "Não", _ => key.ToString()
        };
        if (language == ELanguage.JP) return key switch
        {
            TextKey.Mods => "MOD", TextKey.Title => "パリィとブロック", TextKey.Enabled => "有効",
            TextKey.Description => "ブロックと追加攻撃は、設定した時間までボタン長押しで作動します。コンボ条件達成後は、長押しで特殊攻撃を完了します。",
            TextKey.BlockWindow => "ブロック", TextKey.AttackWindow => "追加攻撃",
            TextKey.ComboTarget => "コンボ条件", TextKey.ReflectionHold => "特殊攻撃完了",
            TextKey.On => "オン", TextKey.Off => "オフ", _ => key.ToString()
        };
        if (language == ELanguage.KO) return key switch
        {
            TextKey.Mods => "모드", TextKey.Title => "패리와 방어", TextKey.Enabled => "사용",
            TextKey.Description => "방어와 추가 공격은 설정한 시간까지만 버튼을 누르고 있을 때 작동합니다. 콤보 조건 달성 후에는 길게 눌러 특수 공격을 완료합니다.",
            TextKey.BlockWindow => "방어", TextKey.AttackWindow => "추가 공격",
            TextKey.ComboTarget => "콤보 조건", TextKey.ReflectionHold => "특수 공격 완료",
            TextKey.On => "켜기", TextKey.Off => "끄기", _ => key.ToString()
        };
        if (language == ELanguage.zhCN) return key switch
        {
            TextKey.Mods => "模组", TextKey.Title => "格挡与招架", TextKey.Enabled => "启用",
            TextKey.Description => "格挡和追加攻击可通过按住按钮触发，最长不超过设定时间。达到连击门槛后，按住按钮即可完成特殊攻击。",
            TextKey.BlockWindow => "格挡", TextKey.AttackWindow => "追加攻击",
            TextKey.ComboTarget => "连击门槛", TextKey.ReflectionHold => "特殊攻击收尾",
            TextKey.On => "开", TextKey.Off => "关", _ => key.ToString()
        };
        if (language == ELanguage.zhHK) return key switch
        {
            TextKey.Mods => "模組", TextKey.Title => "格擋與招架", TextKey.Enabled => "啟用",
            TextKey.Description => "格擋和額外攻擊可透過按住按鈕觸發，最長不超過設定時間。達到連擊門檻後，按住按鈕即可完成特殊攻擊。",
            TextKey.BlockWindow => "格擋", TextKey.AttackWindow => "額外攻擊",
            TextKey.ComboTarget => "連擊門檻", TextKey.ReflectionHold => "特殊攻擊收尾",
            TextKey.On => "開", TextKey.Off => "關", _ => key.ToString()
        };
        return key switch
        {
            TextKey.Mods => "Mods", TextKey.Title => "PARRIES AND BLOCKS", TextKey.Enabled => "Enabled",
            TextKey.Description => "Block and additional attack work while held for no longer than the selected time. After the combo threshold, holding completes the special attack.",
            TextKey.BlockWindow => "Block", TextKey.AttackWindow => "Additional attack",
            TextKey.ComboTarget => "Combo threshold", TextKey.ReflectionHold => "Special attack completion",
            TextKey.On => "On", TextKey.Off => "Off", _ => key.ToString()
        };
    }

    private static void CopyVerticalLayout(GameObject source, GameObject target)
    {
        VerticalLayoutGroup native = source.GetComponent<VerticalLayoutGroup>();
        var layout = target.AddComponent<VerticalLayoutGroup>();
        if (native != null)
        {
            layout.padding = native.padding;
            layout.childAlignment = native.childAlignment;
            layout.spacing = native.spacing;
            layout.childControlWidth = native.childControlWidth;
            layout.childControlHeight = native.childControlHeight;
            layout.childForceExpandWidth = native.childForceExpandWidth;
            layout.childForceExpandHeight = native.childForceExpandHeight;
            layout.childScaleWidth = native.childScaleWidth;
            layout.childScaleHeight = native.childScaleHeight;
            layout.reverseArrangement = native.reverseArrangement;
            return;
        }
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 0f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static TextMeshProUGUI CreateTitle(GameObject source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source, parent);
        clone.name = "Mod section header";
        DisableLocalizers(clone);
        TextMeshProUGUI text = FirstText(clone);
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI CreateDescription(GameObject source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source, parent);
        clone.name = "Mod description";
        DisableLocalizers(clone);
        clone.SetActive(true);
        TextMeshProUGUI text = FirstText(clone);
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI CreateRowLabel(TextMeshProUGUI source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source.gameObject, parent);
        clone.name = "Mod setting label";
        DisableLocalizers(clone);
        TextMeshProUGUI text = clone.GetComponent<TextMeshProUGUI>();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(9f, 0f);
        rect.sizeDelta = new Vector2(160f, 0f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        if (source == null || target == null) return;
        if (source.font != null) target.font = source.font;
        if (source.fontSharedMaterial != null) target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontStyle = source.fontStyle;
        target.color = source.color;
    }

    private static float EdgeIn(RectTransform ancestor, RectTransform child, float localX)
    {
        Vector3 world = child.TransformPoint(new Vector3(localX, 0f, 0f));
        return ancestor.InverseTransformPoint(world).x;
    }

    private static void DisableLocalizers(GameObject root)
    {
        foreach (var localizer in root.GetComponentsInChildren<TextLocalizer>(true)) localizer.enabled = false;
        foreach (var localizer in root.GetComponentsInChildren<ImageLocalizer>(true)) localizer.enabled = false;
    }

    private static TextMeshProUGUI FirstText(GameObject root)
    {
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts.Length == 0) throw new InvalidOperationException("Tab selector does not contain a label.");
        return texts[0];
    }

    private static GameObject NewRectObject(string name, Transform parent)
    {
        var result = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        result.layer = 5;
        result.transform.SetParent(parent, false);
        result.transform.localScale = Vector3.one;
        return result;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }
}
