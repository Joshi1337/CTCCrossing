using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.State;
using Game.AccessControl;
using Game.Messages;
using KeyValue.Runtime;
using HarmonyLib;
using Railroader.Modding;
using Track.Crossings;
using UnityEngine;

namespace BrysonCrossingLamp
{
    public static class ManualControl
    {
        private const string PatchId = "joshi.bryson-crossing-lamp.manual";
        private static readonly Dictionary<GradeCrossingController, string> Keys = new Dictionary<GradeCrossingController, string>();
        private static KeyValueStorage sharedState;
        private static StateManager registeredManager;
        private static string brysonHierarchy;
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
                log.Info("AUTO/CLOSE circuit override installed; synchronized multiplayer controls available.");
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
        public static void BeginWorld(string customBrysonHierarchy)
        {
            Reset();
            if (!Available || StateManager.Shared == null) return;
            brysonHierarchy = customBrysonHierarchy;
            sharedState = new KeyValueStorage();
            registeredManager = StateManager.Shared;
            registeredManager.RegisterPropertyObject(CrossingControlProtocol.ObjectId, sharedState, new ControlAccess());
            // A joining client retains the restored host snapshot. Only a newly loaded host resets AUTO.
            if (StateManager.IsHost)
                foreach (string key in CrossingControlProtocol.Keys) sharedState.Set(key, Value.Bool(false));
        }
        public static void Reset()
        {
            if (sharedState != null) sharedState.OnSetValueLocal = null;
            if (registeredManager != null && registeredManager == StateManager.Shared)
                registeredManager.UnregisterPropertyObject(CrossingControlProtocol.ObjectId);
            registeredManager = null;
            sharedState = null;
            Keys.Clear();
        }
        private static string KeyFor(GradeCrossingController crossing)
        {
            if (crossing == null) return null;
            if (Keys.TryGetValue(crossing, out string key)) return key;
            string path = crossing.transform.name;
            for (Transform parent = crossing.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            for (int i = 0; i < CrossingDefinition.All.Length; i++)
            {
                string expected = i == 0 && !string.IsNullOrWhiteSpace(brysonHierarchy)
                    ? brysonHierarchy : CrossingDefinition.All[i].Hierarchy;
                if (path == expected) { key = CrossingControlProtocol.Keys[i]; break; }
            }
            Keys[crossing] = key;
            return key;
        }
        public static bool IsClosed(GradeCrossingController crossing)
        {
            string key = KeyFor(crossing);
            return sharedState != null && key != null && sharedState.Get(key).BoolValue;
        }
        public static bool CanControl(GradeCrossingController crossing) => Available && sharedState != null &&
            crossing != null && crossing.isActiveAndEnabled && crossing.circuitSets != null && crossing.circuitSets.Count > 0 &&
            KeyFor(crossing) != null && StateManager.CheckAuthorizedToChangeProperty(CrossingControlProtocol.ObjectId, KeyFor(crossing));
        public static bool Toggle(GradeCrossingController crossing)
        {
            if (!CanControl(crossing)) return false;
            // Same authorized message path as the game's own networked switches.
            StateManager.ApplyLocal(new PropertyChange(CrossingControlProtocol.ObjectId, KeyFor(crossing),
                new BoolPropertyValue(!IsClosed(crossing))));
            return true;
        }
        public static bool ForceClosed(GradeCrossingController crossing) =>
            Available && IsClosed(crossing);
        private sealed class ControlAccess : IPropertyAccessControlDelegate
        {
            public AuthorizationRequirementInfo AuthorizationRequirementForPropertyWrite(string key) =>
                new AuthorizationRequirementInfo(CrossingControlProtocol.IsControlKey(key)
                    ? AuthorizationRequirement.MinimumLevelPassenger : AuthorizationRequirement.HostOnly);
        }
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
