using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace StormSim.Sibucao
{
    // Manual and automatic slots have independent, last-good backups.
    public static class SibucaoSaveStore
    {
        public static RunState Read(string path, Action<RunState> validate)
        {
            string json = File.ReadAllText(path);
            foreach (var field in new[] { "\"version\"", "\"position\"", "\"physical\"", "\"scores\"", "\"results\"" })
                if (!json.Contains(field)) throw new InvalidDataException("The save is missing required data.");
            var value = JsonUtility.FromJson<RunState>(json);
            Validate(value); validate?.Invoke(value); return value;
        }
        public static void Validate(RunState value)
        {
            if (value == null || value.version != 1 || value.resolved == null || value.resolved.Length != 9 ||
                value.chosen == null || value.chosen.Length != 9 || value.outcomes == null || value.outcomes.Length != 9 ||
                value.scores == null || value.scores.Length != 5 || value.physical == null ||
                value.physical.results == null || value.physical.results.Length < 9 || value.physical.flags == null || value.physical.bodies == null ||
                !float.IsFinite(value.forward.x) || !float.IsFinite(value.forward.y) || !float.IsFinite(value.forward.z) ||
                !float.IsFinite(value.position.x) || !float.IsFinite(value.position.y) || !float.IsFinite(value.position.z) ||
                !float.IsFinite(value.physical.health) || value.physical.health<0 || value.physical.health>100 ||
                !float.IsFinite(value.physical.stamina) || value.physical.stamina<0 || value.physical.stamina>100 ||
                !float.IsFinite(value.physical.hunger) || value.physical.hunger<0 || value.physical.hunger>100 ||
                !float.IsFinite(value.physical.submergedSeconds) || value.physical.submergedSeconds<0 ||
                !float.IsFinite(value.homeSeconds) || value.homeSeconds < 0)
                throw new InvalidDataException("This save is incomplete or incompatible.");
        }
        public static void Write(string path, RunState state, Action<RunState> validate)
        {
            Validate(state); validate?.Invoke(state);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                Read(temp, validate);
                if (File.Exists(path))
                {
                    bool previousValid = false;
                    try { Read(path, validate); previousValid = true; } catch (Exception) { }
                    // A corrupt primary must not replace an existing good backup.
                    File.Replace(temp, path, previousValid ? path + ".bak" : null);
                }
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
