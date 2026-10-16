using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Shared sprite-pack skin for the desktop HUD and world-space menu.</summary>
    public static class SignalStationUI
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        public static Font Font(bool bold = false) => Resources.Load<Font>("SignalStation/RobotoMono-" + (bold ? "Bold" : "Regular"));
        public static Texture2D Texture(string name)
        {
            if (!textures.TryGetValue(name, out var texture)) textures[name] = texture = Resources.Load<Texture2D>("SignalStation/" + name);
            return texture;
        }
        public static void Draw(Rect rect, string name, int border = 0)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            if (!styles.TryGetValue(name, out var style))
            {
                style = new GUIStyle { border = new RectOffset(border, border, border, border) };
                style.normal.background = Texture(name);
                styles[name] = style;
            }
            style.Draw(rect, GUIContent.none, false, false, false, false);
        }
        public static void Bar(Rect rect, float value, bool warning = false)
        {
            Draw(rect, "bar_track");
            Draw(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), warning ? "bar_fill_amber" : "bar_fill_teal");
        }
    }
}
