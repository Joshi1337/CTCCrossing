using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BrysonCrossingLamp
{
    public static class CircuitOverride
    {
        public static IEnumerable<CodeInstruction> Inject(IEnumerable<CodeInstruction> original,
            ILGenerator generator, MethodInfo predicate, int closedValue)
        {
            var code = original.Select(i => new CodeInstruction(i)).ToList();
            if (code.Count == 0) throw new InvalidOperationException("Empty crossing circuit method.");
            var automatic = generator.DefineLabel();
            code[0].labels.Add(automatic);
            return new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, predicate),
                new CodeInstruction(OpCodes.Brfalse, automatic),
                new CodeInstruction(OpCodes.Ldc_I4, closedValue),
                new CodeInstruction(OpCodes.Ret)
            }.Concat(code);
        }
    }
}
