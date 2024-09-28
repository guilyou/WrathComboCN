﻿using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using XIVSlothCombo.Attributes;
using XIVSlothCombo.Combos;
using XIVSlothCombo.Combos.PvE;
using XIVSlothCombo.Core;
using XIVSlothCombo.Data;
using XIVSlothCombo.Services;

namespace XIVSlothCombo.Window.Functions
{
    internal class Presets : ConfigWindow
    {
        internal static Dictionary<CustomComboPreset, PresetAttributes> Attributes = new();
        internal class PresetAttributes
        {
            public bool IsPvP;
            public CustomComboPreset[] Conflicts;
            public CustomComboPreset? Parent;
            public BlueInactiveAttribute? BlueInactive;
            public VariantParentAttribute? VariantParent;
            public BozjaParentAttribute? BozjaParent;
            public EurekaParentAttribute? EurekaParent;
            public HoverInfoAttribute? HoverInfo;
            public ReplaceSkillAttribute? ReplaceSkill;
            public CustomComboInfoAttribute? CustomComboInfo;
            public AutoActionAttribute? AutoAction;

            public PresetAttributes(CustomComboPreset preset)
            {
                IsPvP = PresetStorage.IsPvP(preset);
                Conflicts = PresetStorage.GetConflicts(preset);
                Parent = PresetStorage.GetParent(preset);
                BlueInactive = preset.GetAttribute<BlueInactiveAttribute>();
                VariantParent = preset.GetAttribute<VariantParentAttribute>();
                BozjaParent = preset.GetAttribute<BozjaParentAttribute>();
                EurekaParent = preset.GetAttribute<EurekaParentAttribute>();
                HoverInfo = preset.GetAttribute<HoverInfoAttribute>();
                ReplaceSkill = preset.GetAttribute<ReplaceSkillAttribute>();
                CustomComboInfo = preset.GetAttribute<CustomComboInfoAttribute>();
                AutoAction = preset.GetAttribute<AutoActionAttribute>();
            }
        }

        internal unsafe static void DrawPreset(CustomComboPreset preset, CustomComboInfoAttribute info, ref int i)
        {
            if (!Attributes.ContainsKey(preset))
            {
                PresetAttributes attributes = new(preset);
                Attributes[preset] = attributes;
            }
            var enabled = PresetStorage.IsEnabled(preset);
            var secret = Attributes[preset].IsPvP;
            var conflicts = Attributes[preset].Conflicts;
            var parent = Attributes[preset].Parent;
            var blueAttr = Attributes[preset].BlueInactive;
            var variantParents = Attributes[preset].VariantParent;
            var bozjaParents = Attributes[preset].BozjaParent;
            var eurekaParents = Attributes[preset].EurekaParent;
            var auto = Attributes[preset].AutoAction;

            ImGui.Spacing();

            if (auto != null)
            {
                if (!Service.Configuration.AutoActions.ContainsKey(preset))
                    Service.Configuration.AutoActions[preset] = false;

                var label = "Auto-Mode";
                var labelSize = ImGui.CalcTextSize(label);
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - labelSize.X.Scale() - 64f.Scale());
                bool autoOn = Service.Configuration.AutoActions[preset];
                if (ImGui.Checkbox($"###AutoAction{i}", ref autoOn))
                {
                    Service.Configuration.AutoActions[preset] = autoOn;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                ImGuiEx.Text(label);
                ImGuiComponents.HelpMarker($"Add this feature to Auto-Rotation.\n" +
                    $"Auto-Rotation will automatically use the actions selected within the feature, allowing you to focus on movement. Configure the settings in the 'Auto-Rotation' section.");
                ImGui.Separator();
            }

            if (ImGui.Checkbox($"{info.Name}###{preset}{i}", ref enabled))
            {
                if (enabled)
                {
                    EnableParentPresets(preset);
                    Service.Configuration.EnabledActions.Add(preset);
                    foreach (var conflict in conflicts)
                    {
                        Service.Configuration.EnabledActions.Remove(conflict);
                    }
                }
                else
                {
                    Service.Configuration.EnabledActions.Remove(preset);
                }

                Service.Configuration.Save();
            }

            DrawReplaceAttribute(preset);

            Vector2 length = new();
            using (var styleCol = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
            {
                if (i != -1)
                {
                    ImGui.Text($"#{i}: ");
                    length = ImGui.CalcTextSize($"#{i}: ");
                    ImGui.SameLine();
                    ImGui.PushItemWidth(length.Length());
                }

                ImGui.TextWrapped($"{info.Description}");

                if (Attributes[preset].HoverInfo != null)
                {
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(Attributes[preset].HoverInfo.HoverText);
                        ImGui.EndTooltip();
                    }
                }
            }


            ImGui.Spacing();

            if (conflicts.Length > 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Conflicts with:");
                StringBuilder conflictBuilder = new();
                ImGui.Indent();
                foreach (var conflict in conflicts)
                {
                    var comboInfo = Attributes[conflict].CustomComboInfo;
                    conflictBuilder.Insert(0, $"{comboInfo.Name}");
                    var par2 = conflict;

                    while (PresetStorage.GetParent(par2) != null)
                    {
                        var subpar = PresetStorage.GetParent(par2);
                        if (subpar != null)
                        {
                            conflictBuilder.Insert(0, $"{Attributes[subpar.Value].CustomComboInfo.Name} -> ");
                            par2 = subpar!.Value;
                        }

                    }

                    if (!string.IsNullOrEmpty(comboInfo.JobShorthand))
                        conflictBuilder.Insert(0, $"[{comboInfo.JobShorthand}] ");

                    ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudRed, CustomComboNS.Functions.CustomComboFunctions.IsEnabled(conflict) ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, 1500), $"- {conflictBuilder}");
                    conflictBuilder.Clear();
                }
                ImGui.Unindent();
                ImGui.Spacing();
            }

