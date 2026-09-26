using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

public static class HoldAssist
{
    private static ManualLogSource log;
    private static readonly Dictionary<IntPtr, float> holdStarts = new();
    private static readonly HashSet<IntPtr> activeBlocks = new();
    private static readonly HashSet<IntPtr> activeMoonrangs = new();
    private static readonly HashSet<IntPtr> activeLimbo = new();
    private static readonly HashSet<IntPtr> activeSppLimbo = new();
    private static readonly HashSet<IntPtr> activeKicks = new();
    private static readonly HashSet<IntPtr> activeSppKicks = new();
    private static readonly Dictionary<string, int> messages = new();
    private static SpecialSession special;

    public static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;

        Patch(harmony, typeof(AbstractTimedAttackHandler), "BeginInputPhase", Type.EmptyTypes,
            postfix: nameof(AttackBegin));
        Patch(harmony, typeof(AbstractTimedAttackHandler), "IUpdatableUpdate", Type.EmptyTypes,
            prefix: nameof(AttackUpdate));
        Patch(harmony, typeof(AbstractTimedAttackHandler), "CanAutoTimeHit",
            new[] { typeof(PlayerCombatMoveDefinition) }, prefix: nameof(AutoAttack));
        Patch(harmony, typeof(AbstractTimedAttackHandler), "EndInputPhase", Type.EmptyTypes,
            prefix: nameof(AttackEnd));
        Patch(harmony, typeof(AbstractTimedAttackHandler), "ResetInput", Type.EmptyTypes,
            prefix: nameof(AttackEnd));
        Patch(harmony, typeof(AbstractTimedAttackHandler), "OnDestroy", Type.EmptyTypes,
            prefix: nameof(AttackEnd));

        Patch(harmony, typeof(TimedBlockHandler), "BeginInputPhase", Type.EmptyTypes,
            postfix: nameof(BlockBegin));
        Patch(harmony, typeof(TimedBlockHandler), "IUpdatableUpdate", Type.EmptyTypes,
            prefix: nameof(BlockUpdate));
        Patch(harmony, typeof(TimedBlockHandler), "GetResult", Type.EmptyTypes,
            prefix: nameof(BlockResult), finalizer: nameof(RestoreBlockResult));
        Patch(harmony, typeof(TimedBlockHandler), "EndInputPhase", Type.EmptyTypes,
            prefix: nameof(BlockEnd));
        Patch(harmony, typeof(TimedBlockHandler), "ResetInput", Type.EmptyTypes,
            prefix: nameof(BlockEnd));
        Patch(harmony, typeof(TimedBlockHandler), "OnDestroy", Type.EmptyTypes,
            prefix: nameof(BlockEnd));
        Patch(harmony, typeof(TimedBlockHandler), "SetInputEnabled", new[] { typeof(bool) },
            prefix: nameof(BlockInputEnabled));

        Type[] moveArguments =
        {
            typeof(Il2CppSystem.Action<CombatMove>), typeof(Il2CppSystem.Action<CombatMove>)
        };
        Patch(harmony, typeof(MoonrangSpecialMove), "DoMove", moveArguments,
            prefix: nameof(MoonrangStart));
        Patch(harmony, typeof(Soonrang), "DoMove", moveArguments,
            prefix: nameof(MoonrangStart));
        Patch(harmony, typeof(MoonrangSpecialMove), "OnProjectileReachedPlayer",
            new[] { typeof(MoonrangProjectile) }, prefix: nameof(MoonrangReachedPrefix),
            postfix: nameof(MoonrangReachedPostfix));
        Patch(harmony, typeof(DeflectMoonrangState), "StateExecute", Type.EmptyTypes,
            prefix: nameof(MoonrangUpdate));
        Patch(harmony, typeof(MoonrangSpecialMove), "OnDeflectFailed",
            new[] { typeof(MoonrangProjectile) }, prefix: nameof(MoonrangFailed));
        Patch(harmony, typeof(MoonrangSpecialMove), "OnFinalHitApplied", Type.EmptyTypes,
            postfix: nameof(MoonrangEnd));
        Patch(harmony, typeof(Soonrang), "OnFinalHitApplied", Type.EmptyTypes,
            postfix: nameof(MoonrangEnd));
        Patch(harmony, typeof(MoonrangSpecialMove), "Unload", Type.EmptyTypes,
            prefix: nameof(MoonrangEnd));

