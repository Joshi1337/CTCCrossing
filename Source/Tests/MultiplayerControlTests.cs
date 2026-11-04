using System;
using System.Collections.Generic;
using BrysonCrossingLamp;
using Game.State;
using KeyValue.Runtime;
using Track.Crossings;
using UnityEngine;

// Exercises the production ManualControl against an in-memory transport boundary.
// This is not a replacement for a two-PC Steam multiplayer test.
public static class MultiplayerControlTests
{
    public static void Run()
    {
        ManualControl.Install(new TestLog());
        var crossing = Crossing(CrossingDefinition.All[0].Hierarchy);
        var other = Crossing(CrossingDefinition.All[1].Hierarchy);
        var host = new StateManager();
        StateManager.Shared = host; StateManager.IsHost = true;
        ManualControl.BeginWorld(null);
        Check(ManualControl.CanControl(crossing), "Host can control crossing");
        Check(ManualControl.Toggle(crossing) && ManualControl.ForceClosed(crossing), "Host CLOSE applied");
        Check(!ManualControl.IsClosed(other), "Other crossing unchanged");
        var snapshot = host.Snapshot();
        ManualControl.Reset();

        var client = new StateManager { Restored = snapshot };
        StateManager.Shared = client; StateManager.IsHost = false;
        ManualControl.BeginWorld(null);
        Check(ManualControl.IsClosed(crossing) && ManualControl.ForceClosed(crossing), "Late join retains host CLOSE, including client circuit");
        Check(client.LocalWrites == 0, "Joining client does not reset or broadcast AUTO");
        Check(ManualControl.CanControl(crossing), "Passenger client can control crossing");
        Check(ManualControl.Toggle(crossing) && !ManualControl.IsClosed(crossing), "Client sends AUTO through authorized game-message path");
        Check(client.LastMessage != null && client.LastMessage.ObjectId == CrossingControlProtocol.ObjectId &&
            client.LastMessage.Key == "bryson" && !client.LastMessage.Value.Value, "Network message targets correct crossing and mode");
        client.Data.Set("bryson", Value.Bool(true));
        Check(ManualControl.IsClosed(crossing), "Incoming state changes are reflected without cached switch positions");
        client.Allowed = false;
        Check(!ManualControl.Toggle(crossing) && ManualControl.IsClosed(crossing), "Unauthorized client sends no change");
        Check(client.Access.AuthorizationRequirementForPropertyWrite("gates").Requirement ==
            Game.AccessControl.AuthorizationRequirement.HostOnly, "Unknown keys never gain passenger access");
        Check(!ManualControl.CanControl(Crossing("World/Other Crossing")), "Unmapped crossing cannot issue a command");
        ManualControl.Reset();
        Check(!ManualControl.ForceClosed(crossing) && client.Data == null, "World unload unregisters controls and clears closure");

        StateManager.Shared = new StateManager { Restored = snapshot }; StateManager.IsHost = true;
        ManualControl.BeginWorld(null);
        Check(!ManualControl.IsClosed(crossing), "Reloaded host resets saved CLOSE to AUTO");
        ManualControl.Reset();
        ManualControl.BeginWorld("World/Custom Bryson");
        Check(ManualControl.Toggle(Crossing("World/Custom Bryson")), "Custom Bryson mapping supported independently of panel discovery");
        Check(!ManualControl.CanControl(crossing), "Default Bryson mapping disabled when overridden");
        ManualControl.Uninstall();
    }
    private static GradeCrossingController Crossing(string path)
    {
        Transform last = null;
        foreach (string name in path.Split('/')) last = new Transform { name = name, parent = last };
        return new GradeCrossingController { transform = last };
    }
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private sealed class TestLog : Railroader.Modding.IModLogger
    {
        public void Info(string message) { }
        public void Error(Exception error, string message) { throw new Exception(message, error); }
    }
}

