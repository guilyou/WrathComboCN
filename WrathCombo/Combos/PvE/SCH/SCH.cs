using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using ECommons.Automation;
using System.Linq;
using WrathCombo.Combos.PvE.Content;
using WrathCombo.CustomComboNS;
using WrathCombo.Data;
using WrathCombo.Extensions;

namespace WrathCombo.Combos.PvE;

internal static partial class SCH
{

    private static bool 复生喊话 = false;

    /*
     * SCH_Consolation
     * Even though Summon Seraph becomes Consolation,
     * This Feature also places Seraph's AoE heal+barrier ontop of the existing fairy AoE skill, Fey Blessing
     */
    internal class SCH_Consolation : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Consolation;
        protected override uint Invoke(uint actionID)
            => actionID is FeyBlessing && LevelChecked(SummonSeraph) && Gauge.SeraphTimer > 0 ? Consolation : actionID;
    }

    /*
     * SCH_Lustrate
     * Replaces Lustrate with Excogitation when Excogitation is ready.
     */
    internal class SCH_Lustrate : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Lustrate;


        protected override uint Invoke(uint actionID)
        {
            if (actionID is Lustrate) {
                //如果没豆子，先以太超流拿豆子
                if (IsEnabled(CustomComboPreset.SCH_Lustrate_以太超流) && ActionReady(Aetherflow) && !Gauge.HasAetherflow() && InCombat()) {
                    return Aetherflow;
                }
                IGameObject? healTarget = GetHealTarget(Config.SCH_ST_Heal_Adv && Config.SCH_ST_Heal_UIMouseOver);
                float hpPercent = GetTargetHPPercent(healTarget);
                //有绿帽先挂绿帽
                if (IsEnabled(CustomComboPreset.SCH_Lustrate_深谋远虑之策) && hpPercent >= 90f && ActionReady(Excogitation) && Gauge.HasAetherflow()) {
                    return Excogitation;
                }
                //没绿帽先挂生命回生法
                if (IsEnabled(CustomComboPreset.SCH_Lustrate_生命回生法) && ActionReady(Protraction)) {
                    return Protraction;
                }
                //结算抬血，若低于50用绿帽抬，若高于50用活性法抬
                if (hpPercent < 50f && ActionReady(Excogitation) && Gauge.HasAetherflow()) {
                    return Excogitation;
                }
                if (hpPercent >= 50f && ActionReady(Lustrate) && Gauge.HasAetherflow()) {
                    return Lustrate;
                }
                //没豆子了，用应急单盾抬
                if (IsEnabled(CustomComboPreset.SCH_Lustrate_应急单盾)) {
                    if (ActionReady(OriginalHook(应急战术))) {
                        return OriginalHook(应急战术);
                    }
                    if (HasEffect(Buffs.应急战术)) {
                        return Adloquium;
                    }
                }
            }
            return actionID;
        }
    }

    /*
     * SCH_Recitation
     * Replaces Recitation with selected one of its combo skills.
     */
    internal class SCH_Recitation : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Recitation;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Recitation || !HasEffect(Buffs.Recitation))
                return actionID;

            switch ((int)Config.SCH_Recitation_Mode)
            {
                case 0: return OriginalHook(Adloquium);
                case 1: return OriginalHook(Succor);
                case 2: return OriginalHook(Indomitability);
                case 3: return OriginalHook(Excogitation);
            }

            return actionID;
        }
    }

    /*
     * SCH_Aetherflow
     * Replaces all Energy Drain actions with Aetherflow when depleted, or just Energy Drain
     * Dissipation option to show if Aetherflow is on Cooldown
     * Recitation also an option
     */
    internal class SCH_Aetherflow : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Aetherflow;
        protected override uint Invoke(uint actionID)
        {
            if (!AetherflowList.Contains(actionID) || !LevelChecked(Aetherflow))
                return actionID;

            bool hasAetherFlows = Gauge.HasAetherflow(); //False if Zero stacks

            if (IsEnabled(CustomComboPreset.SCH_Aetherflow_Recite) &&
                LevelChecked(Recitation) &&
                (IsOffCooldown(Recitation) || HasEffect(Buffs.Recitation)))
            {
                //Recitation Indominability and Excogitation, with optional check against AF zero stack count
                bool alwaysShowReciteExcog = Config.SCH_Aetherflow_Recite_ExcogMode == 1;

                if (Config.SCH_Aetherflow_Recite_Excog &&
                    (alwaysShowReciteExcog ||
                     !alwaysShowReciteExcog && !hasAetherFlows) && actionID is Excogitation)
                {
                    //Do not merge this nested if with above. Won't procede with next set
                    return HasEffect(Buffs.Recitation) && IsOffCooldown(Excogitation)
                        ? Excogitation
                        : Recitation;
                }

                bool alwaysShowReciteIndom = Config.SCH_Aetherflow_Recite_IndomMode == 1;

                if (Config.SCH_Aetherflow_Recite_Indom &&
                    (alwaysShowReciteIndom ||
                     !alwaysShowReciteIndom && !hasAetherFlows) && actionID is Indomitability)
                {
                    //Same as above, do not nest with above. It won't procede with the next set
                    return HasEffect(Buffs.Recitation) && IsOffCooldown(Excogitation)
                        ? Indomitability
                        : Recitation;
                }
            }
            if (!hasAetherFlows)
            {
                bool showAetherflowOnAll = Config.SCH_Aetherflow_Display == 1;

                if ((actionID is EnergyDrain && !showAetherflowOnAll || showAetherflowOnAll) &&
                    IsOffCooldown(actionID))
                {
                    if (IsEnabled(CustomComboPreset.SCH_Aetherflow_Dissipation) &&
                        ActionReady(Dissipation) && IsOnCooldown(Aetherflow) && HasPetPresent())
                        //Dissipation requires fairy, can't seem to make it replace dissipation with fairy summon feature *shrug*
                        return Dissipation;

                    return Aetherflow;
                }
            }
            return actionID;
        }
    }

    /*
     * SCH_Raise (Swiftcast Raise combo)
     * Swiftcast changes to Raise when swiftcast is on cooldown
     */
    internal class SCH_Raise : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Raise;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is Resurrection) {
                //复生喊话
                if (IsEnabled(CustomComboPreset.SCH_Raise_Say)) {
                    if (JustUsed(Resurrection)) {
                        if (复生喊话 == false && IsInParty()) {
                            复生喊话 = true;
                            IGameObject? healTarget = GetHealTarget(true);
                            string LeaderName = healTarget.Name.ToString();
                            if (WasLastAbility(All.Swiftcast))
                                Chat.Instance.SendMessage($"/p 上善若水，烟水还魂！已对{LeaderName}使用复苏。<se.7>");
                            else
                                Chat.Instance.SendMessage($"/p 正在对{LeaderName}]咏唱复苏。<se.7>");
                        }
                    }
                    else {
                        if (复生喊话 == true) {
                            复生喊话 = false;
                        }
                    }
                }

                if (ActionReady(All.Swiftcast)) {
                    return All.Swiftcast;
                }
            }
            return actionID;
        }
    }

    // Replaces Fairy abilities with Fairy summoning with Eos
    internal class SCH_FairyReminder : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_FairyReminder;
        protected override uint Invoke(uint actionID)
            => FairyList.Contains(actionID) && NeedToSummon ? SummonEos : actionID;
    }

    /*
     * SCH_DeploymentTactics
     * Combos Deployment Tactics with Adloquium by showing Adloquim when Deployment Tactics is ready,
     * Recitation is optional, if one wishes to Crit the shield first
     * Supports soft targetting and self as a fallback.
     */
    internal class SCH_DeploymentTactics : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_DeploymentTactics;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not DeploymentTactics || !ActionReady(DeploymentTactics))
                return actionID;

            //Grab our target (Soft->Hard->Self)
            IGameObject? healTarget = GetHealTarget(Config.SCH_DeploymentTactics_Adv && Config.SCH_DeploymentTactics_UIMouseOver);

            //Check for the Galvanize shield buff. Start applying if it doesn't exist
            if (FindEffect(Buffs.Galvanize, healTarget, LocalPlayer.GameObjectId) is null)
            {
                if (IsEnabled(CustomComboPreset.SCH_DeploymentTactics_Recitation) && ActionReady(Recitation))
                    return Recitation;

                return OriginalHook(Adloquium);
            }
            return actionID;
        }
    }

    internal class SCH_DeploymentTactics2 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_DeploymentTactics2;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is DeploymentTactics) {

                //展开战术没好，不用执行后面的，做了盾也扩散不了
                if (!ActionReady(DeploymentTactics)) {
                    //但是因为秘策CD对不上，如果秘策CD另外转好了，可以打个秘策
                    if (ActionReady(Recitation) && !HasEffect(Buffs.Recitation)) {
                        return Recitation;
                    }
                    //否则就等着吧
                    return actionID;
                }

                //获得治疗对象
                IGameObject? healTarget = GetHealTarget(true);

                //如果有秘策，且没有激励（因为有激励肯定就已经是暴击盾了），打秘策
                if (ActionReady(Recitation) && !HasEffect(Buffs.Recitation) && (FindEffectOnMember(Buffs.激励, healTarget) is null)) {
                    return Recitation;
                }

                //有秘策做秘策盾
                if (HasEffect(Buffs.Recitation)) {
                    //插入回生法，增加盾量
                    if (ActionReady(Protraction) && FindEffectOnMember(Buffs.生命回生法, healTarget) is null) {
                        return Protraction;
                    }
                    //插入即刻
                    if (ActionReady(All.Swiftcast) && !HasEffect(All.Buffs.Swiftcast)) {
                        return All.Swiftcast;
                    }
                    //做盾
                    return Adloquium;
                }

                //如果已经做好盾了，直接展开
                if (!(FindEffectOnMember(Buffs.Galvanize, healTarget) is null)) {
                    return DeploymentTactics;
                }

                //没鼓舞，准备做盾，先打回生法增加盾量
                if (ActionReady(Protraction) && FindEffectOnMember(Buffs.生命回生法, healTarget) is null) {
                    return Protraction;
                }
                //插入即刻
                if (ActionReady(All.Swiftcast) && !HasEffect(All.Buffs.Swiftcast)) {
                    return All.Swiftcast;
                }
                return Adloquium;
            }
            return actionID;
        }
    }
    

    /*
     * SCH_DPS
     * Overrides main DPS ability family, The Broils (and Ruin 1)
     * Implements Ruin 2 as the movement option
     * Chain Stratagem has overlap protection
     */
    internal class SCH_DPS : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_DPS;

        internal static int BroilCount => ActionWatching.CombatActions.Count(x => x == OriginalHook(Broil));

        protected override uint Invoke(uint actionID)
        {
            bool actionFound;

            if (Config.SCH_ST_DPS_Adv && Config.SCH_ST_DPS_Adv_Actions.Count > 0)
            {
                bool onBroils = Config.SCH_ST_DPS_Adv_Actions[0] && BroilList.Contains(actionID);
                bool onBios = Config.SCH_ST_DPS_Adv_Actions[1] && BioList.ContainsKey(actionID);
                bool onRuinII = Config.SCH_ST_DPS_Adv_Actions[2] && actionID is Ruin2;
                actionFound = onBroils || onBios || onRuinII;
            }
            else
                actionFound = BroilList.Contains(actionID); //default handling

            // Return if action not found
            if (!actionFound)
                return actionID;

            if (IsEnabled(CustomComboPreset.SCH_DPS_FairyReminder) &&
                NeedToSummon)
                return SummonEos;

            if (IsEnabled(CustomComboPreset.SCH_DPS_Variant_Rampart) &&
                IsEnabled(Variant.VariantRampart) &&
                IsOffCooldown(Variant.VariantRampart) &&
                CanSpellWeave())
                return Variant.VariantRampart;

            //Opener
            if (IsEnabled(CustomComboPreset.SCH_DPS_Balance_Opener))
                if (Opener().FullOpener(ref actionID))
                    return actionID;

            // Aetherflow
            if (IsEnabled(CustomComboPreset.SCH_DPS_Aetherflow) &&
                !WasLastAction(Dissipation) && ActionReady(Aetherflow) &&
                !Gauge.HasAetherflow() && InCombat() && CanSpellWeave())
                return Aetherflow;

            // Lucid Dreaming
            if (IsEnabled(CustomComboPreset.SCH_DPS_Lucid) &&
                ActionReady(All.LucidDreaming) &&
                LocalPlayer.CurrentMp <= Config.SCH_ST_DPS_LucidOption &&
                CanSpellWeave())
                return All.LucidDreaming;

            //Target based options
            if (HasBattleTarget())
            {
                // Energy Drain
                if (IsEnabled(CustomComboPreset.SCH_DPS_EnergyDrain))
                {
                    float edTime = Config.SCH_ST_DPS_EnergyDrain_Adv ? Config.SCH_ST_DPS_EnergyDrain : 10f;

                    if (LevelChecked(EnergyDrain) && InCombat() && CanSpellWeave() &&
                        Gauge.HasAetherflow() && GetCooldownRemainingTime(Aetherflow) <= edTime &&
                        (!IsEnabled(CustomComboPreset.SCH_DPS_EnergyDrain_BurstSaver) ||
                         LevelChecked(ChainStratagem) && GetCooldownRemainingTime(ChainStratagem) > 10 ||
                         !ChainStratagem.LevelChecked()))
                        return EnergyDrain;
                }

                // Chain Stratagem
                if (IsEnabled(CustomComboPreset.SCH_DPS_ChainStrat) &&
                    (Config.SCH_ST_DPS_ChainStratagemSubOption == 0 ||
                     Config.SCH_ST_DPS_ChainStratagemSubOption == 1 && InBossEncounter()))
                {
                    // If CS is available and usable, or if the Impact Buff is on Player
                    if (ActionReady(ChainStratagem) &&
                        !TargetHasEffectAny(Debuffs.ChainStratagem) &&
                        GetTargetHPPercent() > Config.SCH_ST_DPS_ChainStratagemOption &&
                        InCombat() &&
                        CanSpellWeave())
                        return ChainStratagem;

                    if (LevelChecked(BanefulImpaction) &&
                        HasEffect(Buffs.ImpactImminent) &&
                        InCombat() &&
                        CanSpellWeave())
                        return BanefulImpaction;
                    // Don't use OriginalHook(ChainStratagem), because player can disable ingame action replacement
                }

                //Bio/Biolysis
                if (IsEnabled(CustomComboPreset.SCH_DPS_Bio) && LevelChecked(Bio) && InCombat() &&
                    BioList.TryGetValue(OriginalHook(Bio), out ushort dotDebuffID))
                {
                    if (IsEnabled(CustomComboPreset.SCH_DPS_Variant_SpiritDart) &&
                        IsEnabled(Variant.VariantSpiritDart) &&
                        GetDebuffRemainingTime(Variant.Debuffs.SustainedDamage) <= 3 &&
                        CanSpellWeave())
                        return Variant.VariantSpiritDart;

                    float refreshTimer = Config.SCH_ST_DPS_Bio_Adv ? Config.SCH_DPS_BioUptime_Threshold : 3;
                    int hpThreshold = Config.SCH_DPS_BioSubOption == 1 || !InBossEncounter() ? Config.SCH_DPS_BioOption : 0;
                    if (GetDebuffRemainingTime(dotDebuffID) <= refreshTimer &&
                        GetTargetHPPercent() > hpThreshold)
                        return OriginalHook(Bio);
                }

                //Ruin 2 Movement
                if (IsEnabled(CustomComboPreset.SCH_DPS_Ruin2Movement) &&
                    LevelChecked(Ruin2) && IsMoving())
                    return OriginalHook(Ruin2);
            }
            return actionID;
        }
    }

    /*
     * SCH_AoE
     * Overrides main AoE DPS ability, Art of War
     * Lucid Dreaming and Aetherflow weave options
     */
    internal class SCH_AoE : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_AoE;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (ArtOfWar or ArtOfWarII))
                return actionID;

            if (IsEnabled(CustomComboPreset.SCH_AoE_FairyReminder) &&
                NeedToSummon)
                return SummonEos;

            if (IsEnabled(CustomComboPreset.SCH_DPS_Variant_Rampart) &&
                IsEnabled(Variant.VariantRampart) &&
                IsOffCooldown(Variant.VariantRampart) &&
                CanSpellWeave())
                return Variant.VariantRampart;

            Status? sustainedDamage = FindTargetEffect(Variant.Debuffs.SustainedDamage);

            if (IsEnabled(CustomComboPreset.SCH_DPS_Variant_SpiritDart) &&
                IsEnabled(Variant.VariantSpiritDart) &&
                (sustainedDamage is null || sustainedDamage.RemainingTime <= 3) &&
                HasBattleTarget())
                return Variant.VariantSpiritDart;

            // Aetherflow
            if (IsEnabled(CustomComboPreset.SCH_AoE_Aetherflow) &&
                ActionReady(Aetherflow) && !Gauge.HasAetherflow() &&
                InCombat())
                return Aetherflow;

            // Lucid Dreaming
            if (IsEnabled(CustomComboPreset.SCH_AoE_Lucid) &&
                ActionReady(All.LucidDreaming) &&
                LocalPlayer.CurrentMp <= Config.SCH_AoE_LucidOption)
                return All.LucidDreaming;

            return actionID;
        }
    }

    /*
     * SCH_AoE_Heal
     * Overrides main AoE Healing abiility, Succor
     * Lucid Dreaming and Atherflow weave options
     */
    internal class SCH_AoE_Heal : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_AoE_Heal;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Succor)
                return actionID;

            // Aetherflow
            if (IsEnabled(CustomComboPreset.SCH_AoE_Heal_Aetherflow) &&
                ActionReady(Aetherflow) && !Gauge.HasAetherflow() &&
                !(IsEnabled(CustomComboPreset.SCH_AoE_Heal_Aetherflow_Indomitability) && GetCooldownRemainingTime(Indomitability) <= 0.6f) &&
                InCombat())
                return Aetherflow;

            if (IsEnabled(CustomComboPreset.SCH_AoE_Heal_Dissipation)
                && ActionReady(Dissipation)
                && !Gauge.HasAetherflow()
                && InCombat())
                return Dissipation;

            // Lucid Dreaming
            if (IsEnabled(CustomComboPreset.SCH_AoE_Heal_Lucid)
                && All.CanUseLucid(Config.SCH_AoE_Heal_LucidOption))
                return All.LucidDreaming;

            float averagePartyHP = GetPartyAvgHPPercent();
            for(int i = 0; i < Config.SCH_AoE_Heals_Priority.Count; i++)
            {
                int index = Config.SCH_AoE_Heals_Priority.IndexOf(i + 1);
                int config = GetMatchingConfigAoE(index, out uint spell, out bool enabled);

                if (enabled && averagePartyHP <= config && ActionReady(spell))
                    return spell;
            }

            return actionID;
        }
    }

    //Ⱥ̧����
    //�������� - ��������֮��
    internal class SCH_Indomitability : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Indomitability;
        protected override uint Invoke(uint actionID)
        {
            //�ǲ�������֮�� 
            if (actionID is Indomitability) {
                //����������ȼ�����ȴת����
                if (LevelChecked(FeyBlessing) && IsOffCooldown(FeyBlessing)) {
                    //��С��Ů������С��Ů���Ǵ���ʹ״̬
                    if (HasPetPresent() && Gauge.SeraphTimer == 0)
                        //����������
                        return FeyBlessing;
                }

                //���û���ӣ���̫�������ˣ�������̫����
                if (ActionReady(Aetherflow) && !Gauge.HasAetherflow() && InCombat())
                    return Aetherflow;
            }

            return actionID;
        }
    }

    /*
     * SCH_Fairy_Combo
     * Overrides Whispering Dawn
     */
    internal class SCH_Fairy_Combo : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Fairy_Combo;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not WhisperingDawn)
                return actionID;

            if (HasPetPresent())
            {
                // FeyIllumination
                if (ActionReady(FeyIllumination))
                    return OriginalHook(FeyIllumination);

                // FeyBlessing
                if (ActionReady(FeyBlessing) && !(Gauge.SeraphTimer > 0))
                    return OriginalHook(FeyBlessing);

                if (IsEnabled(CustomComboPreset.SCH_Fairy_Combo_Consolation) && ActionReady(WhisperingDawn))
                    return OriginalHook(actionID);

                if (IsEnabled(CustomComboPreset.SCH_Fairy_Combo_Consolation) && Gauge.SeraphTimer > 0 && GetRemainingCharges(Consolation) > 0)
                    return OriginalHook(Consolation);
            }

            return actionID;
        }
    }

    //ת������
    internal class SCH_Fairy_Combo3 : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_Fairy_Combo3;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is Dissipation) {
                if (ActionReady(SummonSeraph) && HasPetPresent()) {
                    return SummonSeraph;
                }
                //if (LevelChecked(SummonSeraph) && Gauge.SeraphTimer > 0 && HasCharges(Consolation)) {
                if (Gauge.SeraphTimer > 0) {
                    return Consolation;
                }
                if (ActionReady(Seraphism) && HasPetPresent() && InCombat()) {
                    return Seraphism;
                }
                if (ActionReady(Dissipation) && HasPetPresent()) {
                    return Dissipation;
                }
            }
            return actionID;
        }
    }

    /*
     * SCH_ST_Heal
     * Overrides main AoE Healing abiility, Succor
     * Lucid Dreaming and Atherflow weave options
     */
    internal class SCH_ST_Heal : CustomCombo
    {
        protected internal override CustomComboPreset Preset { get; } = CustomComboPreset.SCH_ST_Heal;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Physick)
                return actionID;

            // Aetherflow
            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Aetherflow) &&
                ActionReady(Aetherflow) && !Gauge.HasAetherflow() &&
                InCombat() && CanSpellWeave())
                return Aetherflow;

            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Dissipation)
                && ActionReady(Dissipation)
                && !Gauge.HasAetherflow()
                && InCombat())
                return Dissipation;

            // Lucid Dreaming
            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Lucid) &&
                ActionReady(All.LucidDreaming) &&
                LocalPlayer.CurrentMp <= Config.SCH_ST_Heal_LucidOption &&
                CanSpellWeave())
                return All.LucidDreaming;

            // Dissolve Union if needed
            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Aetherpact)
                && OriginalHook(Aetherpact) is DissolveUnion //Quick check to see if Fairy Aetherpact is Active
                && AetherPactTarget is not null //Null checking so GetTargetHPPercent doesn't fall back to CurrentTarget
                && GetTargetHPPercent(AetherPactTarget) >= Config.SCH_ST_Heal_AetherpactDissolveOption)
                return DissolveUnion;

            //Grab our target (Soft->Hard->Self)
            IGameObject? healTarget = OptionalTarget ?? GetHealTarget(Config.SCH_ST_Heal_Adv && Config.SCH_ST_Heal_UIMouseOver);

            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Esuna) && ActionReady(All.Esuna) &&
                GetTargetHPPercent(healTarget, Config.SCH_ST_Heal_IncludeShields) >= Config.SCH_ST_Heal_EsunaOption &&
                HasCleansableDebuff(healTarget))
                return All.Esuna;

            for(int i = 0; i < Config.SCH_ST_Heals_Priority.Count; i++)
            {
                int index = Config.SCH_ST_Heals_Priority.IndexOf(i + 1);
                int config = GetMatchingConfigST(index, out uint spell, out bool enabled);

                if (enabled)
                {
                    if (GetTargetHPPercent(healTarget, Config.SCH_ST_Heal_IncludeShields) <= config &&
                        ActionReady(spell))
                        return spell;
                }
            }

            //Check for the Galvanize shield buff. Start applying if it doesn't exist or Target HP is below %
            if (IsEnabled(CustomComboPreset.SCH_ST_Heal_Adloquium) &&
                ActionReady(Adloquium) &&
                GetTargetHPPercent(healTarget, Config.SCH_ST_Heal_IncludeShields) <= Config.SCH_ST_Heal_AdloquiumOption)
            {
                if (Config.SCH_ST_Heal_AldoquimOpts[2] && ActionReady(EmergencyTactics) && !(FindEffectOnMember(Buffs.Galvanize, healTarget) is null))
                    return EmergencyTactics;

                if ((Config.SCH_ST_Heal_AldoquimOpts[0] || FindEffectOnMember(Buffs.Galvanize, healTarget) is null) && //Ignore existing shield check
                    (!Config.SCH_ST_Heal_AldoquimOpts[1] ||
                     FindEffectOnMember(SGE.Buffs.EukrasianDiagnosis, healTarget) is null && FindEffectOnMember(SGE.Buffs.EukrasianPrognosis, healTarget) is null
                    )) //Eukrasia Shield Check
                    return OriginalHook(Adloquium);
            }

            return actionID;
        }
    }
}




