using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using BrysonCrossingLamp;

public static class ManualPatchTests
{
    public sealed class FakeCrossing
    {
        private enum CircuitState { Quiet, Lights, Gates }
        public bool Hold;
        public bool Host = true;
        public int Detected, AutomaticCalls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private CircuitState UpdateCircuit(int circuit)
        { AutomaticCalls++; return (CircuitState)Detected; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Evaluate() => (int)UpdateCircuit(0);
    }
    public static bool Forced(FakeCrossing crossing) => crossing.Host && crossing.Hold;
    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> code, ILGenerator generator) =>
        CircuitOverride.Inject(code, generator, typeof(ManualPatchTests).GetMethod(nameof(Forced)), 2);
    public static void Run()
    {
        var harmony = new Harmony("crossing-control-tests");
        var method = AccessTools.Method(typeof(FakeCrossing), "UpdateCircuit");
        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ManualPatchTests), nameof(Transpile)));
        try
        {
            var a = new FakeCrossing();
            var b = new FakeCrossing();
            Check(a.Evaluate() == 0, "AUTO clear crossing stays clear");
            a.Detected = 1;
            Check(a.Evaluate() == 1, "AUTO preserves advance warning");
            a.Detected = 0; a.Hold = true;
            int before = a.AutomaticCalls;
            Check(a.Evaluate() == 2 && a.AutomaticCalls == before, "CLOSE forces gates even without a train");
            Check(b.Evaluate() == 0, "Other crossing remains automatic");
            a.Hold = false; a.Detected = 2;
            Check(a.Evaluate() == 2, "Returning AUTO does not open under a train");
            a.Detected = 0;
            Check(a.Evaluate() == 0, "Returning AUTO opens after crossing clears");
            a.Hold = true; a.Host = false;
            Check(a.Evaluate() == 0, "Client cannot override host control");
        }
        finally { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
        var restored = new FakeCrossing { Hold = true };
        Check(restored.Evaluate() == 0, "Unpatch restores original logic");
    }
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
