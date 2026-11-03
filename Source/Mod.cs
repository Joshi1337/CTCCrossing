using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Helpers.Animation;
using KeyValue.Runtime;
using Railroader.Modding;
using RollingStock.Controls;
using Track.Crossings;
using Track.Signals;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

namespace BrysonCrossingLamp
{
    public sealed class Mod : IModEntryPoint, IWorldLifecycle
    {
        public static string Version => typeof(Mod).Assembly.GetName().Version.ToString(3);
        private IModContext context;
        private GameObject runner;
        public void OnModLoad(IModContext value)
        {
            context = value;
            ManualControl.Install(context.Log);
            context.Log.Info($"CTCCrossing {Mod.Version} loaded. AUTO/CLOSE controls; F8 toggles diagnostics.");
        }
        public void OnWorldLoad()
        {
            Stop();
            runner = new GameObject("BrysonCrossingLamp.Runtime");
            Object.DontDestroyOnLoad(runner);
            var indicators = new List<LampRuntime>();
            foreach (var definition in CrossingDefinition.All)
            {
                var indicator = runner.AddComponent<LampRuntime>();
                indicator.Initialize(context, definition);
                indicators.Add(indicator);
            }
            runner.AddComponent<LampDiagnostics>().Indicators = indicators;
        }
        public void OnWorldUnload() { Stop(); }
        public void OnModUnload() { Stop(); ManualControl.Uninstall(); }
        private void Stop()
        {
            ManualControl.Reset();
            if (runner != null) Object.Destroy(runner);
            runner = null;
        }
    }

    [Serializable]
    public sealed class Settings
    {
        public string crossingHierarchy = "";
        public float maxCrossingDistanceMetres = 600f;
        // Metres in the chosen native lamp's orientation, unaffected by its scale.
        public float offsetRightMetres = 0f;
        public float offsetUpMetres = 0.030f;
        public float offsetForwardMetres = 0f;
        public bool showLabel = true;
        public float labelYawDegrees = 180f;
    }

