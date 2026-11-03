using System;
using BrysonCrossingLamp;
var cases = new (string name, bool command, bool invert, float time, float length, bool expected)[] {
 ("Open gates", false, false, 0, 5, false),
 ("Command issued but motion not started", true, false, 0, 5, false),
 ("Closing half way", true, false, 2.5f, 5, false),
 ("Almost closed", true, false, 4.99f, 5, false),
 ("Fully closed", true, false, 5, 5, true),
 ("Unclamped completed animation", true, false, 12, 5, true),
 ("Opening command clears indication immediately", false, false, 5, 5, false),
 ("Reversing before closure", false, false, 2, 5, false),
 ("Inverted animation still closing", true, true, 1, 5, false),
 ("Inverted animation closed", true, true, 0, 5, true),
 ("Inverted opening command", false, true, 0, 5, false),
 ("No clip length", true, false, 5, 0, false),
 ("Invalid playback time", true, false, float.NaN, 5, false),
 ("Infinite playback time", true, false, float.PositiveInfinity, 5, false),
 ("Invalid duration", true, false, 5, float.NaN, false)
};
foreach(var c in cases) {
 bool actual = GateConfirmation.IsAtClosedEnd(c.command,c.invert,c.time,c.length);
 if(actual != c.expected) throw new Exception(c.name);
 Console.WriteLine("PASS " + c.name);
}
Console.WriteLine($"{cases.Length}/{cases.Length} passed");

var lampCases = new (string name, bool command, bool closed, bool available, float elapsed, bool expected)[] {
 ("Closing starts lit", true, false, true, 0f, true),
 ("Closing on half", true, false, true, 0.25f, true),
 ("Closing off half", true, false, true, 0.5f, false),
 ("Closing next cycle", true, false, true, 1f, true),
 ("Closed overrides dark phase", true, true, true, 0.75f, true),
 ("Closed remains lit", true, true, true, 15.5f, true),
 ("Opening clears even if gates still down", false, true, true, 0f, false),
 ("Missing feedback stays dark", true, false, false, 0f, false),
 ("Missing feedback cannot confirm closed", true, true, false, 0f, false),
 ("Open stays dark", false, false, true, 0f, false)
};
foreach(var c in lampCases) {
 if(GateConfirmation.LampIsLit(c.command,c.closed,c.available,c.elapsed) != c.expected)
  throw new Exception(c.name);
 Console.WriteLine("PASS " + c.name);
}
Console.WriteLine($"Total {cases.Length + lampCases.Length} passed");

// Reject cross-wiring between station labels; punctuation variants are accepted.
if (CrossingDefinition.All.Length != 4) throw new Exception("Expected four crossings");
var paths = new System.Collections.Generic.HashSet<string>();
foreach (var definition in CrossingDefinition.All) {
 if (!paths.Add(definition.Hierarchy)) throw new Exception("Duplicate crossing path");
 foreach (var heading in definition.Headings)
  if (!definition.MatchesHeading(heading)) throw new Exception("Heading mismatch: " + heading);
 foreach (var other in CrossingDefinition.All)
  if (other != definition && definition.MatchesHeading(other.Name)) throw new Exception("Cross-wired heading");
 if (definition.MatchesHeading(null) || definition.MatchesHeading("Crossing " + definition.Name))
  throw new Exception("Matched empty or generated label");
 Console.WriteLine("PASS unique crossing and heading mapping: " + definition.Name);
}
if (!CrossingDefinition.All[1].MatchesHeading("Governor’s Island")) throw new Exception("Curly apostrophe");
Console.WriteLine("PASS heading punctuation normalization");
// One crossing's blink phase/state must not depend on another's.
bool brysonClosed = GateConfirmation.LampIsLit(true, true, true, 0.75f);
bool sylvaClosingDark = GateConfirmation.LampIsLit(true, false, true, 0.75f);
bool andrewsUnknown = GateConfirmation.LampIsLit(true, true, false, 0f);
if (!brysonClosed || sylvaClosingDark || andrewsUnknown) throw new Exception("Crossing states not isolated");
Console.WriteLine("PASS simultaneous independent crossing states");

ManualPatchTests.Run();

var remote = new RemoteGateProgress();
remote.Update(true, 0f, 5f, null);
if (remote.Closed) throw new Exception("Remote starts conservatively");
remote.Update(true, 2f, 5f, null);
if (Math.Abs(remote.Fraction - 0.4f) > 0.001f || remote.Closed) throw new Exception("Remote partial close");
remote.Update(true, 3f, 5f, null);
if (!remote.Closed || remote.UsingObservation) throw new Exception("Remote completion");
remote.Update(false, 2f, 5f, null);
if (Math.Abs(remote.Fraction - 0.6f) > 0.001f) throw new Exception("Remote opening");
remote.Update(true, 1f, 5f, null);
if (Math.Abs(remote.Fraction - 0.8f) > 0.001f) throw new Exception("Remote reversal");
remote.Update(true, 0f, 5f, null);
if (Math.Abs(remote.Fraction - 0.8f) > 0.001f) throw new Exception("Paused game progressed");
remote.Update(true, 0.1f, 5f, 0.2f);
if (Math.Abs(remote.Fraction - 0.2f) > 0.001f || !remote.UsingObservation) throw new Exception("Visual resync");
remote.Update(true, 4f, 5f, null);
if (!remote.Closed) throw new Exception("Continues from last visible state");
remote.Update(true, 1f, 0f, null);
if (remote.Available || remote.Closed) throw new Exception("Invalid duration");
Console.WriteLine("PASS remote travel, opening, reversal, pause, visual resync and invalid metadata");
