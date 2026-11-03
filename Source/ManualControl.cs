using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.State;
using HarmonyLib;
using Railroader.Modding;
using Track.Crossings;
using UnityEngine;

namespace BrysonCrossingLamp
{
    public static class ManualControl
    {
        private const string PatchId = "joshi.bryson-crossing-lamp.manual";
        private static readonly HashSet<GradeCrossingController> Closed = new HashSet<GradeCrossingController>();
        private static Harmony harmony;
        private static MethodInfo circuit;
        private static int closedValue;
        public static bool Available { get; private set; }

        public static void Install(IModLogger log)
        {
            if (Available) return;
            try
            {
                circuit = AccessTools.Method(typeof(GradeCrossingController), "UpdateCircuit", new[] { typeof(int) });
                if (circuit == null || !circuit.ReturnType.IsEnum || Enum.GetUnderlyingType(circuit.ReturnType) != typeof(int))
                    throw new InvalidOperationException("Unsupported crossing circuit signature.");
                closedValue = Convert.ToInt32(Enum.Parse(circuit.ReturnType, "Gates"));
                harmony = new Harmony(PatchId);
                harmony.Patch(circuit, transpiler: new HarmonyMethod(typeof(ManualControl), nameof(Transpile)));
                Available = true;
                log.Info("AUTO/CLOSE circuit override installed; controls available to host/single player.");
            }
            catch (Exception ex)
            {
                Available = false;
                log.Error(ex, "Manual crossing control unavailable; automatic crossing logic retained.");
            }
        }
        public static void Uninstall()
        {
            Reset();
            if (harmony != null && circuit != null) harmony.Unpatch(circuit, HarmonyPatchType.All, PatchId);
            harmony = null;
            Available = false;
        }
        public static void Reset() { Closed.Clear(); }
        public static bool IsClosed(GradeCrossingController crossing) => crossing != null && Closed.Contains(crossing);
        public static bool CanControl(GradeCrossingController crossing) => Available && StateManager.IsHost &&
            crossing != null && crossing.isActiveAndEnabled && crossing.circuitSets != null && crossing.circuitSets.Count > 0;
        public static bool Toggle(GradeCrossingController crossing)
        {
            if (!CanControl(crossing)) return false;
            if (!Closed.Remove(crossing)) Closed.Add(crossing);
            return true;
        }
        public static bool ForceClosed(GradeCrossingController crossing) =>
            Available && StateManager.IsHost && IsClosed(crossing);
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator generator) =>
            CircuitOverride.Inject(instructions, generator, AccessTools.Method(typeof(ManualControl), nameof(ForceClosed)), closedValue);
    }

    public sealed class CrossingToggle : MonoBehaviour, IPickable
    {
        public LampRuntime Owner;
        public float MaxPickDistance => 5f;
        public int Priority => 2;
        public PickableActivationFilter ActivationFilter => PickableActivationFilter.PrimaryOnly;
        public TooltipInfo TooltipInfo => Owner == null ? TooltipInfo.Empty : Owner.SwitchTooltip;
        public void Activate(PickableActivateEvent evt)
        {
            if (evt.Activation == PickableActivation.Primary && Owner != null) Owner.ToggleManual();
        }
        public void Deactivate() { }
    }
}
