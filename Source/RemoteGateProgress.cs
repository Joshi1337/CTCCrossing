namespace BrysonCrossingLamp
{
    // Tracks logical travel while Unity disables the distant visual animation.
    public sealed class RemoteGateProgress
    {
        public float Fraction { get; private set; }
        public bool Available { get; private set; }
        public bool UsingObservation { get; private set; }
        public bool Closed => Available && Fraction >= 0.9999f;
        public void Update(bool command, float elapsed, float travelSeconds, float? observedFraction)
        {
            Available = false;
            UsingObservation = false;
            if (!Finite(travelSeconds) || travelSeconds <= 0f || !Finite(elapsed) || elapsed < 0f) return;
            if (observedFraction.HasValue && Finite(observedFraction.Value))
            {
                Fraction = Clamp(observedFraction.Value);
                UsingObservation = true;
            }
            else
            {
                float distance = elapsed / travelSeconds;
                Fraction = Clamp(Fraction + (command ? distance : -distance));
            }
            Available = true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
