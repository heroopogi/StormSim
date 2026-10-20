using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoAftermathBuildingUpgrade
    {
        const string ScenePath = "Assets/Scenes/SIBUCAO_AFTERMATH.unity";
        const string AssetFolder = "Assets/StormSim/Sibucao/Art/Aftermath/VRBuildingUpgrades";
        const string RootName = "Aftermath - VR ready building states";
        const float RequiredClearance = 1.5f;
        static readonly Regex BuildingName = new Regex(@"^House \d{4}(?: - furnished interior)?$", RegexOptions.Compiled);
        static Material mud, timber, metal, warning, inspected, tarp;
        static Mesh cube;
        static SibucaoRoute route;
        static int assetIndex;

        struct Part
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public Part(Vector3 position, Vector3 scale, Quaternion rotation)
            { this.position = position; this.scale = scale; this.rotation = rotation; }
        }

        sealed class Candidate
        {
            public Transform transform;
            public Bounds bounds;
            public float progress, distance;
            public Vector3 routePoint;
        }

        [MenuItem("StormSim/Aftermath/Upgrade buildings for PC and VR")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (EditorSceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open SIBUCAO_AFTERMATH first.");
            if (GameObject.Find(RootName)) throw new InvalidOperationException("VR-ready aftermath building upgrade is already installed.");

            Directory.CreateDirectory(AssetFolder);
            var session = Object.FindFirstObjectByType<SibucaoSession>();
            if (!session || !session.route) throw new InvalidOperationException("Sibucao route is missing.");
            route = session.route;
            PrepareAssets();

            var candidates = FindCandidates();
            if (candidates.Count < 6) throw new InvalidOperationException("Not enough route-facing buildings were found.");
            var root = new GameObject(RootName).transform;
            int installed = 0, colliders = 0, audio = 0;
            var report = new List<string>();
            foreach (var candidate in candidates.Take(8))
            {
                var level = installed % 4 == 3 ? AftermathDamageLevel.Unsafe : installed % 2 == 0 ? AftermathDamageLevel.Minor : AftermathDamageLevel.Moderate;
                var result = Dress(root, candidate, level, installed);
                colliders += result.collider ? 1 : 0;
                if (result.audio) audio++;
                report.Add(candidate.transform.name + " | " + level + " | route clearance " + result.clearance.ToString("0.00") + " m | LOD yes | simplified collider " + (result.collider ? "yes" : "not required"));
                installed++;
            }

            StaticBatchingUtility.Combine(root.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) throw new IOException("Could not save aftermath scene.");
            Directory.CreateDirectory("Captures/Aftermath");
            report.Insert(0, "PASS: " + installed + " building states, " + colliders + " visible simplified debris colliders, " + audio + " spatial audio emitters.");
            report.Add("Minimum requested route clearance: " + RequiredClearance.ToString("0.0") + " m. Decorative details have no colliders.");
            File.WriteAllLines("Captures/Aftermath/vr-building-upgrade.txt", report);
            Debug.Log(string.Join("\n", report));
        }

        static List<Candidate> FindCandidates()
        {
            var list = new List<Candidate>();
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!BuildingName.IsMatch(transform.name) || transform.IsChildOf(GameObject.Find("Aftermath - school relief and storm debris")?.transform)) continue;
                var renderers = transform.GetComponentsInChildren<Renderer>(false).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < 2 || bounds.size.x > 24 || bounds.size.z > 24) continue;
                float progress = route.Progress(bounds.center);
                var point = route.Sample(progress);
                float distance = Flat(bounds.center, point);
                if (distance < 2.5f || distance > 22) continue;
                list.Add(new Candidate { transform = transform, bounds = bounds, progress = progress, distance = distance, routePoint = point });
            }
            // Use physical proximity rather than route progress: this route loops and progress alone can
            // exclude nearby houses. Eight metres keeps the visual states distributed without gaps.
            var selected = new List<Candidate>();
            foreach (var candidate in list.OrderBy(c => c.distance).ThenByDescending(c => c.progress))
                if (selected.All(existing => Flat(existing.bounds.center, candidate.bounds.center) > 8)) selected.Add(candidate);
            return selected;
        }

        struct DressResult { public float clearance; public bool collider, audio; }
        static DressResult Dress(Transform parent, Candidate c, AftermathDamageLevel level, int index)
        {
            var root = new GameObject("Building state " + index + " - " + c.transform.name).transform;
            root.SetParent(parent, false);
            var state = root.gameObject.AddComponent<SibucaoAftermathBuildingState>();
            state.damageLevel = level;
            state.visibleCondition = level == AftermathDamageLevel.Unsafe ? "Unsafe structure; keep clear" : level == AftermathDamageLevel.Moderate ? "Assess before entry" : "Inspected; minor exterior damage";

            Vector3 away = c.bounds.center - c.routePoint; away.y = 0;
            if (away.sqrMagnitude < .01f) away = Vector3.right;
            away.Normalize();
            Vector3 tangent = Vector3.Cross(Vector3.up, away).normalized;
            float facadeExtent = Mathf.Abs(away.x) * c.bounds.extents.x + Mathf.Abs(away.z) * c.bounds.extents.z;
            float width = Mathf.Clamp(Mathf.Abs(tangent.x) * c.bounds.size.x + Mathf.Abs(tangent.z) * c.bounds.size.z, 2.6f, 7);
            Vector3 facade = c.bounds.center - away * (facadeExtent + .045f);
            facade.y = c.bounds.min.y;
            Quaternion facing = Quaternion.LookRotation(-away, Vector3.up);

            var high = new List<Renderer>();
            high.Add(CreateMesh(root, "Flood and silt evidence", new[]
            {
                new Part(facade + Vector3.up * .32f, new Vector3(width, .42f, .035f), facing),
                new Part(facade + tangent * (width * .28f) + Vector3.up * .68f, new Vector3(width * .22f, .12f, .04f), facing)
            }, mud, "mud"));

            if (level != AftermathDamageLevel.Minor)
            {
                high.Add(CreateMesh(root, "Secured and damaged opening", new[]
                {
                    new Part(facade - tangent * (width * .2f) + Vector3.up * 1.45f, new Vector3(1.25f, .13f, .07f), facing * Quaternion.Euler(0, 0, 18)),
                    new Part(facade - tangent * (width * .2f) + Vector3.up * 1.45f, new Vector3(1.25f, .13f, .07f), facing * Quaternion.Euler(0, 0, -18))
                }, timber, "boards"));
            }

            bool hasCollider = false;
            Vector3 debris = facade + tangent * Mathf.Clamp(width * .33f, 1.2f, 2.2f) - away * .5f;
            float clearance = DistanceToRoute(debris);
            if (level != AftermathDamageLevel.Minor && clearance >= RequiredClearance)
            {
                var debrisParts = new[]
                {
                    new Part(debris + Vector3.up * .12f, new Vector3(1.6f,.12f,.18f), Quaternion.Euler(5,index*31,7)),
                    new Part(debris + tangent*.35f + Vector3.up*.16f, new Vector3(1.2f,.1f,.16f), Quaternion.Euler(-3,index*31+48,-5)),
                    new Part(debris - tangent*.35f + Vector3.up*.13f, new Vector3(1.35f,.11f,.15f), Quaternion.Euler(4,index*31-35,4))
                };
                high.Add(CreateMesh(root, "Visible timber debris cluster", debrisParts, timber, "debris"));
                high.Add(CreateMesh(root, "Visible roof sheet in debris", new[] { new Part(debris + away*.18f + Vector3.up*.24f, new Vector3(1.45f,.045f,.85f), Quaternion.Euler(8,index*27,-7)) }, metal, "sheet"));
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.center = debris + Vector3.up * .18f;
                collider.size = new Vector3(2.15f, .36f, 1.35f);
                hasCollider = true;
            }

            if (level == AftermathDamageLevel.Unsafe)
                high.Add(CreateMesh(root, "Temporary roof tarpaulin", new[] { new Part(c.bounds.center + Vector3.up * (c.bounds.extents.y + .12f), new Vector3(Mathf.Min(4,c.bounds.size.x*.55f),.045f,Mathf.Min(3,c.bounds.size.z*.45f)), Quaternion.Euler(3,index*19,5)) }, tarp, "tarp"));

            Color signColor = level == AftermathDamageLevel.Unsafe ? new Color(.98f,.58f,.18f) : level == AftermathDamageLevel.Moderate ? new Color(.98f,.78f,.35f) : new Color(.42f,.78f,.7f);
            var boardMaterial = level == AftermathDamageLevel.Minor ? inspected : warning;
            Vector3 signPosition = facade - tangent * Mathf.Clamp(width * .32f, 1, 2) + Vector3.up * 1.05f;
            high.Add(CreateMesh(root, "Readable safety board", new[] { new Part(signPosition, new Vector3(1.55f,.54f,.055f), facing) }, boardMaterial, "sign"));
            var label = new GameObject("Readable safety status"); label.transform.SetParent(root, false);
            label.transform.SetPositionAndRotation(signPosition - away * .035f, facing);
            var text = label.AddComponent<TextMesh>();
            text.text = level == AftermathDamageLevel.Unsafe ? "UNSAFE\nKEEP CLEAR" : level == AftermathDamageLevel.Moderate ? "ASSESS\nBEFORE ENTRY" : "INSPECTED\nMINOR DAMAGE";
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.fontSize = 52; text.characterSize = .032f; text.color = new Color(.08f,.12f,.13f);
            high.Add(text.GetComponent<MeshRenderer>());

            var low = CreateMesh(root, "Distant building condition marker", new[]
            {
                new Part(facade + Vector3.up * .31f, new Vector3(width,.4f,.03f), facing),
                new Part(signPosition, new Vector3(1.55f,.54f,.04f), facing)
            }, boardMaterial, "low");
            var lod = root.gameObject.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.CrossFade; lod.animateCrossFading = true;
            lod.SetLODs(new[] { new LOD(.08f, high.ToArray()), new LOD(.018f, new[] { low }), new LOD(.006f, Array.Empty<Renderer>()) });
            lod.RecalculateBounds();

            state.routeClearance = hasCollider ? clearance : float.PositiveInfinity;
            bool spatialAudio = level != AftermathDamageLevel.Minor && index < 5;
            if (spatialAudio) AddSpatialAudio(root, debris, level);
            return new DressResult { clearance = hasCollider ? clearance : DistanceToRoute(facade), collider = hasCollider, audio = spatialAudio };
        }

        static MeshRenderer CreateMesh(Transform parent, string name, IEnumerable<Part> parts, Material material, string suffix)
        {
            var combine = parts.Select(p => new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(p.position, p.rotation, p.scale) }).ToArray();
            var mesh = new Mesh { name = name + " mesh", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine, true, true, false); mesh.RecalculateBounds(); mesh.RecalculateNormals();
            string path = AssetFolder + "/Building upgrade " + assetIndex++ + " " + suffix + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            go.isStatic = true;
            return renderer;
        }

        static void AddSpatialAudio(Transform parent, Vector3 position, AftermathDamageLevel level)
        {
            var go = new GameObject(level == AftermathDamageLevel.Unsafe ? "Spatial unstable metal" : "Spatial cleanup activity");
            go.transform.SetParent(parent, false); go.transform.position = position + Vector3.up * 1.2f;
            var source = go.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetFolder + (level == AftermathDamageLevel.Unsafe ? "/unstable-metal.wav" : "/cleanup-impact.wav"));
            source.playOnAwake = false; source.loop = false; source.volume = .13f; source.priority = 180;
            source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2; source.maxDistance = 18; source.dopplerLevel = 0;
            go.AddComponent<SibucaoAftermathSpatialAudio>().interval = level == AftermathDamageLevel.Unsafe ? new Vector2(11, 19) : new Vector2(7, 14);
        }

        static void PrepareAssets()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = temp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(temp);
            mud = Material("VR building mud line", new Color(.22f,.17f,.105f));
            timber = Material("VR waterlogged timber", new Color(.27f,.16f,.08f));
            metal = Material("VR displaced metal", new Color(.32f,.39f,.4f), .28f);
            warning = Material("VR unsafe amber", new Color(.96f,.55f,.14f));
            inspected = Material("VR inspected teal", new Color(.38f,.74f,.69f));
            tarp = Material("VR recovery tarp", new Color(.05f,.24f,.42f), .2f);
            WriteWav(AssetFolder + "/cleanup-impact.wav", false);
            WriteWav(AssetFolder + "/unstable-metal.wav", true);
            AssetDatabase.Refresh();
            assetIndex = Directory.GetFiles(AssetFolder, "Building upgrade *.asset").Length;
        }

        static Material Material(string name, Color color, float smoothness = 0)
        {
            string path = AssetFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.enableInstancing = true;
            return material;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
        static float DistanceToRoute(Vector3 point)
        {
            float best = float.MaxValue;
            for (int i = 1; i < route.points.Length; i++)
            {
                Vector3 a = route.points[i - 1], b = route.points[i]; a.y = b.y = point.y;
                Vector3 delta = b - a; float t = Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(point, a + delta * t));
            }
            return best;
        }

        static void WriteWav(string assetPath, bool creak)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath)) return;
            const int rate = 22050; float seconds = creak ? 1.15f : .42f; int samples = Mathf.CeilToInt(rate * seconds);
            var bytes = new byte[44 + samples * 2];
            void I(int at, int value) { bytes[at]=(byte)value; bytes[at+1]=(byte)(value>>8); bytes[at+2]=(byte)(value>>16); bytes[at+3]=(byte)(value>>24); }
            void H(int at, int value) { bytes[at]=(byte)value; bytes[at+1]=(byte)(value>>8); }
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("RIFF"),0,bytes,0,4); I(4,bytes.Length-8); Array.Copy(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "),0,bytes,8,8);
            I(16,16); H(20,1); H(22,1); I(24,rate); I(28,rate*2); H(32,2); H(34,16); Array.Copy(System.Text.Encoding.ASCII.GetBytes("data"),0,bytes,36,4); I(40,samples*2);
            var random = new System.Random(creak ? 773 : 441);
            for (int n=0;n<samples;n++)
            {
                float t=n/(float)rate, envelope=creak?Mathf.Sin(Mathf.Clamp01(t/seconds)*Mathf.PI):Mathf.Exp(-t*12);
                float wave=creak?Mathf.Sin(2*Mathf.PI*(310-120*t)*t)+.32f*Mathf.Sin(2*Mathf.PI*690*t):(float)(random.NextDouble()*2-1)*.75f+Mathf.Sin(2*Mathf.PI*115*t);
                short value=(short)Mathf.Clamp(wave*envelope*6500,-32760,32760);H(44+n*2,value);
            }
            File.WriteAllBytes(fullPath,bytes);
        }

        public static void Batch()
        {
            try { EditorSceneManager.OpenScene(ScenePath); Install(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
