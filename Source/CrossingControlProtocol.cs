namespace BrysonCrossingLamp
{
    // Stable keys, independent of local scene instance IDs and translated panel labels.
    public static class CrossingControlProtocol
    {
        public const string ObjectId = "joshi.ctccrossing.controls.v1";
        public static readonly string[] Keys = { "bryson", "governors-island", "sylva", "andrews" };
        public static bool IsControlKey(string key) => System.Array.IndexOf(Keys, key) >= 0;
    }
}
