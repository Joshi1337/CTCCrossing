namespace BrysonCrossingLamp
{
    // The command alone is never a closed-position confirmation.
    public static class GateConfirmation
    {
        // One cycle per second: 0.5 s on, 0.5 s off. Unknown feedback stays dark.
        public static bool LampIsLit(bool command, bool allClosed, bool available, float elapsed)
        {
            if (!command || !available) return false;
            if (allClosed) return true;
            return !float.IsNaN(elapsed) && !float.IsInfinity(elapsed) && elapsed >= 0f
                && elapsed % 1f < 0.5f;
        }

        public static bool IsAtClosedEnd(bool command, bool inverted, float time, float length)
        {
            if (!command || float.IsNaN(time) || float.IsInfinity(time) ||
                float.IsNaN(length) || float.IsInfinity(length) || length <= 0f) return false;
            const float toleranceSeconds = 0.001f;
            return inverted ? time <= toleranceSeconds : time >= length - toleranceSeconds;
        }
    }
}