// Minimal stand-ins for Unity and the game's API; no proprietary game assemblies in CI.
namespace UnityEngine
{
    public class Transform { public string name; public Transform parent; }
    public class MonoBehaviour { }
}
namespace Track.Crossings
{
    public class GradeCrossingController
    {
        public Transform transform;
        public bool isActiveAndEnabled = true;
        public List<int> circuitSets = new List<int> { 0 };
        private enum CircuitState { Quiet, Lights, Gates }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private CircuitState UpdateCircuit(int index) => CircuitState.Quiet;
    }
}
namespace KeyValue.Runtime
{
    public struct Value { public bool BoolValue; public static Value Bool(bool value) => new Value { BoolValue = value }; }
    public class KeyValueStorage
    {
        public readonly Dictionary<string, Value> Values = new Dictionary<string, Value>();
        public Action<string, Value> OnSetValueLocal;
        public Value Get(string key) => Values.TryGetValue(key, out var value) ? value : default;
        public void Set(string key, Value value) { Values[key] = value; OnSetValueLocal?.Invoke(key, value); }
    }
}
namespace Game.AccessControl
{
    public enum AuthorizationRequirement { HostOnly, MinimumLevelPassenger }
    public struct AuthorizationRequirementInfo
    {
        public AuthorizationRequirement Requirement;
        public AuthorizationRequirementInfo(AuthorizationRequirement requirement) { Requirement = requirement; }
    }
    public interface IPropertyAccessControlDelegate { AuthorizationRequirementInfo AuthorizationRequirementForPropertyWrite(string key); }
}
namespace Game.Messages
{
    public class BoolPropertyValue { public bool Value; public BoolPropertyValue(bool value) { Value = value; } }
    public class PropertyChange
    {
        public string ObjectId, Key; public BoolPropertyValue Value;
        public PropertyChange(string id, string key, BoolPropertyValue value) { ObjectId = id; Key = key; Value = value; }
    }
}
namespace Game.State
{
    public class StateManager
    {
        public static StateManager Shared;
        public static bool IsHost;
        public bool Allowed = true;
        public int LocalWrites;
        public KeyValueStorage Data;
        public Dictionary<string, Value> Restored;
        public Game.Messages.PropertyChange LastMessage;
        public Game.AccessControl.IPropertyAccessControlDelegate Access;
        public void RegisterPropertyObject(string id, KeyValueStorage data, Game.AccessControl.IPropertyAccessControlDelegate access)
        {
            Data = data; Access = access;
            if (Restored != null) foreach (var pair in Restored) data.Set(pair.Key, pair.Value);
            data.OnSetValueLocal = (key, value) => LocalWrites++;
        }
        public Dictionary<string, Value> Snapshot() => new Dictionary<string, Value>(Data.Values);
        public void UnregisterPropertyObject(string id) { Data = null; }
        public static bool CheckAuthorizedToChangeProperty(string id, string key) => Shared.Allowed &&
            (IsHost || Shared.Access.AuthorizationRequirementForPropertyWrite(key).Requirement == Game.AccessControl.AuthorizationRequirement.MinimumLevelPassenger);
        public static void ApplyLocal(Game.Messages.PropertyChange message)
        {
            if (!CheckAuthorizedToChangeProperty(message.ObjectId, message.Key)) throw new Exception("Unauthorized message");
            Shared.LastMessage = message; Shared.Data.Set(message.Key, Value.Bool(message.Value.Value));
        }
    }
}
namespace Railroader.Modding { public interface IModLogger { void Info(string message); void Error(Exception error, string message); } }
namespace BrysonCrossingLamp { public class LampRuntime { public TooltipInfo SwitchTooltip; public void ToggleManual() { } } }
public interface IPickable { }
public enum PickableActivationFilter { PrimaryOnly }
public enum PickableActivation { Primary }
public struct PickableActivateEvent { public PickableActivation Activation; }
public struct TooltipInfo { public static TooltipInfo Empty => default; }
