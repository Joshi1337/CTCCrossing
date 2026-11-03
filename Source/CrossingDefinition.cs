using System;
using System.Linq;
namespace BrysonCrossingLamp
{
    public sealed class CrossingDefinition
    {
        public readonly string Name, Label, Hierarchy;
        public readonly string[] Headings;
        public CrossingDefinition(string name, string label, string hierarchy, params string[] headings)
        { Name = name; Label = label; Hierarchy = hierarchy; Headings = headings; }
        public bool MatchesHeading(string text)
        {
            string normalized = Normalize(text);
            return normalized.Length > 0 && Headings.Any(h => Normalize(h) == normalized);
        }
        private static string Normalize(string text) => new string((text ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        public static readonly CrossingDefinition[] All = {
            new CrossingDefinition("Bryson", "Crossing BRYSON", "World/Large Scenery/Bryson/Grade Crossing - Bryson Everett", "BRYSON"),
            new CrossingDefinition("Governors Island", "Crossing GOV. ISLAND", "World/Roads Bryson/US19 Gov Island/Grade Crossing Setup",
                "GOVERNORS ISLAND", "GOVERNOR'S ISLAND", "GOV ISLAND", "GOV. ISLAND", "GOVERNORS IS.", "GOV. IS."),
            new CrossingDefinition("Sylva", "Crossing SYLVA", "World/Roads Sylva/Grade Crossing - Allen", "SYLVA"),
            new CrossingDefinition("Andrews", "Crossing ANDREWS", "World/Roads Andrews/Grade Crossing Setup 2 Tracks", "ANDREWS")
        };
    }
}
