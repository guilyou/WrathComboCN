using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using ECommons.Automation;
using ECommons.DalamudServices;
using System.Linq;
using WrathCombo.Combos.PvE.Content;
using WrathCombo.CustomComboNS;
using WrathCombo.Data;
namespace WrathCombo.Combos.PvE;

internal partial class WHM
{
    private static bool 复活喊话 = false;
    private static bool 天赐喊话 = false;
    private static bool 庇护所喊话 = false;
    internal class WHM_SolaceMisery : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_SolaceMisery;

        protected override uint Invoke(uint actionID) =>
            actionID is AfflatusSolace && gauge.BloodLily == 3
                ? AfflatusMisery
                : actionID;
    }

    internal class WHM_RaptureMisery : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_RaptureMisery;

        protected override uint Invoke(uint actionID) =>
            actionID is AfflatusRapture && gauge.BloodLily == 3
                ? AfflatusMisery
                : actionID;
    }

    internal class WHM_CureSync : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_CureSync;

        protected override uint Invoke(uint actionID) => actionID is Cure2 && !LevelChecked(Cure2)
            ? Cure
            : actionID;
    }

    internal class WHM_Raise : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_Raise;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not All.Swiftcast)
                return actionID;

            bool thinAirReady = !HasEffect(Buffs.ThinAir) && LevelChecked(ThinAir) && HasCharges(ThinAir);

            if (HasEffect(All.Buffs.Swiftcast))
                return IsEnabled(CustomComboPreset.WHM_ThinAirRaise) && thinAirReady
                    ? ThinAir
                    : Raise;

            return actionID;
        }
    }

    internal class WHM_ST_MainCombo : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_ST_MainCombo;

        protected override uint Invoke(uint actionID)
        {
            bool actionFound;

            if (Config.WHM_ST_MainCombo_Adv && Config.WHM_ST_MainCombo_Adv_Actions.Count > 0)
            {
                bool onStones = Config.WHM_ST_MainCombo_Adv_Actions[0] && StoneGlareList.Contains(actionID);
                bool onAeros = Config.WHM_ST_MainCombo_Adv_Actions[1] && AeroList.ContainsKey(actionID);
                bool onStone2 = Config.WHM_ST_MainCombo_Adv_Actions[2] && actionID is Stone2;
                actionFound = onStones || onAeros || onStone2;
            }
            else
            {
                actionFound = StoneGlareList.Contains(actionID); //default handling
            }

            // If the action is not in the list, return the actionID
            if (!actionFound)
                return actionID;

            if (IsEnabled(CustomComboPreset.WHM_ST_MainCombo_Opener))
                if (Opener().FullOpener(ref actionID))
                    return actionID;

            bool liliesFull = gauge.Lily == 3;
            bool liliesNearlyFull = gauge.Lily == 2 && gauge.LilyTimer >= 17000;

            if (CanSpellWeave())
            {
                bool lucidReady = ActionReady(All.LucidDreaming) && LevelChecked(All.LucidDreaming) &&
                                  LocalPlayer.CurrentMp <= Config.WHM_STDPS_Lucid;
                bool pomReady = LevelChecked(PresenceOfMind) && IsOffCooldown(PresenceOfMind);
                bool assizeReady = LevelChecked(Assize) && IsOffCooldown(Assize);
                bool pomEnabled = IsEnabled(CustomComboPreset.WHM_ST_MainCombo_PresenceOfMind);
                bool assizeEnabled = IsEnabled(CustomComboPreset.WHM_ST_MainCombo_Assize);
                bool lucidEnabled = IsEnabled(CustomComboPreset.WHM_ST_MainCombo_Lucid);

                if (IsEnabled(CustomComboPreset.WHM_DPS_Variant_Rampart) &&
                    IsEnabled(Variant.VariantRampart) &&
                    IsOffCooldown(Variant.VariantRampart) &&
                    CanSpellWeave())
                    return Variant.VariantRampart;

                if (pomEnabled && pomReady)
                    return PresenceOfMind;

                if (assizeEnabled && assizeReady)
                    return Assize;

                if (lucidEnabled && lucidReady)
                    return All.LucidDreaming;
            }

            if (InCombat())
            {
                // DoTs
                if (IsEnabled(CustomComboPreset.WHM_ST_MainCombo_DoT) && LevelChecked(Aero) && HasBattleTarget() &&
                    AeroList.TryGetValue(OriginalHook(Aero), out ushort dotDebuffID))
                {
                    if (IsEnabled(CustomComboPreset.WHM_DPS_Variant_SpiritDart) &&
                        IsEnabled(Variant.VariantSpiritDart) &&
                        GetDebuffRemainingTime(Variant.Debuffs.SustainedDamage) <= 3 &&
                        CanSpellWeave())
                        return Variant.VariantSpiritDart;

                    // DoT Uptime & HP% threshold
                    float refreshTimer = Config.WHM_ST_MainCombo_DoT_Adv ? Config.WHM_ST_MainCombo_DoT_Threshold : 3;
                    int hpThreshold = Config.WHM_ST_DPS_AeroOptionSubOption == 1 || !InBossEncounter() ? Config.WHM_ST_DPS_AeroOption : 0;
                    if (GetDebuffRemainingTime(dotDebuffID) <= refreshTimer &&
                        GetTargetHPPercent() > hpThreshold)
                        return OriginalHook(Aero);
                }

                // Glare IV
                if (IsEnabled(CustomComboPreset.WHM_ST_MainCombo_GlareIV)
                    && HasEffect(Buffs.SacredSight)
                    && GetBuffStacks(Buffs.SacredSight) > 0)
                    return OriginalHook(Glare4);

                if (IsEnabled(CustomComboPreset.WHM_ST_MainCombo_LilyOvercap) && LevelChecked(AfflatusRapture) &&
                    (liliesFull || liliesNearlyFull))
                    return AfflatusRapture;

                if (IsEnabled(CustomComboPreset.WHM_ST_MainCombo_Misery_oGCD) && LevelChecked(AfflatusMisery) &&
                    gauge.BloodLily >= 3)
                    return AfflatusMisery;

                return OriginalHook(Stone1);
            }

            return actionID;
        }
    }

    internal class WHM_AoEHeals : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_AoEHeals;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Medica1)
                return actionID;

            bool thinAirReady = LevelChecked(ThinAir) && !HasEffect(Buffs.ThinAir) &&
                                GetRemainingCharges(ThinAir) > Config.WHM_AoEHeals_ThinAir;
            bool canWeave = CanSpellWeave(0.3);
            bool lucidReady = ActionReady(All.LucidDreaming) && LocalPlayer.CurrentMp <= Config.WHM_AoEHeals_Lucid;

            bool plenaryReady = ActionReady(PlenaryIndulgence) &&
                                (!Config.WHM_AoEHeals_PlenaryWeave ||
                                 Config.WHM_AoEHeals_PlenaryWeave && canWeave);
            bool divineCaressReady = ActionReady(DivineCaress) && HasEffect(Buffs.DivineGrace);

            bool assizeReady = ActionReady(Assize) &&
                               (!Config.WHM_AoEHeals_AssizeWeave || Config.WHM_AoEHeals_AssizeWeave && canWeave);

            IGameObject? healTarget = OptionalTarget ??
                                      (Config.WHM_AoEHeals_MedicaMO
                                          ? GetHealTarget(Config.WHM_AoEHeals_MedicaMO)
                                          : LocalPlayer);
            Status? hasMedica2 = FindEffect(Buffs.Medica2, healTarget, LocalPlayer?.GameObjectId);
            Status? hasMedica3 = FindEffect(Buffs.Medica3, healTarget, LocalPlayer?.GameObjectId);

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Assize) && assizeReady)
                return Assize;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Plenary) && plenaryReady)
                return PlenaryIndulgence;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_DivineCaress) && divineCaressReady)
                return OriginalHook(DivineCaress);

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Lucid) && canWeave && lucidReady)
                return All.LucidDreaming;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Misery) && gauge.BloodLily == 3)
                return AfflatusMisery;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Rapture) && LevelChecked(AfflatusRapture) &&
                gauge.Lily > 0)
                return AfflatusRapture;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_ThinAir) && thinAirReady)
                return ThinAir;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Medica2)
                && (hasMedica2 == null && hasMedica3 == null // No Medica buffs
                    || hasMedica2 != null &&
                    hasMedica2.RemainingTime <=
                    Config.WHM_AoEHeals_MedicaTime // Medica buff, but falling off soon
                    || hasMedica3 != null && hasMedica3.RemainingTime <= Config.WHM_AoEHeals_MedicaTime) // ^
                && (ActionReady(Medica2) || ActionReady(Medica3)))
                return LevelChecked(Medica3) ? Medica3 : Medica2;

            if (IsEnabled(CustomComboPreset.WHM_AoEHeals_Cure3)
                && ActionReady(Cure3)
                && (LocalPlayer.CurrentMp >= Config.WHM_AoEHeals_Cure3MP
                    || HasEffect(Buffs.ThinAir)))
                return Cure3;

            return actionID;
        }
    }

    internal class WHM_ST_Heals : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_STHeals;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Cure)
                return actionID;

            IGameObject? healTarget = OptionalTarget ?? GetHealTarget(Config.WHM_STHeals_UIMouseOver);

            bool thinAirReady = LevelChecked(ThinAir) && !HasEffect(Buffs.ThinAir) &&
                                GetRemainingCharges(ThinAir) > Config.WHM_STHeals_ThinAir;

            bool regenReady = ActionReady(Regen) &&
                              !JustUsed(Regen, 4) &&
                              (!MemberHasEffect(Buffs.Regen, healTarget, false, out var regen) || regen?.RemainingTime <= Config.WHM_STHeals_RegenTimer);

            if (IsEnabled(CustomComboPreset.WHM_STHeals_Esuna) && ActionReady(All.Esuna) &&
                GetTargetHPPercent(healTarget, Config.WHM_STHeals_IncludeShields) >= Config.WHM_STHeals_Esuna &&
                HasCleansableDebuff(healTarget))
                return All.Esuna;

            if (IsEnabled(CustomComboPreset.WHM_STHeals_Lucid) &&
                All.CanUseLucid(Config.WHM_STHeals_Lucid))
                return All.LucidDreaming;

            foreach(int prio in Config.WHM_ST_Heals_Priority.Items.OrderBy(x => x))
            {
                int index = Config.WHM_ST_Heals_Priority.IndexOf(prio);
                int config = GetMatchingConfigST(index, OptionalTarget, out uint spell, out bool enabled);

                if (enabled)
                    if (GetTargetHPPercent(healTarget, Config.WHM_STHeals_IncludeShields) <= config &&
                        ActionReady(spell))
                        return spell;
            }


            if (IsEnabled(CustomComboPreset.WHM_STHeals_Regen) && regenReady)
                return Regen;

            if (IsEnabled(CustomComboPreset.WHM_STHeals_Solace) && gauge.Lily > 0 && ActionReady(AfflatusSolace))
                return AfflatusSolace;

            if (IsEnabled(CustomComboPreset.WHM_STHeals_ThinAir) && thinAirReady)
                return ThinAir;

            if (ActionReady(Cure2))
                return Cure2;

            return actionID;
        }
    }

    internal class WHM_AoE_DPS : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_AoE_DPS;

        internal static int AssizeCount => ActionWatching.CombatActions.Count(x => x == Assize);

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (Holy or Holy3))
                return actionID;

            bool liliesFullNoBlood = gauge.Lily == 3 && gauge.BloodLily < 3;
            bool liliesNearlyFull = gauge.Lily == 2 && gauge.LilyTimer >= 17000;
            bool presenceOfMindReady = ActionReady(PresenceOfMind) && !Config.WHM_AoEDPS_PresenceOfMindWeave;

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_SwiftHoly) &&
                ActionReady(All.Swiftcast) &&
                AssizeCount == 0 && !IsMoving() && InCombat())
                return All.Swiftcast;

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_SwiftHoly) &&
                WasLastAction(All.Swiftcast))
                return actionID;

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_Assize) && ActionReady(Assize))
                return Assize;

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_PresenceOfMind) && presenceOfMindReady)
                return PresenceOfMind;

            if (IsEnabled(CustomComboPreset.WHM_DPS_Variant_Rampart) &&
                IsEnabled(Variant.VariantRampart) &&
                IsOffCooldown(Variant.VariantRampart))
                return Variant.VariantRampart;

            if (IsEnabled(CustomComboPreset.WHM_DPS_Variant_SpiritDart) &&
                IsEnabled(Variant.VariantSpiritDart) &&
                GetDebuffRemainingTime(Variant.Debuffs.SustainedDamage) <= 3 &&
                HasBattleTarget())
                return Variant.VariantSpiritDart;

            if (CanSpellWeave() || IsMoving())
            {
                if (ActionReady(Glare4))
                    return OriginalHook(Glare4);

                if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_PresenceOfMind) && ActionReady(PresenceOfMind))
                    return OriginalHook(PresenceOfMind);

                if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_Lucid) && ActionReady(All.LucidDreaming) &&
                    LocalPlayer.CurrentMp <= Config.WHM_AoEDPS_Lucid)
                    return All.LucidDreaming;
            }

            // Glare IV
            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_GlareIV)
                && HasEffect(Buffs.SacredSight)
                && GetBuffStacks(Buffs.SacredSight) > 0)
                return OriginalHook(Glare4);

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_LilyOvercap) && LevelChecked(AfflatusRapture) &&
                (liliesFullNoBlood || liliesNearlyFull))
                return AfflatusRapture;

            if (IsEnabled(CustomComboPreset.WHM_AoE_DPS_Misery) && LevelChecked(AfflatusMisery) &&
                gauge.BloodLily >= 3 && HasBattleTarget())
                return AfflatusMisery;

            return actionID;
        }
    }

    internal class WHM_减伤 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_减伤;

        protected override uint Invoke(uint actionID)
        {
            IGameObject? healTarget = GetHealTarget(true);
            if (actionID is 神祝祷) {
                if (IsEnabled(CustomComboPreset.WHM_减伤_再生)) {
                    if (ActionReady(再生)) {
                        if (FindEffectOnMember(Buffs.再生, healTarget, true) is null) {
                            return 再生;
                        }
                    }
                }
                if (ActionReady(水流幕)) {
                    return 水流幕;
                }
                if (FindEffectOnMember(Buffs.神祝祷, healTarget) is null) {
                    return 神祝祷;
                }
                return BLM.Fire;
            }
            return actionID;
        }
    }

    internal class WHM_狂喜之心_愈疗 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_狂喜之心_愈疗;

        protected override uint Invoke(uint actionID)
        {
            WHMGauge? gauge = GetJobGauge<WHMGauge>();
            if (actionID is 愈疗) {
                if (LevelChecked(狂喜之心) && gauge.Lily > 0) {
                    return 狂喜之心;
                }
                if (IsEnabled(CustomComboPreset.WHM_无中生有_愈疗) && InCombat() && ActionReady(无中生有) && !HasEffect(Buffs.无中生有)) {
                    return 无中生有;
                }
            }
            return actionID;
        }
    }

    internal class WHM_全大赦 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_全大赦;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is 全大赦) {
                if (IsEnabled(CustomComboPreset.WHM_庇护所_喊话)) {
                    if (WasLastAbility(庇护所)) {
                        if (庇护所喊话 == false && IsInParty()) {
                            庇护所喊话 = true;
                            Chat.Instance.SendMessage($"/p 肯定是这边！(请进庇护所！）<se.7>");
                        }
                    }
                    else {
                        if (庇护所喊话 == true) {
                            庇护所喊话 = false;
                        }
                    }
                }
                if (IsEnabled(CustomComboPreset.WHM_法令_全大赦) && ActionReady(法令)) {
                    return 法令;
                }
                if (IsEnabled(CustomComboPreset.WHM_庇护所_全大赦) && ActionReady(庇护所)) {
                    return 庇护所;
                }
            }
            return actionID;
        }
    }

    internal class WHM_Raise_动画 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.WHM_Raise_动画;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is 复活) {
                //复生喊话
                if (IsEnabled(CustomComboPreset.WHM_Raise_喊话)) {
                    if (JustUsed(复活, 3f)) {
                        if (复活喊话 == false && IsInParty()) {
                            复活喊话 = true;
                            IGameObject? healTarget = GetHealTarget(true);
                            string LeaderName = healTarget.Name.ToString();
                            if (JustUsed(All.Swiftcast, 3f)) {
                                Chat.Instance.SendMessage($"/p 大地母亲为你骄傲。（已对【{LeaderName}】施放复活）<se.1>");
                            }
                            else {
                                Chat.Instance.SendMessage($"/p 愿太阳温暖你的脸。（正在复活{LeaderName}）<se.1>");
                            }
                        }
                    }
                    else {
                        if (复活喊话 == true) {
                            复活喊话 = false;
                        }
                    }
                }
                if (ActionReady(All.Swiftcast) && !HasEffect(All.Buffs.Swiftcast)) {
                    return All.Swiftcast;
                }
                if (ActionReady(无中生有) && !HasEffect(Buffs.无中生有)) {
                    return 无中生有;
                }
            }
            return actionID;
        }
    }
}
