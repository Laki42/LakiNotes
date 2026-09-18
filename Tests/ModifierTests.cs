using System;
using System.Linq;
using Laki;

internal static class ModifierTests
{
    internal static void Run(Action<bool, string> check)
    {
        var constructor = typeof(GameplayModifiers).GetConstructors().Single(c => c.GetParameters().Length == 15);
        foreach (bool small in new[] { false, true })
        foreach (bool pro in new[] { false, true })
        {
            var args = constructor.GetParameters().Select(p => p.Name == "smallCubes" ? (object)small :
                p.Name == "proMode" ? pro : Activator.CreateInstance(p.ParameterType)).ToArray();
            var modifiers = (GameplayModifiers)constructor.Invoke(args);
            float scale = modifiers.notesUniformScale;
            check(scale == (small ? .5f : 1f) && modifiers.proMode == pro, "Actual game modifier values");
            check(VisualScale.TryCompensation(scale, scale, scale, scale, out float inverse), "Modifier combination accepted");
            check(Math.Abs(scale * inverse - 1) < .00001f && inverse <= 2, "Stable visual and detached effect scale");
            foreach (float extent in new[] { .20f, .25f, .34f })
            {
                float center = VisualScale.Offset(extent, scale);
                // Largest motif .052, pulse 3.5%, rotation-independent unit radius, bob .005.
                float clearance = center - .052f * 1.035f - .005f - extent * scale;
                check(clearance > .01f, "All three motifs clear the note face with bob/pulse");
                check(center <= .4121f, "Decoration remains close to note");
            }
            check(modifiers.notesUniformScale == scale && modifiers.proMode == pro, "Modifier never mutated");
        }
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, 0, -.5f, .1f, 2f })
            check(!VisualScale.TryCompensation(bad, bad, bad, bad, out _), "Unsafe scale rejected");
        check(!VisualScale.TryCompensation(.5f, .5f, 1, .5f, out _), "Non-uniform custom scale rejected");
        check(VisualScale.Offset(.20f, 1) == .272f, "Normal-note origin unchanged");
        check(Math.Abs(VisualScale.Offset(.20f, .5f) - .172f) < .00001f, "Small-note spacing follows body size");
        Console.WriteLine("Modifiers: actual GameplayModifiers + production compensation, none / Small / Pro / Small+Pro: PASS (math, not rendering).");
    }
}
