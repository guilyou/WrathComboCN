using Dalamud.Hooking;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Window.Functions;

namespace WrathCombo.Core
{
    /// <summary> This class facilitates icon replacement. </summary>
    internal sealed partial class IconReplacer : IDisposable
    {
        public readonly List<CustomCombo> CustomCombos;
        private readonly Hook<IsIconReplaceableDelegate> isIconReplaceableHook;
        public readonly Hook<GetIconDelegate> getIconHook;

        private IntPtr actionManager = IntPtr.Zero;
        private readonly IntPtr module = IntPtr.Zero;

        /// <summary> Initializes a new instance of the <see cref="IconReplacer"/> class. </summary>
        public IconReplacer()
        {
            CustomCombos = Assembly.GetAssembly(typeof(CustomCombo))!.GetTypes()
                .Where(t => !t.IsAbstract && t.BaseType == typeof(CustomCombo))
                .Select(t => Activator.CreateInstance(t))
                .Cast<CustomCombo>()
                .OrderByDescending(x => x.Preset)
                .ToList();

            getIconHook = Svc.Hook.HookFromAddress<GetIconDelegate>((nint)ActionManager.Addresses.GetAdjustedActionId.Value, GetIconDetour);
            isIconReplaceableHook = Svc.Hook.HookFromAddress<IsIconReplaceableDelegate>(Service.Address.IsActionIdReplaceable, IsIconReplaceableDetour);

            getIconHook.Enable();
            isIconReplaceableHook.Enable();
        }

        private delegate ulong IsIconReplaceableDelegate(uint actionID);

        public delegate uint GetIconDelegate(IntPtr actionManager, uint actionID);

        /// <inheritdoc/>
        public void Dispose()
        {
            getIconHook?.Dispose();
            isIconReplaceableHook?.Dispose();
        }

        /// <summary> Calls the original hook. </summary>
        /// <param name="actionID"> Action ID. </param>
        /// <returns> The result from the hook. </returns>
        internal uint OriginalHook(uint actionID) => getIconHook.Original(actionManager, actionID);

        private static IEnumerable<CustomCombo>? _filteredCombos;

        public void UpdateFilteredCombos()
        {
            _filteredCombos = CustomCombos.Where(x => x.Preset.Attributes() is not null && (x.Preset.Attributes().CustomComboInfo.JobID == 0 || x.Preset.Attributes().CustomComboInfo.JobID == Player.JobId || x.Preset.Attributes().CustomComboInfo.JobID == CustomComboFunctions.JobIDs.ClassToJob(Player.JobId)));
        }

        private unsafe uint GetIconDetour(IntPtr actionManager, uint actionID)
        {
            try
            {
                if (_filteredCombos is null)
                    UpdateFilteredCombos();

                if (Svc.ClientState.LocalPlayer == null)
                    return OriginalHook(actionID);

                if (ClassLocked() ||
                    (DisabledJobsPVE.Any(x => x == Svc.ClientState.LocalPlayer.ClassJob.RowId) && !Svc.ClientState.IsPvP) ||
                    (DisabledJobsPVP.Any(x => x == Svc.ClientState.LocalPlayer.ClassJob.RowId) && Svc.ClientState.IsPvP))
                    return OriginalHook(actionID);

                foreach (CustomCombo? combo in _filteredCombos)
                {
                    if (combo.TryInvoke(actionID, out uint newActionID))
                    {
                        if (Service.Configuration.BlockSpellOnMove && ActionManager.GetAdjustedCastTime(ActionType.Action, newActionID) > 0 && CustomComboFunctions.TimeMoving.Ticks > 0)
                        {
                            return OriginalHook(11);
                        }
                        return newActionID;
                    }
                }

                return OriginalHook(actionID);
            }

            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Preset error");
                return OriginalHook(actionID);
            }
        }

        // Class locking
        public unsafe static bool ClassLocked()
        {
            if (Svc.ClientState.LocalPlayer is null) return false;

            if (Svc.ClientState.LocalPlayer.Level <= 35) return false;

            if (Svc.ClientState.LocalPlayer.ClassJob.RowId is
                (>= 8 and <= 25) or 27 or 28 or >= 30)
                return false;

            if (!UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(66049))
                return false;

            if ((Svc.ClientState.LocalPlayer.ClassJob.RowId is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 26 or 29) &&
                Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] &&
                Svc.ClientState.LocalPlayer.Level > 35) return true;

            return false;
        }

        private ulong IsIconReplaceableDetour(uint actionID) => 1;
    }
}
