#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    /// <summary>Editor-only automated production-input checks, not a manual playthrough.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class SibucaoStoryPlaybackProbe : MonoBehaviour
    {
        public string status = "Idle";
        public List<string> results = new List<string>();
        SibucaoSession S => SibucaoSession.Instance;
        SibucaoPlaythroughProbe input;
        bool observe, wasPlaying, observedEnd, skip, captured;
        string frozenState, error;
        Vector3 frozenPosition;
        Quaternion frozenView;
        float observedStart;
        int frozenHistory;
        void Awake()
        {
            if (System.IO.File.Exists("tmp/story/checks.txt")) results.AddRange(System.IO.File.ReadAllLines("tmp/story/checks.txt").Skip(2));
        }
        public static SibucaoStoryPlaybackProbe Get()
        {
            var p = FindFirstObjectByType<SibucaoStoryPlaybackProbe>();
            return p ? p : new GameObject("EDITOR ONLY story playback regression").AddComponent<SibucaoStoryPlaybackProbe>();
        }
        public void Run(string mode)
        {
            StopAllCoroutines(); input = SibucaoPlaythroughProbe.Get();
            Application.runInBackground = true;
            StartCoroutine(Guard(mode, mode == "negative" ? Negative() : mode == "restart" ? RestartDuringPlayback() : Traverse(mode == "skip" ? 2 : mode == "probe" ? 0 : 1, mode == "skip")));
        }
        IEnumerator Invoke(MonoBehaviour host, string method, params object[] args) =>
            (IEnumerator)host.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host, args);
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Save() { System.IO.Directory.CreateDirectory("tmp/story"); System.IO.File.WriteAllLines("tmp/story/checks.txt", new[] { DateTime.Now + " — AUTOMATED Play Mode fixtures; not manual/fresh-home play", status }.Concat(results)); }
        IEnumerator Guard(string name, IEnumerator work)
        {
            status = name; error = null; Save();
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            while (stack.Count > 0)
            {
                object current = null; bool next = false;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                catch (Exception e) { error = e.GetBaseException().Message; break; }
                if (!next) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            observe = false; SibucaoEditorInput.Reset();
            status = error == null ? "PASS " + name : "FAIL " + name + ": " + error;
            results.Add(status); Save();
        }
        IEnumerator Fixture()
        {
            var fixture = SibucaoPriorityOneProbe.Get();
            yield return Invoke(fixture, "Fixture", 0, true, 0);
        }
        IEnumerator Negative()
        {
            yield return Fixture();
            var b = S.physicalDecisions.stations[0].branches[1]; int before = S.story.PlaybackCount;
            S.player.Teleport(b.path[0].position + Vector3.up * .12f, b.station.transform.forward);
            yield return new WaitForSeconds(.5f);
            SibucaoEditorInput.Interact = true; yield return new WaitForSeconds(2);
            Check(!b.station.Resolved && !S.story.IsPlaying && S.story.PlaybackCount == before, "E + idle triggered a completion/cinematic");
            SibucaoEditorInput.Move = Vector2.up; yield return new WaitForSeconds(.7f); SibucaoEditorInput.Move = Vector2.zero;
            Check(b.Engaged && !S.story.IsPlaying && S.story.CapturedFrames > 0, "Partial traversal must record footage without playing it");
            S.Pause(); int capturedBefore = S.story.CapturedFrames;
            yield return new WaitForSecondsRealtime(.4f);
            Check(S.Mode == SibucaoMode.Paused && S.story.CapturedFrames == capturedBefore && !S.story.IsPlaying, "Pause continued capture or began playback");
            S.SetMode(SibucaoMode.Playing);
            S.story.enabled = false;
            Check(S.story.AllocatedFrames == 0 && b.Engaged, "Disable did not release footage independently of the action");
            S.story.enabled = true; yield return new WaitForSeconds(.3f);
            Check(S.story.CapturedFrames > 0 && !S.story.IsPlaying, "An existing engaged action did not resume capture");
            Check(!S.physicalDecisions.FinishPhysicalChoice(b.station, 1), "Resolver accepted unfinished action");
            S.story.QueueCompletedAction(b); yield return null;
            Check(!S.story.IsPlaying, "Unfinished action queued a cinematic");
            SibucaoEditorInput.Cancel = true; yield return null; yield return null;
            Check(!b.station.Resolved && !S.story.IsPlaying && S.story.AllocatedFrames == 0 && S.story.PlaybackCount == before, "Cancel leaked footage or triggered a cinematic");
            results.Add("PASS arrival/E/idle/partial traversal/cancel: no cinematic, no score; pause suspends capture; disable releases buffers; existing engaged action resumes recording");
        }
        IEnumerator RestartDuringPlayback()
        {
            yield return Fixture();
            var p = SibucaoBranchPlayProbe.Get();
            typeof(SibucaoBranchPlayProbe).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, input);
            yield return Invoke(p, "Exercise", S.physicalDecisions.stations[0].branches[1]);
            yield return null;
            Check(S.story.IsPlaying, "Restart check never reached a cinematic");
            yield return new WaitForSecondsRealtime(.4f);
            S.StartRun();
            Check(!S.story.IsPlaying && S.story.AllocatedFrames == 0 && S.Mode == SibucaoMode.Playing && Time.timeScale == 1, "Restart retained cinematic lock/resources");
            Check(S.state.physical.history.Count == 0 && !S.physicalDecisions.stations[0].Resolved, "Restart inherited old result");
            results.Add("PASS new run during playback: restores time/input, releases footage, clears old decision");
        }
        IEnumerator Traverse(int choice, bool skipPlayback)
        {
            yield return Fixture();
            int before = S.story.PlaybackCount;
            observe = true; skip = skipPlayback; wasPlaying = observedEnd = captured = false;
            if (choice == 0)
            {
                yield return ProbeCrossing();
            }
            else
            {
                var p = SibucaoBranchPlayProbe.Get();
                typeof(SibucaoBranchPlayProbe).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, input);
                yield return Invoke(p, "Exercise", S.physicalDecisions.stations[0].branches[choice]);
            }
            float deadline = Time.realtimeSinceStartup + 10;
            while (!observedEnd && Time.realtimeSinceStartup < deadline) yield return null;
            Check(observedEnd, "Post-completion cinematic not observed");
            Check(error == null, error);
            Check(S.story.PlaybackCount == before + 1 && S.story.LastChoice == choice, "Wrong/duplicate cinematic");
            Check(S.Mode == SibucaoMode.Playing && Time.timeScale == 1 && S.story.AllocatedFrames == 0, "Input/time/buffers not restored");
            Check(!S.physicalDecisions.FinishPhysicalChoice(S.physicalDecisions.stations[0], choice), "Duplicate resolution accepted");
            yield return null;
            Check(S.story.PlaybackCount == before + 1, "Duplicate replay fired");
            results.Add("PASS B1 C" + choice + ": real input finished before one cinematic; " + (skip ? "Space skipped" : "played to end") + "; score/items/clock/position unchanged during playback; buffers released");
            observe = false;
            // A seeded fence failure already applies a two-second stumble. Preserve it;
            // test control restoration after that consequence, not during its intended lock.
            var response = S.player.GetComponent<SibucaoPhysicalFeedback>();
            float recoverBy = Time.time + 3;
            while (response && response.remaining > 0 && Time.time < recoverBy) yield return null;
            var start = S.player.transform.position;
            SibucaoEditorInput.Move = Vector2.up; yield return new WaitForSeconds(.4f); SibucaoEditorInput.Move = Vector2.zero;
            Check(Vector3.Distance(start, S.player.transform.position) > .1f, "Movement did not resume");
        }
        IEnumerator ProbeCrossing()
        {
            var st = S.physicalDecisions.stations[0]; var b = st.branches[0];
            S.state.homeSeconds = SibucaoStormTimeline.FloodStart(S.home) + 110;
            yield return null; while (S.GetComponent<FloodSimulationManager>().IsCatchingUp) yield return null;
            var legacy = SibucaoFirstCrossingProbe.Get();
            typeof(SibucaoFirstCrossingProbe).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(legacy, input);
            S.player.Teleport(b.tool.transform.position - st.transform.forward * 1.2f + Vector3.up * .1f, st.transform.forward);
            yield return new WaitForSeconds(.35f);
            yield return Invoke(legacy, "GripVisibleTool", b.tool);
            for (int sample = 0; sample < 2; sample++)
            {
                Vector3 local = sample == 0 ? new Vector3(0, 0, -4) : new Vector3(-3.3f, 0, 2);
                yield return input.MoveTo(st.transform.TransformPoint(local), 20);
                yield return input.Aim(st.transform.TransformPoint(local + new Vector3(0, -.1f, 1)));
                float deadline = Time.time + 6;
                while (st.Attempt.probeSamples < sample + 1 && Time.time < deadline) yield return null;
                Check(st.Attempt.probeSamples >= sample + 1, "Physical stick did not settle at sample " + sample);
            }
            Check(!st.Resolved && !S.story.IsPlaying, "Samples alone triggered completion/playback");
            yield return input.MoveTo(b.path[0].position, 25);
            foreach (var point in b.path) yield return input.MoveTo(point.position, 25);
            yield return input.MoveTo(b.path.Last().position + st.transform.forward, 20);
            Check(st.Resolved && S.state.physical.results[0] == 0, "Probe traversal did not finish C0");
        }
        void LateUpdate()
        {
            if (!observe || !S || !S.story) return;
            bool now = S.story.IsPlaying;
            if (now && !wasPlaying)
            {
                frozenState = JsonUtility.ToJson(S.state); frozenPosition = S.player.transform.position;
                frozenView = S.player.view.transform.rotation; frozenHistory = S.state.physical.history.Count;
                observedStart = Time.unscaledTime;
                if (frozenHistory != 1 || !S.physicalDecisions.stations[0].Resolved) error = "Cinematic began before one result existed";
            }
            if (now || wasPlaying)
            {
                if (JsonUtility.ToJson(S.state) != frozenState) error = "Run state changed during cinematic";
                if (Vector3.Distance(frozenPosition, S.player.transform.position) > .001f || Quaternion.Angle(frozenView, S.player.view.transform.rotation) > .01f) error = "Live player moved during cinematic";
            }
            if (now)
            {
                if (Time.timeScale != 0 || S.Mode != SibucaoMode.Cinematic) error = "Cinematic did not freeze gameplay";
                if (!captured && S.story.Elapsed > .5f)
                {
                    captured = true; var path = "tmp/story/B1_C" + S.story.LastChoice;
                    System.IO.Directory.CreateDirectory("tmp/story");
                    ScreenCapture.CaptureScreenshot(path + "_presentation.png");
                    var old = RenderTexture.active; RenderTexture.active = (RenderTexture)S.story.CurrentFrame;
                    var image = new Texture2D(RenderTexture.active.width, RenderTexture.active.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                    System.IO.File.WriteAllBytes(path + "_footage.png", image.EncodeToPNG()); Destroy(image); RenderTexture.active = old;
                }
                // Deliberately hold gameplay controls; they must have no effect during presentation.
                SibucaoEditorInput.Move = Vector2.up; SibucaoEditorInput.Look = new Vector2(10, 0);
                if (skip && S.story.Elapsed > 1.2f) SibucaoEditorInput.SkipCinematic = true;
            }
            if (!now && wasPlaying)
            {
                observedEnd = true;
                SibucaoEditorInput.Move = SibucaoEditorInput.Look = Vector2.zero;
                float wall = Time.unscaledTime - observedStart;
                if (skip && wall > 2.5f) error = "Skip did not end promptly";
                results.Add("Observed " + (skip ? "skipped" : "full") + " playback " + wall.ToString("F2") + "s, history=" + frozenHistory);
            }
            wasPlaying = now;
        }
        void OnDestroy() { SibucaoEditorInput.Reset(); }
    }
}
#endif