    public sealed class LampRuntime : MonoBehaviour
    {
        private static readonly FieldInfo PlayableField = typeof(KeyValueBoolAnimator)
            .GetField("_playable", BindingFlags.Instance | BindingFlags.NonPublic);
        private IModContext context;
        private Settings settings;
        private CrossingDefinition definition;
        private CTCPanelController panel;
        private CTCPanelLamp anchor;
        private Transform stationHeading;
        private Vector3 headingCenter;
        private GradeCrossingController crossing;
        private KeyValueObject crossingValues;
        private KeyValueBoolAnimator[] gates = Array.Empty<KeyValueBoolAnimator>();
        private GameObject lampRoot;
        private Material lensMaterial;
        private TextMesh switchLabel;
        private Transform switchHandle;
        private Color unlit, lit;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private float nextDiscovery, nextRead;
        private bool isLit, loggedError;
        private bool wasClosing;
        private float closingStarted;
        private float lastStateTime = -1f;
        private readonly Dictionary<int, RemoteGateProgress> gateProgress = new Dictionary<int, RemoteGateProgress>();
        private string status = "Warte auf das 3D-CTC-Pult ...";
        private string lastLoggedStatus, crossingPath = "noch nicht gefunden";
        private string anchorPath = "";
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Initialize(IModContext value, CrossingDefinition crossingDefinition)
        {
            context = value;
            definition = crossingDefinition;
            settings = new Settings();
            string path = Path.Combine(context.ModDirectory, "settings.json");
            try
            {
                if (File.Exists(path)) settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path)) ?? settings;
            }
            catch (Exception ex) { context.Log.Error(ex, "Cannot read settings; using defaults."); }
        }

        private void Update()
        {
            if (context == null) return;
            try
            {
                if (Time.unscaledTime >= nextDiscovery)
                {
                    nextDiscovery = Time.unscaledTime + 5f;
                    Discover();
                }
                if (Time.unscaledTime >= nextRead)
                {
                    nextRead = Time.unscaledTime + 0.1f;
                    ReadState();
                }
            }
            catch (Exception ex)
            {
                SetLamp(false);
                SetStatus("Keine sichere Rueckmeldung (Details im Player.log).");
                if (!loggedError) { context.Log.Error(ex, "Crossing lamp update failed; lamp stays off."); loggedError = true; }
            }
        }

        private void Discover()
        {
            var currentPanel = CTCPanelController.Shared;
            if (currentPanel == null)
            {
                ClearLamp(); panel = null; crossing = null; crossingValues = null;
                gates = Array.Empty<KeyValueBoolAnimator>();
                return;
            }
            if (panel != currentPanel)
            {
                ClearLamp(); panel = currentPanel; crossing = null; crossingValues = null;
            }
            if (lampRoot == null)
            {
                var lamps = panel.GetComponentsInChildren<CTCPanelLamp>(true)
                    .Where(l => l.lampRenderer != null && l.palette != null && l.gameObject.scene.IsValid()).ToArray();
                if (lamps.Length == 0) { SetStatus("Keine native Pultlampe gefunden."); return; }
                // Anchor to the actual BRYSON heading, not the global lamp row.
                var headings = panel.GetComponentsInChildren<TMP_Text>(true)
                    .Where(t => definition.MatchesHeading(t.text)).Select(t => t.transform)
                    .Concat(panel.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                        .Where(t => definition.MatchesHeading(t.text)).Select(t => t.transform))
                    .Concat(panel.GetComponentsInChildren<TextMesh>(true)
                        .Where(t => definition.MatchesHeading(t.text)).Select(t => t.transform));
                var heading = headings
                    .OrderBy(t => lamps.Min(l => Vector3.SqrMagnitude(l.transform.position - t.position)))
                    .FirstOrDefault();
                if (heading == null)
                {
                    SetStatus(definition.Name + "-Beschriftung am Pult nicht gefunden.");
                    return;
                }
                stationHeading = heading;
                var headingRect = heading as RectTransform;
                headingCenter = headingRect != null ? headingRect.TransformPoint(headingRect.rect.center) : heading.position;
                anchor = lamps.OrderBy(l => Vector3.SqrMagnitude(l.transform.position - headingCenter)).First();
                if (definition.Name == "Governors Island")
                {
                    // Pick the approach-block lens closest to the siding heading.
                    // Scene object origins can be offset from the visible lens centers.
                    var approachLamp = lamps.Where(l =>
                        (l.mode == CTCPanelLamp.Mode.BlockOccupancy &&
                            (l.targetId == "br-gi-0" || l.targetId == "br-gi-1")) ||
                        l.name == "Block br-gi-0" || l.name == "Block br-gi-1")
                        .OrderBy(l => Mathf.Abs(Vector3.Dot(LensCenter(l.lampRenderer) - headingCenter, stationHeading.right)))
                        .FirstOrDefault();
                    if (approachLamp == null)
                    {
                        SetStatus("Gov Island: Pultlampen der Annaeherungsbloecke nicht gefunden.");
                        return;
                    }
                    anchor = approachLamp;
                    headingCenter += stationHeading.right * Vector3.Dot(
                        LensCenter(approachLamp.lampRenderer) - headingCenter, stationHeading.right);
                    context.Log.Info("Gov Island alignment block: " + approachLamp.name + " / " + approachLamp.targetId);
                }
                BuildLamp();
            }
            if (anchor == null) return;

            // Explicit paths verified in the user's game log: never bind a remote lamp to Bryson.
            string target = definition.Name == "Bryson" && !string.IsNullOrWhiteSpace(settings.crossingHierarchy)
                ? settings.crossingHierarchy : definition.Hierarchy;
            var candidates = Object.FindObjectsByType<GradeCrossingController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene.IsValid() && Hierarchy(c.transform) == target).ToArray();
            GradeCrossingController selected = candidates.Length == 1 ? candidates[0] : null;
            if (selected != crossing)
            {
                crossing = selected;
                gateProgress.Clear();
                lastStateTime = -1f;
                crossingValues = crossing != null ? crossing.GetComponent<KeyValueObject>() : null;
                crossingPath = crossing != null ? Hierarchy(crossing.transform) : "keine eindeutige Zuordnung";
                context.Log.Info(definition.Name + " selected crossing: " + crossingPath);
                SetLamp(false);
            }
            gates = crossingValues == null ? Array.Empty<KeyValueBoolAnimator>() :
                Object.FindObjectsByType<KeyValueBoolAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(a => a.key == "gates" && a.GetComponentInParent<KeyValueObject>() == crossingValues).ToArray();
        }

        private void BuildLamp()
        {
            // An inactive staging parent prevents cloned native scripts subscribing to CTC state.
            var staging = new GameObject("BrysonCrossingLamp.Staging");
            staging.SetActive(false);
            GameObject clone = null;
            try
            {
                clone = Object.Instantiate(anchor.gameObject, staging.transform, false);
                clone.name = definition.Name + " Crossing Closed Indicator";
                var clonedLamp = clone.GetComponent<CTCPanelLamp>();
                var lens = clonedLamp.lampRenderer;
                unlit = anchor.palette.red.unlit;
                lit = anchor.palette.red.lit * anchor.palette.red.emissiveIntensity;
                // This copy is purely visual. No inherited switches, observers or pick targets.
                foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                {
                    // The source may currently be hidden by CTCPanelCuller.
                    // Its Awake-time renderer set does not include our new copies,
                    // so a cloned disabled renderer would otherwise stay hidden forever.
                    renderer.enabled = true;
                    renderer.forceRenderingOff = false;
                    Material[] materials = renderer.materials;
                    ownedMaterials.AddRange(materials);
                    if (renderer == lens && materials.Length > 0) lensMaterial = materials[0];
                }
                if (lensMaterial == null) throw new InvalidOperationException("Native lamp has no lens material.");
                clone.SetActive(false);
                clone.transform.SetParent(anchor.transform.parent, false);
                clone.transform.localScale = anchor.transform.localScale;
                clone.transform.rotation = anchor.transform.rotation;
                clone.transform.position = headingCenter
                    + stationHeading.right * settings.offsetRightMetres
                    + stationHeading.up * settings.offsetUpMetres
                    + stationHeading.forward * settings.offsetForwardMetres;
                if (definition.Name == "Governors Island")
                {
                    float correction = Vector3.Dot(headingCenter - LensCenter(lens), stationHeading.right)
                        + settings.offsetRightMetres;
                    clone.transform.position += stationHeading.right * correction;
                }
                lampRoot = clone;
                anchorPath = Hierarchy(stationHeading);
                if (settings.showLabel) AddLabel();
                AddManualSwitch();
                SetLamp(false);
                clone.SetActive(true);
                context.Log.Info(definition.Name + " 3D crossing lamp created above heading: " + anchorPath);
            }
            catch
            {
                if (clone != null) Object.Destroy(clone);
                lampRoot = null;
                throw;
            }
            finally { Object.Destroy(staging); }
        }

        private void AddLabel()
        {
            // Child of the native parent with world-size text to avoid inherited model scale.
            var label = new GameObject(definition.Label + " Label");
            label.transform.SetParent(lampRoot.transform, false);
            var text = label.AddComponent<TextMesh>();
            text.text = definition.Label;
            text.fontSize = 48;
            text.characterSize = 0.0015f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.9f, 0.9f, 0.85f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            label.transform.rotation = anchor.transform.rotation * Quaternion.Euler(0f, settings.labelYawDegrees, 0f);
            label.transform.position = lampRoot.transform.position + stationHeading.up * 0.012f;
            if (definition.Name == "Governors Island")
                label.transform.position += stationHeading.right * (Vector3.Dot(
                    headingCenter - label.transform.position, stationHeading.right) + settings.offsetRightMetres);
            Vector3 scale = lampRoot.transform.lossyScale;
            label.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        }

        public TooltipInfo SwitchTooltip => new TooltipInfo(definition.Name + " Crossing - " +
            (ManualControl.IsClosed(crossing) ? "CLOSE" : "AUTO"),
            !ManualControl.Available ? "Manual control unavailable. See Player.log." :
            !Game.State.StateManager.IsHost ? "Host control only." :
            !ManualControl.CanControl(crossing) ? "Waiting for crossing controller." :
            ManualControl.IsClosed(crossing) ? "Click: return to AUTO (train detection)." :
                "Click: CLOSE and hold gates closed.");

        public void ToggleManual()
        {
            if (!ManualControl.Toggle(crossing))
            {
                context.Log.Warning(definition.Name + ": " + SwitchTooltip.Text);
                return;
            }
            context.Log.Info(definition.Name + " operator selected " + (ManualControl.IsClosed(crossing) ? "CLOSE" : "AUTO"));
            RefreshSwitch();
        }

        private void AddManualSwitch()
        {
            var root = new GameObject(definition.Name + " AUTO CLOSE Switch");
            root.transform.SetParent(lampRoot.transform, false);
            root.transform.rotation = stationHeading.rotation;
            Vector3 parentScale = lampRoot.transform.lossyScale;
            root.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            root.transform.position = lampRoot.transform.position + stationHeading.right * 0.040f
                - stationHeading.forward * 0.004f;
            // Use the native clickable CTC controls' layer where available.
            var nativeKnob = panel.GetComponentInChildren<CTCPanelKnob>(true);
            var nativeCollider = nativeKnob != null ? nativeKnob.GetComponentInChildren<Collider>(true) : null;
            int layer = nativeCollider != null ? nativeCollider.gameObject.layer : anchor.gameObject.layer;
            root.layer = layer;
            root.AddComponent<CrossingToggle>().Owner = this;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Switch Plate";
            plate.layer = layer;
            plate.transform.SetParent(root.transform, false);
            plate.transform.localScale = new Vector3(0.017f, 0.019f, 0.004f);
            var plateMaterial = new Material(anchor.lampRenderer.sharedMaterial);
            plateMaterial.color = new Color(0.12f, 0.12f, 0.12f);
            plateMaterial.SetColor(EmissionColor, Color.black);
            ownedMaterials.Add(plateMaterial);
            plate.GetComponent<Renderer>().sharedMaterial = plateMaterial;

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "AUTO CLOSE Handle";
            handle.layer = layer;
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            handle.transform.localScale = new Vector3(0.004f, 0.013f, 0.004f);
            var handleMaterial = new Material(anchor.lampRenderer.sharedMaterial);
            handleMaterial.color = new Color(0.8f, 0.8f, 0.75f);
            handleMaterial.EnableKeyword("_EMISSION");
            handleMaterial.SetColor(EmissionColor, new Color(0.2f, 0.2f, 0.18f));
            ownedMaterials.Add(handleMaterial);
            handle.GetComponent<Renderer>().sharedMaterial = handleMaterial;
            switchHandle = handle.transform;

            var labelObject = new GameObject("AUTO CLOSE Position");
            labelObject.transform.SetParent(root.transform, false);
            switchLabel = labelObject.AddComponent<TextMesh>();
            switchLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            switchLabel.fontSize = 48;
            switchLabel.characterSize = 0.0013f;
            switchLabel.anchor = TextAnchor.UpperCenter;
            switchLabel.alignment = TextAlignment.Center;
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = switchLabel.font.material;
            labelObject.transform.rotation = anchor.transform.rotation * Quaternion.Euler(0f, settings.labelYawDegrees, 0f);
            labelObject.transform.position = root.transform.position - stationHeading.up * 0.013f;
            RefreshSwitch();
        }

        private void RefreshSwitch()
        {
            bool close = ManualControl.IsClosed(crossing);
            if (switchLabel != null)
            {
                switchLabel.text = !ManualControl.Available ? "N/A" : close ? "CLOSE" : "AUTO";
                switchLabel.color = close ? new Color(1f, 0.75f, 0.25f) : Color.white;
            }
            if (switchHandle != null) switchHandle.localRotation = Quaternion.Euler(0f, 0f, close ? -35f : 35f);
        }

        private static Vector3 LensCenter(Renderer renderer) =>
            renderer.transform.TransformPoint(renderer.localBounds.center);

        private void ReadState()
        {
            float now = Time.time;
            float elapsed = lastStateTime < 0f ? 0f : Mathf.Max(0f, now - lastStateTime);
            lastStateTime = now;
            bool closed = false;
            bool closing = false;
            string reason;
            if (lampRoot == null) reason = "Warte auf das 3D-CTC-Pult ...";
            else if (crossing == null || crossingValues == null) reason = "Kein eindeutig zugeordneter Bahnuebergang.";
            else if (!crossing.gameObject.activeInHierarchy) reason = "Bahnuebergang nicht geladen/aktiv.";
            else if (gates.Length == 0) reason = "Keine Schrankenanimation zugeordnet.";
            else
            {
                bool command = crossingValues["gates"].BoolValue;
                closed = command;
                bool unavailable = false;
                bool remote = false;
                foreach (var gate in gates)
                {
                    if (gate == null || gate.animationClip == null || gate.animationClip.isLooping ||
                        float.IsNaN(gate.speed) || float.IsInfinity(gate.speed) || gate.speed <= 0f)
                    { closed = false; unavailable = true; break; }
                    int gateId = gate.GetInstanceID();
                    if (!gateProgress.TryGetValue(gateId, out var progress))
                    {
                        progress = new RemoteGateProgress();
                        gateProgress.Add(gateId, progress);
                    }
                    float duration = gate.animationClip.length;
                    float? observed = null;
                    if (gate.isActiveAndEnabled && PlayableField != null)
                    {
                        var animator = gate.GetComponent<Animator>();
                        var handle = PlayableField.GetValue(gate) as PlayableHandle;
                        if (animator != null && animator.isActiveAndEnabled && handle != null && duration > 0f)
                        {
                            float animationTime = handle.Time;
                            if (!float.IsNaN(animationTime) && !float.IsInfinity(animationTime))
                                observed = gate.invert ? 1f - animationTime / duration : animationTime / duration;
                        }
                    }
                    progress.Update(command, elapsed, duration / gate.speed, observed);
                    if (!progress.Available) { closed = false; unavailable = true; break; }
                    remote |= !progress.UsingObservation;
                    if (!progress.Closed) closed = false;
                }
                closing = command && !closed && !unavailable;
                reason = unavailable ? "Schrankenlaufzeit nicht verfuegbar: Leuchte aus." :
                    closed ? "GESCHLOSSEN: rote Leuchte an." :
                    command ? "Schranken schliessen: Leuchte blinkt rot." : "Offen / oeffnend: Leuchte aus.";
                if (!unavailable && remote) reason += " [Fernanzeige: berechnete Stellung]";
            }
            if (closing && !wasClosing) closingStarted = Time.unscaledTime;
            wasClosing = closing;
            SetLamp(GateConfirmation.LampIsLit(closed || closing, closed, closed || closing,
                Time.unscaledTime - closingStarted));
            SetStatus(reason);
            RefreshSwitch();
        }

        private void SetStatus(string value)
        {
            status = value;
            if (lastLoggedStatus == value || context == null) return;
            lastLoggedStatus = value;
            context.Log.Info(definition.Name + ": " + value + " | gate animations=" + gates.Length);
        }
        private void SetLamp(bool value)
        {
            isLit = value;
            if (lensMaterial == null) return;
            lensMaterial.color = unlit;
            lensMaterial.EnableKeyword("_EMISSION");
            lensMaterial.SetColor(EmissionColor, value ? lit : Color.black);
        }
        private void ClearLamp()
        {
            if (lampRoot != null) Object.Destroy(lampRoot);
            lampRoot = null; anchor = null; lensMaterial = null; switchLabel = null; switchHandle = null; isLit = false; wasClosing = false;
            foreach (var material in ownedMaterials) if (material != null) Object.Destroy(material);
            ownedMaterials.Clear();
        }
        private void OnDestroy() { ClearLamp(); }
        public string DiagnosticTitle => definition.Name + " | " + (ManualControl.IsClosed(crossing) ? "CLOSE" : "AUTO") + " | " + (isLit ? "ROT" : "AUS") + " | Animationen: " + gates.Length;
        public string DiagnosticStatus => status;
        public string DiagnosticCrossing => crossingPath;
        public string DiagnosticAnchor => anchorPath;
        private static string Hierarchy(Transform item)
        {
            var names = new List<string>();
            for (var t = item; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
    public sealed class LampDiagnostics : MonoBehaviour
    {
        public List<LampRuntime> Indicators = new List<LampRuntime>();
        private bool visible;
        private Vector2 scroll;
        private void OnGUI()
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.F8)
            { visible = !visible; evt.Use(); }
            if (!visible) return;
            GUILayout.BeginArea(new Rect(20, 70, 720, Mathf.Min(620, Screen.height - 90)), GUI.skin.box);
            GUILayout.Label($"CTCCrossing {Mod.Version} - Diagnose (F8 schliesst)");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var indicator in Indicators)
            {
                if (indicator == null) continue;
                GUILayout.Label(indicator.DiagnosticTitle);
                GUILayout.Label(indicator.DiagnosticStatus);
                GUILayout.Label("Uebergang: " + indicator.DiagnosticCrossing);
                GUILayout.Label("Pult: " + indicator.DiagnosticAnchor);
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

}

