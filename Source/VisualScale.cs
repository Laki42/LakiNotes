using System;

namespace Laki
{
    internal static class VisualScale
    {
        // 1.40.8 NoteController.Init sets noteTransform.localScale = one * uniformScale.
        // Small Notes supplies 0.5. Compensate only that verified, uniform local scale.
        internal static bool TryCompensation(float uniformScale, float x, float y, float z, out float inverse)
        {
            inverse = 1;
            if (!Finite(uniformScale) || uniformScale < .5f || uniformScale > 1.6f ||
                !Finite(x) || !Finite(y) || !Finite(z) ||
                Math.Abs(x - uniformScale) > .0001f || Math.Abs(y - uniformScale) > .0001f ||
                Math.Abs(z - uniformScale) > .0001f) return false;
            inverse = 1 / uniformScale;
            return true;
        }
        internal static float Offset(float extent, float uniformScale) => Math.Max(.20f, extent) * uniformScale + .072f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
