using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace StormSim.Sibucao
{
    /// <summary>
    /// Desktop B1 story prototype. Replays rendered evidence of the completed action;
    /// never re-enacts physics, chooses a branch, or awards a second result.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class SibucaoStoryPlayback : MonoBehaviour
    {
        [Tooltip("First-crossing proof only. Other encounters need their own interaction/presentation review.")]
        public bool firstCrossingEnabled = true;
        const int Width = 768, Height = 432, Fps = 24, TailFrames = 60, HighlightFrames = 18;
        const float PlaybackSpeed = .85f, Fade = .18f;
        readonly List<RenderTexture> frames = new List<RenderTexture>();
        readonly List<float> frameTimes = new List<float>();
        RenderTexture[] tail, highlight;
        float[] times, highlightTimes;
        Camera captureCamera;
        SibucaoPhysicalOption recording, pending;
        PhysicalBranchAttempt recordedAttempt;
        int next, count, highlightCount, lastFrame = -1;
        float captureAt, highlightAt = -1, playbackAt, duration, toastRemaining, subtitleRemaining;
        bool playing;
        GUIStyle caption, skipStyle;
        SibucaoSession S => SibucaoSession.Instance;
        public bool IsPlaying => playing;
        public int CapturedFrames => count;
        public int PlaybackCount { get; private set; }
        public int LastChoice { get; private set; } = -1;
        public float Duration => duration;
        public float Elapsed => playing ? Time.unscaledTime - playbackAt : 0;
        public Texture CurrentFrame => frames.Count == 0 ? null : frames[FrameIndex()];
        public int AllocatedFrames => tail == null ? 0 : TailFrames + HighlightFrames;

        public void BeginAction(SibucaoPhysicalOption branch)
        {
            if (!isActiveAndEnabled || !firstCrossingEnabled || !branch || !branch.IsFirstCrossing ||
                !S || S.Mode != SibucaoMode.Playing || UnityEngine.XR.XRSettings.isDeviceActive) return;
            ResetPlayback();
            if (!S.player || !S.player.view || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            recording = branch;
            recordedAttempt = branch.Attempt;
            captureCamera = S.player.view;
            tail = new RenderTexture[TailFrames];
            highlight = new RenderTexture[HighlightFrames];
            times = new float[TailFrames];
            highlightTimes = new float[HighlightFrames];
            for (int i = 0; i < tail.Length; i++) tail[i] = MakeFrame(i);
            for (int i = 0; i < highlight.Length; i++) highlight[i] = MakeFrame(TailFrames + i);
            CameraCaptureBridge.AddCaptureAction(captureCamera, Capture);
        }

        RenderTexture MakeFrame(int index)
        {
            var frame = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32)
            {
                name = "B1 story frame " + index, hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false, autoGenerateMips = false, filterMode = FilterMode.Bilinear
            };
            frame.Create();
            return frame;
        }

        // URP's capture pass supplies the post-processed camera image without IMGUI HUD.
        // A bounded GPU-only ring avoids disk recordings, readbacks and a second world render.
        void Capture(RenderTargetIdentifier source, CommandBuffer commands)
        {
            if (!recording || playing || pending || !S || S.Mode != SibucaoMode.Playing ||
                Time.time < captureAt || lastFrame == Time.frameCount) return;
            commands.Blit(source, tail[next]);
            times[next] = Time.time;
            next = (next + 1) % TailFrames;
            count = Mathf.Min(count + 1, TailFrames);
            lastFrame = Time.frameCount;
            captureAt = Time.time + 1f / Fps;
            if ((highlightCount == 0 && count >= HighlightFrames) || (highlightAt >= 0 && Time.time >= highlightAt))
            {
                highlightCount = Mathf.Min(count, HighlightFrames);
                for (int i = 0; i < highlightCount; i++)
                {
                    int sourceIndex = (next - highlightCount + i + TailFrames) % TailFrames;
                    commands.Blit(tail[sourceIndex], highlight[i]);
                    highlightTimes[i] = times[sourceIndex];
                }
                highlightAt = -1;
            }
        }

        // Keep the latest actual depth-sampling moment, followed by the final traversal.
        public void MarkActionBeat(SibucaoPhysicalOption branch)
        {
            if (recording == branch && !playing) highlightAt = Time.time + .25f;
        }

        public void QueueCompletedAction(SibucaoPhysicalOption branch)
        {
            if (!recording || recording != branch || !S || S.Mode != SibucaoMode.Playing ||
                !branch.station.Resolved || S.state.physical.results[branch.station.index] != branch.choiceIndex ||
                !ReferenceEquals(recordedAttempt, branch.Attempt) || !recordedAttempt.finished) return;
            pending = branch;
        }

        void LateUpdate()
        {
            if (playing)
            {
                if (!S || S.Mode != SibucaoMode.Cinematic) { ResetPlayback(); return; }
                bool skip = Keyboard.current != null &&
                    (StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#if UNITY_EDITOR
                if (SibucaoEditorInput.Active) skip = SibucaoEditorInput.Take(ref SibucaoEditorInput.SkipCinematic);
#endif
                if ((skip && Elapsed > .15f) || Elapsed >= duration) Skip();
                return;
            }
            if (pending)
            {
                if (S && S.Mode == SibucaoMode.Playing) StartPlayback();
                else ResetPlayback();
                return;
            }
            if (recording && (!S || (S.Mode != SibucaoMode.Playing && S.Mode != SibucaoMode.Paused && S.Mode != SibucaoMode.Settings) ||
                !ReferenceEquals(recordedAttempt, recording.Attempt) || !recordedAttempt.engaged || recording.station.Resolved))
                ResetPlayback();
            // Loaded unfinished actions may already be engaged and never call Begin again.
            if (!recording && S && S.Mode == SibucaoMode.Playing && S.physicalDecisions &&
                S.physicalDecisions.stations != null && S.physicalDecisions.stations.Length > 0)
            {
                var station = S.physicalDecisions.stations[0];
                var active = station ? station.EngagedBranch : null;
                if (active) BeginAction(active);
            }
        }

        void StartPlayback()
        {
            CameraCaptureBridge.RemoveCaptureAction(captureCamera, Capture);
            if (count < 6) { ResetPlayback(); return; } // Never replace missing footage with a simulated action.
            int first = (next - count + TailFrames) % TailFrames;
            float end = 0;
            if (highlightCount > 1 && highlightTimes[highlightCount - 1] < times[first] - .1f)
            {
                float origin = Mathf.Max(highlightTimes[0], highlightTimes[highlightCount - 1] - .75f);
                for (int i = 0; i < highlightCount; i++)
                {
                    if (highlightTimes[i] < origin) continue;
                    frames.Add(highlight[i]); frameTimes.Add(highlightTimes[i] - origin);
                }
                if (frameTimes.Count > 0) end = frameTimes[frameTimes.Count - 1] + 1f / Fps;
            }
            // At low frame rates cap source duration too; a slow PC must not produce a long cutscene.
            float startTime = Mathf.Max(times[first], times[(next - 1 + TailFrames) % TailFrames] - 2.5f);
            for (int i = 0; i < count; i++)
            {
                int slot = (first + i) % TailFrames;
                if (times[slot] < startTime) continue;
                frames.Add(tail[slot]); frameTimes.Add(end + times[slot] - startTime);
            }
            duration = (frameTimes[frameTimes.Count - 1] + 1f / Fps) / PlaybackSpeed + Fade * 2;
            toastRemaining = Mathf.Max(0, S.toastUntil - Time.unscaledTime);
            subtitleRemaining = Mathf.Max(0, S.subtitleUntil - Time.unscaledTime);
            playbackAt = Time.unscaledTime;
            LastChoice = pending.choiceIndex;
            PlaybackCount++;
            playing = true;
            pending = null;
            S.SetMode(SibucaoMode.Cinematic);
        }

        int FrameIndex()
        {
            float time = Mathf.Max(0, Elapsed - Fade) * PlaybackSpeed;
            int i = 0;
            while (i + 1 < frameTimes.Count && frameTimes[i + 1] <= time) i++;
            return i;
        }

        public void Skip()
        {
            if (!playing) return;
            if (S && S.Mode == SibucaoMode.Cinematic)
            {
                S.toastUntil = Time.unscaledTime + toastRemaining;
                S.subtitleUntil = Time.unscaledTime + subtitleRemaining;
                S.SetMode(SibucaoMode.Playing);
            }
            ResetPlayback();
        }

        public void ResetPlayback()
        {
            CameraCaptureBridge.RemoveCaptureAction(captureCamera, Capture);
            captureCamera = null;
            if (playing && S && S.Mode == SibucaoMode.Cinematic) S.SetMode(SibucaoMode.Playing);
            playing = false; recording = pending = null; recordedAttempt = null;
            frames.Clear(); frameTimes.Clear();
            Release(tail); Release(highlight); tail = highlight = null;
            times = highlightTimes = null;
            next = count = highlightCount = 0; lastFrame = -1; captureAt = 0; highlightAt = -1;
        }

        static void Release(RenderTexture[] textures)
        {
            if (textures == null) return;
            foreach (var frame in textures) if (frame) { frame.Release(); Destroy(frame); }
        }

        void OnDisable() => ResetPlayback();
        void OnDestroy() => ResetPlayback();

        void OnGUI()
        {
            if (!playing || !S || S.Mode != SibucaoMode.Cinematic || !CurrentFrame) return;
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; int oldDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -1000; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float bar = Mathf.Max(36, Screen.height * .065f);
            GUI.DrawTexture(new Rect(0, bar, Screen.width, Screen.height - 2 * bar), CurrentFrame, ScaleMode.ScaleToFit, false);
            if (caption == null)
            {
                var ui = S.GetComponent<SibucaoUI>();
                caption = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = ui ? ui.roboto : null };
                caption.normal.textColor = new Color(.906f, .941f, .949f);
                skipStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, font = ui ? ui.roboto : null };
            }
            caption.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 14, 24));
            skipStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .018f, 13, 20));
            GUI.Label(new Rect(10, 0, Screen.width - 20, bar), "PUROK SIBUCAO  /  THE FIRST CROSSING", caption);
            if (GUI.Button(new Rect(Screen.width - 192, Screen.height - bar + 6, 180, bar - 12), "Continue  ·  Space", skipStyle)) Skip();
            float opacity = Mathf.Max(1 - Elapsed / Fade, 1 - (duration - Elapsed) / Fade);
            if (playing && opacity > 0)
            {
                GUI.color = new Color(0, 0, 0, Mathf.Clamp01(opacity));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth;
        }
    }
}