            if (blueAttr != null)
            {
                if (blueAttr.Actions.Count > 0)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, blueAttr.NoneSet ? ImGuiColors.DPSRed : ImGuiColors.DalamudOrange);
                    ImGui.Text($"{(blueAttr.NoneSet ? "No Required Spells Active:" : "Missing active spells:")} {string.Join(", ", blueAttr.Actions.Select(x => ActionWatching.GetBLUIndex(x) + ActionWatching.GetActionName(x)))}");
                    ImGui.PopStyleColor();
                }

                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
                    ImGui.Text($"All required spells active!");
                    ImGui.PopStyleColor();
                }
            }

            if (variantParents is not null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
                ImGui.TextWrapped($"Part of normal combo{(variantParents.ParentPresets.Length > 1 ? "s" : "")}:");
                StringBuilder builder = new();
                foreach (var par in variantParents.ParentPresets)
                {
                    builder.Insert(0, $"{(Attributes.ContainsKey(par) ? Attributes[par].CustomComboInfo.Name : par.GetAttribute<CustomComboInfoAttribute>().Name)}");
                    var par2 = par;
                    while (PresetStorage.GetParent(par2) != null)
                    {
                        var subpar = PresetStorage.GetParent(par2);
                        if (subpar != null)
                        {
                            builder.Insert(0, $"{(Attributes.ContainsKey(subpar.Value) ? Attributes[subpar.Value].CustomComboInfo.Name : subpar?.GetAttribute<CustomComboInfoAttribute>().Name)} -> ");
                            par2 = subpar!.Value;
                        }

                    }

                    ImGui.TextWrapped($"- {builder}");
                    builder.Clear();
                }
                ImGui.PopStyleColor();
            }

            if (bozjaParents is not null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
                ImGui.TextWrapped($"Part of normal combo{(variantParents.ParentPresets.Length > 1 ? "s" : "")}:");
                StringBuilder builder = new();
                foreach (var par in bozjaParents.ParentPresets)
                {
                    builder.Insert(0, $"{(Attributes.ContainsKey(par) ? Attributes[par].CustomComboInfo.Name : par.GetAttribute<CustomComboInfoAttribute>().Name)}");
                    var par2 = par;
                    while (PresetStorage.GetParent(par2) != null)
                    {
                        var subpar = PresetStorage.GetParent(par2);
                        if (subpar != null)
                        {
                            builder.Insert(0, $"{(Attributes.ContainsKey(subpar.Value) ? Attributes[subpar.Value].CustomComboInfo.Name : subpar?.GetAttribute<CustomComboInfoAttribute>().Name)} -> ");
                            par2 = subpar!.Value;
                        }

                    }

                    ImGui.TextWrapped($"- {builder}");
                    builder.Clear();
                }
                ImGui.PopStyleColor();
            }

            if (eurekaParents is not null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
                ImGui.TextWrapped($"Part of normal combo{(variantParents.ParentPresets.Length > 1 ? "s" : "")}:");
                StringBuilder builder = new();
                foreach (var par in eurekaParents.ParentPresets)
                {
                    builder.Insert(0, $"{(Attributes.ContainsKey(par) ? Attributes[par].CustomComboInfo.Name : par.GetAttribute<CustomComboInfoAttribute>().Name)}");
                    var par2 = par;
                    while (PresetStorage.GetParent(par2) != null)
                    {
                        var subpar = PresetStorage.GetParent(par2);
                        if (subpar != null)
                        {
                            builder.Insert(0, $"{(Attributes.ContainsKey(subpar.Value) ? Attributes[subpar.Value].CustomComboInfo.Name : subpar?.GetAttribute<CustomComboInfoAttribute>().Name)} -> ");
                            par2 = subpar!.Value;
                        }

                    }

                    ImGui.TextWrapped($"- {builder}");
                    builder.Clear();
                }
                ImGui.PopStyleColor();
            }
            if (enabled)
            {
                switch (info.JobID)
                {
                    //case All.JobID: All.Config.Draw(preset); break;
                    //case AST.JobID: AST.Config.Draw(preset); break;
                    //case BLM.JobID: BLM.Config.Draw(preset); break;
                    //case BLU.JobID: BLU.Config.Draw(preset); break;
                    //case BRD.JobID: BRD.Config.Draw(preset); break;
                    //case DNC.JobID: DNC.Config.Draw(preset); break;
                    //case DOL.JobID: DOL.Config.Draw(preset); break;
                    //case DRG.JobID: DRG.Config.Draw(preset); break;
                    //case DRK.JobID: DRK.Config.Draw(preset); break;
                    //case GNB.JobID: GNB.Config.Draw(preset); break;
                    //case MCH.JobID: MCH.Config.Draw(preset); break;
                    case MNK.JobID: MNK.Config.Draw(preset); break;
                    //case NIN.JobID: NIN.Config.Draw(preset); break;
                    //case PCT.JobID: PCT.Config.Draw(preset); break;
                    //case PLD.JobID: PLD.Config.Draw(preset); break;
                    //case RPR.JobID: RPR.Config.Draw(preset); break;
                    //case RDM.JobID: RDM.Config.Draw(preset); break;
                    //case SAM.JobID: SAM.Config.Draw(preset); break;
                    //case SCH.JobID: SCH.Config.Draw(preset); break;
                    //case SGE.JobID: SGE.Config.Draw(preset); break;
                    //case SMN.JobID: SMN.Config.Draw(preset); break;
                    //case VPR.JobID: VPR.Config.Draw(preset); break;
                    //case WAR.JobID: WAR.Config.Draw(preset); break;
                    //case WHM.JobID: WHM.Config.Draw(preset); break;
                    default: UserConfigItems.Draw(preset, enabled); break;
                }
            }

            i++;

            //var children = presetChildren.ContainsKey(preset) ? presetChildren[preset] : null
            presetChildren.TryGetValue(preset, out var children);

            if (children != null)
            {
                if (enabled || !Service.Configuration.HideChildren)
                {
                    ImGui.Indent();

                    foreach (var (childPreset, childInfo) in children)
                    {
                        if (Service.Configuration.HideConflictedCombos)
                        {
                            var conflictOriginals = PresetStorage.GetConflicts(childPreset);    // Presets that are contained within a ConflictedAttribute
                            var conflictsSource = PresetStorage.GetAllConflicts();              // Presets with the ConflictedAttribute

                            if (!conflictsSource.Where(x => x == childPreset || x == preset).Any() || conflictOriginals.Length == 0)
                            {
                                DrawPreset(childPreset, childInfo, ref i);
                                continue;
                            }

                            if (conflictOriginals.Any(x => PresetStorage.IsEnabled(x)))
                            {
                                Service.Configuration.EnabledActions.Remove(childPreset);
                                Service.Configuration.Save();
                            }

                            else
                            {
                                DrawPreset(childPreset, childInfo, ref i);
                                continue;
                            }
                        }
                        else
                        {
                            DrawPreset(childPreset, childInfo, ref i);
                            continue;
                        }
                    }

                    ImGui.Unindent();
                }
                else
                {
                    i += AllChildren(presetChildren[preset]);

                }
            }
        }

        private static void DrawReplaceAttribute(CustomComboPreset preset)
        {
            var att = Attributes[preset].ReplaceSkill;
            if (att != null)
            {
                string skills = string.Join(", ", att.ActionNames);

                ImGuiComponents.HelpMarker($"Replaces: {skills}");
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    foreach (var icon in att.ActionIcons)
                    {
                        var img = Svc.Texture.GetFromGameIcon(new(icon)).GetWrapOrEmpty();
                        ImGui.Image(img.ImGuiHandle, (img.Size / 2f) * ImGui.GetIO().FontGlobalScale);
                        ImGui.SameLine();
                    }
                    ImGui.EndTooltip();
                }
            }
        }

        internal static int AllChildren((CustomComboPreset Preset, CustomComboInfoAttribute Info)[] children)
        {
            var output = 0;

            foreach (var (Preset, Info) in children)
            {
                output++;
                output += AllChildren(presetChildren[Preset]);
            }

            return output;
        }



        /// <summary> Iterates up a preset's parent tree, enabling each of them. </summary>
        /// <param name="preset"> Combo preset to enabled. </param>
        private static void EnableParentPresets(CustomComboPreset preset)
        {
            var parentMaybe = PresetStorage.GetParent(preset);

            while (parentMaybe != null)
            {
                var parent = parentMaybe.Value;

                if (!Service.Configuration.EnabledActions.Contains(parent))
                {
                    Service.Configuration.EnabledActions.Add(parent);
                    foreach (var conflict in PresetStorage.GetConflicts(parent))
                    {
                        Service.Configuration.EnabledActions.Remove(conflict);
                    }
                }

                parentMaybe = PresetStorage.GetParent(parent);
            }
        }
    }
}