        InstallLimbo(harmony);
        InstallPotionKick(harmony);
        InstallCounterMoves(harmony, moveArguments);

        Patch(harmony, typeof(ReshanBasicAttack), "DoMove", moveArguments,
            postfix: nameof(ReshanStart));
        Patch(harmony, typeof(ReshanBasicAttack), "LateUpdate", Type.EmptyTypes,
            prefix: nameof(ReshanUpdate));
        Patch(harmony, typeof(ReshanBasicAttack), "CanAutoTimeHit", Type.EmptyTypes,
            prefix: nameof(ReshanAuto));
        Patch(harmony, typeof(ReshanBasicAttack), "WouldThrowingNowBeATimedHit", Type.EmptyTypes,
            prefix: nameof(ReshanAuto));
        Patch(harmony, typeof(ReshanBasicAttack), "HasQTESuccess", Type.EmptyTypes,
            prefix: nameof(ReshanAuto), postfix: nameof(ReshanSuccess));
        Patch(harmony, typeof(ReshanBasicAttack), "OnMoveDone", Type.EmptyTypes,
            postfix: nameof(ReshanEnd));
        Patch(harmony, typeof(ReshanBasicAttack), "Unload", Type.EmptyTypes,
            prefix: nameof(ReshanEnd));

        log.LogInfo("Bounded hold automation installed for blocks, normal attacks and every counter-mode special move.");
    }

    private static void InstallLimbo(Harmony harmony)
    {
        Patch(harmony, typeof(LimboDeflectState), "StateEnter", Type.EmptyTypes,
            postfix: nameof(LimboStart));
        Patch(harmony, typeof(LimboDeflectState), "StateExecute", Type.EmptyTypes,
            prefix: nameof(LimboUpdate));
        Patch(harmony, typeof(LimboDeflectState), "OnDeflectInput", Type.EmptyTypes,
            prefix: nameof(LimboSuccessPrefix), postfix: nameof(LimboSuccess));
        Patch(harmony, typeof(LimboDeflectState), "StateExit", Type.EmptyTypes,
            prefix: nameof(LimboEnd));

        Patch(harmony, typeof(SPPPlayerLimboDeflectState), "StateEnter", Type.EmptyTypes,
            postfix: nameof(SppLimboStart));
        Patch(harmony, typeof(SPPPlayerLimboDeflectState), "StateExecute", Type.EmptyTypes,
            prefix: nameof(SppLimboUpdate));
        Patch(harmony, typeof(SPPPlayerLimboDeflectState), "OnDeflectInput", Type.EmptyTypes,
            prefix: nameof(SppLimboSuccessPrefix), postfix: nameof(SppLimboSuccess));
        Patch(harmony, typeof(SPPPlayerLimboDeflectState), "StateExit", Type.EmptyTypes,
            prefix: nameof(SppLimboEnd));
    }

    private static void InstallPotionKick(Harmony harmony)
    {
        Patch(harmony, typeof(KickPotionState), "StateEnter", Type.EmptyTypes,
            postfix: nameof(KickStart));
        Patch(harmony, typeof(KickPotionState), "StateExecute", Type.EmptyTypes,
            prefix: nameof(KickUpdate));
        Patch(harmony, typeof(KickPotionState), "OnKickInput", Type.EmptyTypes,
            prefix: nameof(KickSuccessPrefix), postfix: nameof(KickSuccess));
        Patch(harmony, typeof(KickPotionState), "StateExit", Type.EmptyTypes,
            prefix: nameof(KickEnd));

        Patch(harmony, typeof(SPPPlayerPotionKickState), "StateEnter", Type.EmptyTypes,
            postfix: nameof(SppKickStart));
        Patch(harmony, typeof(SPPPlayerPotionKickState), "StateExecute", Type.EmptyTypes,
            prefix: nameof(SppKickUpdate));
        Patch(harmony, typeof(SPPPlayerPotionKickState), "OnKickInput", Type.EmptyTypes,
            prefix: nameof(SppKickSuccessPrefix), postfix: nameof(SppKickSuccess));
        Patch(harmony, typeof(SPPPlayerPotionKickState), "StateExit", Type.EmptyTypes,
            prefix: nameof(SppKickEnd));
    }

    private static void InstallCounterMoves(Harmony harmony, Type[] moveArguments)
    {
        InstallCounterMove(harmony, typeof(ArtificerLeapFrog), moveArguments,
            "OnTimedHitResult", "OnMoveDone");
        InstallCounterMove(harmony, typeof(ValereCombatMoveTrapeze), moveArguments,
            "OnQTEResult", "OnMoveDone");
        InstallCounterMove(harmony, typeof(SeraiFanOfKnives), moveArguments,
            "OnQTEResult", "OnMoveDone");
        InstallCounterMove(harmony, typeof(SunboyJugglenaut), moveArguments,
            "OnQTEResult", "OnProjectilesDone");
    }

    private static void InstallCounterMove(Harmony harmony, Type type, Type[] moveArguments,
        string resultMethod, string endMethod)
    {
        Patch(harmony, type, "DoMove", moveArguments, prefix: nameof(CounterMoveStart));
        Patch(harmony, type, resultMethod, new[] { typeof(TeamQTEResult) },
            prefix: nameof(CounterMoveResult));
        Patch(harmony, type, endMethod, Type.EmptyTypes, postfix: nameof(CounterMoveEnd));
        Patch(harmony, type, "Unload", Type.EmptyTypes, prefix: nameof(CounterMoveEnd));
    }

    private static void Patch(Harmony harmony, Type type, string methodName, Type[] argumentTypes,
        string prefix = null, string postfix = null, string finalizer = null)
    {
        var method = AccessTools.DeclaredMethod(type, methodName, argumentTypes)
            ?? throw new MissingMethodException(type.Name, methodName);
        harmony.Patch(method,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(HoldAssist), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(HoldAssist), postfix),
            finalizer: finalizer == null ? null : new HarmonyMethod(typeof(HoldAssist), finalizer));
        log.LogInfo($"Hold hook: {type.Name}.{methodName}");
    }

    private static bool Held(PlayerInputs inputs)
        => ModSettings.Enabled.Value && inputs != null && inputs.GetButton("Interact");

    private static bool BaseHoldAllowed(PlayerInputs inputs, IntPtr key, float duration)
    {
        if (!Held(inputs))
        {
            holdStarts.Remove(key);
            return false;
        }

        if (!holdStarts.TryGetValue(key, out float started))
        {
            started = Time.time;
            holdStarts[key] = started;
        }
        return Time.time - started <= Math.Max(0.1f, duration);
    }

    private static bool SpecialHoldAllowed(PlayerInputs inputs, IntPtr key, SpecialKind kind, IntPtr owner)
    {
        if (special != null && special.Kind == kind && special.Owner == owner && special.Unlocked)
        {
            if (!Held(inputs))
            {
                holdStarts.Remove(key);
                return false;
            }
            return Time.time <= special.AutomationEnd;
        }
        return BaseHoldAllowed(inputs, key, ModSettings.AttackHoldDuration.Value);
    }

    private static void AttackBegin(AbstractTimedAttackHandler __instance)
        => holdStarts.Remove(__instance.Pointer);

    private static void AttackUpdate(AbstractTimedAttackHandler __instance)
    {
        if (__instance.IsInputPhaseStarted())
            CurrentAttackHoldAllowed(__instance);
        else
            holdStarts.Remove(__instance.Pointer);
    }

    private static bool AutoAttack(AbstractTimedAttackHandler __instance, ref bool __result)
    {
        if (!CurrentAttackHoldAllowed(__instance))
            return true;
        __result = true;
        Report("attack", special == null
            ? "normal timed attack accepted while held"
            : special.Kind + " timed input accepted while held");
        return false;
    }

    private static bool CurrentAttackHoldAllowed(AbstractTimedAttackHandler handler)
    {
        if (special == null)
            return BaseHoldAllowed(handler.playerInputs, handler.Pointer, ModSettings.AttackHoldDuration.Value);
        return SpecialHoldAllowed(handler.playerInputs, handler.Pointer, special.Kind, special.Owner);
    }

    private static void AttackEnd(AbstractTimedAttackHandler __instance)
        => holdStarts.Remove(__instance.Pointer);

    private static void BlockBegin(TimedBlockHandler __instance)
    {
        holdStarts.Remove(__instance.Pointer);
        activeBlocks.Remove(__instance.Pointer);
    }

    private static void BlockUpdate(TimedBlockHandler __instance)
    {
        IntPtr key = __instance.Pointer;
        bool allowed = __instance.IsInputPhaseStarted() &&
            BaseHoldAllowed(__instance.playerInputs, key, ModSettings.BlockHoldDuration.Value);
        if (allowed)
        {
            if (!__instance.playingBlockAnimation) __instance.DoBlock();
            __instance.blocking = true;
            __instance.blockEndTime = Time.time + 0.1f;
            __instance.blockAnimationEndTime = Time.time + 0.1f;
            if (activeBlocks.Add(key)) Report("block", "automatic block active while held");
            return;
        }

        if (activeBlocks.Remove(key)) __instance.EndBlock();
    }

    private static void BlockResult(TimedBlockHandler __instance, out bool? __state)
    {
        __state = null;
        if (!BaseHoldAllowed(__instance.playerInputs, __instance.Pointer, ModSettings.BlockHoldDuration.Value)) return;
        __state = __instance.blocking;
        __instance.blocking = true;
    }

    private static void RestoreBlockResult(TimedBlockHandler __instance, bool? __state)
    {
        if (__state.HasValue) __instance.blocking = __state.Value;
    }

    private static void BlockEnd(TimedBlockHandler __instance)
    {
        holdStarts.Remove(__instance.Pointer);
        activeBlocks.Remove(__instance.Pointer);
    }

    private static void BlockInputEnabled(TimedBlockHandler __instance, bool __0)
    {
        if (!__0) BlockEnd(__instance);
    }

    private static void MoonrangStart(MoonrangSpecialMove __instance)
        => BeginSpecial(SpecialKind.Moonrang, __instance.Pointer);

    private static void MoonrangReachedPrefix(MoonrangProjectile __0, out int __state)
        => __state = __0 == null ? -1 : __0.BounceCount;

    private static void MoonrangReachedPostfix(MoonrangSpecialMove __instance,
        MoonrangProjectile __0, int __state)
    {
        if (__state >= 0 && __0 != null && __0.BounceCount > __state)
            RegisterSuccess(SpecialKind.Moonrang, __instance.Pointer);
    }

    private static void MoonrangUpdate(DeflectMoonrangState __instance)
    {
        if (special == null || special.Kind != SpecialKind.Moonrang || __instance.combatActor == null) return;
        bool allowed = __instance.deflectEnabled && SpecialHoldAllowed(__instance.combatActor.PlayerInputs,
            __instance.Pointer, SpecialKind.Moonrang, special.Owner);
        if (allowed)
        {
            if (!__instance.playingDeflectAnimation) __instance.OnDeflectInput();
            __instance.deflecting = true;
            __instance.deflectEndTime = Time.time + 0.1f;
            __instance.deflectAnimationEndTime = Time.time + 0.1f;
            activeMoonrangs.Add(__instance.Pointer);
        }
        else if (activeMoonrangs.Remove(__instance.Pointer))
        {
            __instance.EndDeflect();
        }
        UpdateHud();
    }

    private static void MoonrangFailed(MoonrangSpecialMove __instance)
    {
        if (Matches(SpecialKind.Moonrang, __instance.Pointer)) ResetCombo();
    }

    private static void MoonrangEnd(MoonrangSpecialMove __instance)
        => EndSpecial(SpecialKind.Moonrang, __instance.Pointer);

    private static void LimboStart(LimboDeflectState __instance)
        => BeginSpecial(SpecialKind.Limbo, __instance.Pointer);

    private static void LimboUpdate(LimboDeflectState __instance)
    {
        if (__instance.combatActor == null || !Matches(SpecialKind.Limbo, __instance.Pointer)) return;
        bool allowed = SpecialHoldAllowed(__instance.combatActor.PlayerInputs, __instance.Pointer,
            SpecialKind.Limbo, __instance.Pointer);
        if (allowed)
        {
            if (!__instance.deflecting) __instance.OnDeflectInput();
            __instance.deflecting = true;
            __instance.deflectEndTime = Time.time + 0.1f;
            activeLimbo.Add(__instance.Pointer);
        }
        else if (activeLimbo.Remove(__instance.Pointer)) __instance.EndDeflect();
        UpdateHud();
    }

    private static void LimboSuccessPrefix(LimboDeflectState __instance, out bool __state)
        => __state = __instance.deflecting;

    private static void LimboSuccess(LimboDeflectState __instance, bool __state)
    {
        if (!__state && __instance.deflecting) RegisterSuccess(SpecialKind.Limbo, __instance.Pointer);
    }

    private static void LimboEnd(LimboDeflectState __instance)
    {
        activeLimbo.Remove(__instance.Pointer);
        EndSpecial(SpecialKind.Limbo, __instance.Pointer);
    }

    private static void SppLimboStart(SPPPlayerLimboDeflectState __instance)
        => BeginSpecial(SpecialKind.Limbo, __instance.Pointer);

    private static void SppLimboUpdate(SPPPlayerLimboDeflectState __instance)
    {
        PlayerInputs inputs = __instance.owner?.blockHandler?.playerInputs;
        if (inputs == null || !Matches(SpecialKind.Limbo, __instance.Pointer)) return;
        bool allowed = SpecialHoldAllowed(inputs, __instance.Pointer,
            SpecialKind.Limbo, __instance.Pointer);
        if (allowed)
        {
            if (!__instance.deflecting) __instance.OnDeflectInput();
            __instance.deflecting = true;
            __instance.deflectEndTime = Time.time + 0.1f;
            activeSppLimbo.Add(__instance.Pointer);
        }
        else if (activeSppLimbo.Remove(__instance.Pointer)) __instance.EndDeflect();
        UpdateHud();
    }

    private static void SppLimboSuccessPrefix(SPPPlayerLimboDeflectState __instance, out bool __state)
        => __state = __instance.deflecting;

    private static void SppLimboSuccess(SPPPlayerLimboDeflectState __instance, bool __state)
    {
        if (!__state && __instance.deflecting) RegisterSuccess(SpecialKind.Limbo, __instance.Pointer);
    }

    private static void SppLimboEnd(SPPPlayerLimboDeflectState __instance)
    {
        activeSppLimbo.Remove(__instance.Pointer);
        EndSpecial(SpecialKind.Limbo, __instance.Pointer);
    }

    private static void KickStart(KickPotionState __instance)
        => BeginSpecial(SpecialKind.PotionKick, __instance.Pointer);

    private static void KickUpdate(KickPotionState __instance)
    {
        if (__instance.combatActor == null || !Matches(SpecialKind.PotionKick, __instance.Pointer)) return;
        bool allowed = SpecialHoldAllowed(__instance.combatActor.PlayerInputs, __instance.Pointer,
            SpecialKind.PotionKick, __instance.Pointer);
        if (allowed)
        {
            if (!__instance.kicking) __instance.OnKickInput();
            __instance.kicking = true;
            __instance.kickEndTime = Time.time + 0.1f;
            activeKicks.Add(__instance.Pointer);
        }
        else if (activeKicks.Remove(__instance.Pointer)) __instance.EndKick();
        UpdateHud();
    }

    private static void KickSuccessPrefix(KickPotionState __instance, out bool __state)
        => __state = __instance.kicking;

    private static void KickSuccess(KickPotionState __instance, bool __state)
    {
        if (!__state && __instance.kicking) RegisterSuccess(SpecialKind.PotionKick, __instance.Pointer);
    }

    private static void KickEnd(KickPotionState __instance)
    {
        activeKicks.Remove(__instance.Pointer);
        EndSpecial(SpecialKind.PotionKick, __instance.Pointer);
    }

    private static void SppKickStart(SPPPlayerPotionKickState __instance)
        => BeginSpecial(SpecialKind.PotionKick, __instance.Pointer);

    private static void SppKickUpdate(SPPPlayerPotionKickState __instance)
    {
        PlayerInputs inputs = __instance.owner?.blockHandler?.playerInputs;
        if (inputs == null || !Matches(SpecialKind.PotionKick, __instance.Pointer)) return;
        bool allowed = SpecialHoldAllowed(inputs, __instance.Pointer,
            SpecialKind.PotionKick, __instance.Pointer);
        if (allowed)
        {
            if (!__instance.kicking) __instance.OnKickInput();
            __instance.kicking = true;
            __instance.kickEndTime = Time.time + 0.1f;
            activeSppKicks.Add(__instance.Pointer);
        }
        else if (activeSppKicks.Remove(__instance.Pointer)) __instance.EndKick();
        UpdateHud();
    }

    private static void SppKickSuccessPrefix(SPPPlayerPotionKickState __instance, out bool __state)
        => __state = __instance.kicking;

    private static void SppKickSuccess(SPPPlayerPotionKickState __instance, bool __state)
    {
        if (!__state && __instance.kicking) RegisterSuccess(SpecialKind.PotionKick, __instance.Pointer);
    }

    private static void SppKickEnd(SPPPlayerPotionKickState __instance)
    {
        activeSppKicks.Remove(__instance.Pointer);
        EndSpecial(SpecialKind.PotionKick, __instance.Pointer);
    }

    private static void ReshanStart(ReshanBasicAttack __instance)
        => BeginSpecial(SpecialKind.Reshan, __instance.Pointer);

    private static void ReshanUpdate(ReshanBasicAttack __instance)
    {
        if (!Matches(SpecialKind.Reshan, __instance.Pointer) || __instance.reshanActor == null) return;
        SpecialHoldAllowed(__instance.reshanActor.PlayerInputs, __instance.Pointer,
            SpecialKind.Reshan, __instance.Pointer);
        UpdateHud();
    }

    private static bool ReshanAuto(ReshanBasicAttack __instance, ref bool __result)
    {
        if (__instance.reshanActor == null || !Matches(SpecialKind.Reshan, __instance.Pointer) ||
            !SpecialHoldAllowed(__instance.reshanActor.PlayerInputs, __instance.Pointer,
                SpecialKind.Reshan, __instance.Pointer)) return true;
        __result = true;
        return false;
    }

    private static void ReshanSuccess(ReshanBasicAttack __instance, bool __result)
    {
        if (!__result || !Matches(SpecialKind.Reshan, __instance.Pointer)) return;
        int token = __instance.hitCount;
        if (special.LastSuccessToken == token) return;
        special.LastSuccessToken = token;
        RegisterSuccess(SpecialKind.Reshan, __instance.Pointer);
    }

    private static void ReshanEnd(ReshanBasicAttack __instance)
        => EndSpecial(SpecialKind.Reshan, __instance.Pointer);

    private static void CounterMoveStart(PlayerCombatMove __instance)
    {
        if (TryGetCounterKind(__instance, out SpecialKind kind))
            BeginSpecial(kind, __instance.Pointer);
    }

    private static void CounterMoveResult(PlayerCombatMove __instance, TeamQTEResult __0)
    {
        if (TryGetCounterKind(__instance, out SpecialKind kind))
            RegisterQteResult(kind, __instance.Pointer, __0);
    }

    private static void CounterMoveEnd(PlayerCombatMove __instance)
    {
        if (TryGetCounterKind(__instance, out SpecialKind kind))
            EndSpecial(kind, __instance.Pointer);
    }

    private static void RegisterQteResult(SpecialKind kind, IntPtr owner, TeamQTEResult result)
    {
        if (!Matches(kind, owner) || result == null) return;
        if (result.HasSuccess())
            RegisterSuccess(kind, owner);
        else
            ResetCombo();
    }

    private static bool TryGetCounterKind(PlayerCombatMove move, out SpecialKind kind)
    {
        if (move is ArtificerLeapFrog) kind = SpecialKind.LeapFrog;
        else if (move is ValereCombatMoveTrapeze) kind = SpecialKind.Trapeze;
        else if (move is SeraiFanOfKnives) kind = SpecialKind.VenomFlurry;
        else if (move is SunboyJugglenaut) kind = SpecialKind.Jugglenaut;
        else
        {
            kind = default;
            return false;
        }
        return true;
    }

    private static void BeginSpecial(SpecialKind kind, IntPtr owner)
    {
        if (Matches(kind, owner)) return;
        ClearSpecial();
        if (!ModSettings.Enabled.Value) return;
        special = new SpecialSession(kind, owner);
        ComboHud.Show(0, false);
        Report("special-start-" + kind, kind + " combo started");
    }

    private static bool Matches(SpecialKind kind, IntPtr owner)
        => special != null && special.Kind == kind && special.Owner == owner;

    private static void RegisterSuccess(SpecialKind kind, IntPtr owner)
    {
        if (!Matches(kind, owner) || !ModSettings.Enabled.Value) return;
        special.Count++;
        int target = Math.Max(1, ModSettings.SpecialComboTarget.Value);
        bool pulse = false;
        if (!special.Unlocked && special.Count >= target)
        {
            special.Unlocked = true;
            special.AutomationEnd = Time.time + ModSettings.SpecialHoldDuration.Value;
            pulse = true;
            Report("special-unlock-" + kind,
                $"{kind} extended hold unlocked at X {special.Count} for {ModSettings.SpecialHoldDuration.Value:0.0}s");
        }
        ComboHud.Show(DisplayCount(), special.Unlocked, pulse);
    }

    private static void ResetCombo()
    {
        if (special == null) return;
        special.Count = 0;
        special.Unlocked = false;
        special.AutomationEnd = 0f;
        special.LastSuccessToken = int.MinValue;
        ComboHud.Show(0, false);
    }

    private static void UpdateHud()
    {
        if (special == null) return;
        ComboHud.Update(DisplayCount(), special.Unlocked && Time.time <= special.AutomationEnd);
    }

    private static int DisplayCount()
        => special == null ? 0 : Math.Min(special.Count, Math.Max(1, ModSettings.SpecialComboTarget.Value));

    private static void EndSpecial(SpecialKind kind, IntPtr owner)
    {
        if (Matches(kind, owner)) ClearSpecial();
    }

    private static void ClearSpecial()
    {
        if (special != null) holdStarts.Remove(special.Owner);
        special = null;
        activeMoonrangs.Clear();
        activeLimbo.Clear();
        activeSppLimbo.Clear();
        activeKicks.Clear();
        activeSppKicks.Clear();
        ComboHud.Hide();
    }

    private static void Report(string key, string message)
    {
        messages.TryGetValue(key, out int count);
        if (count >= 10) return;
        messages[key] = count + 1;
        log.LogInfo("HOLD ASSIST " + message);
    }

    private enum SpecialKind
    {
        Moonrang,
        Limbo,
        PotionKick,
        Reshan,
        LeapFrog,
        Trapeze,
        VenomFlurry,
        Jugglenaut
    }

    private sealed class SpecialSession
    {
        public readonly SpecialKind Kind;
        public readonly IntPtr Owner;
        public int Count;
        public bool Unlocked;
        public float AutomationEnd;
        public int LastSuccessToken = int.MinValue;

        public SpecialSession(SpecialKind kind, IntPtr owner)
        {
            Kind = kind;
            Owner = owner;
        }
    }
}
