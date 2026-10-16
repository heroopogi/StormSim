using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoRuntimeUpgrade
    {
        public const string RunPath="Assets/StormSim/Sibucao/Art/OwnedAnimations/Invector/Basic_FreeMovement.fbx";
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode before installation");
            var clip=AssetDatabase.LoadAllAssetsAtPath(RunPath).OfType<AnimationClip>().First(c=>c.name=="Run");
            var speeds=new Dictionary<string,float>();var report=new List<string>();
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/npc_casual_set_00/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("npc_csl_00_character_")))
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));go.hideFlags=HideFlags.HideAndDontSave;go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=go.GetComponentInChildren<Animator>();
                if(!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Invalid Humanoid "+path);
                animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var graph=PlayableGraph.Create("Owned Run audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);AnimationPlayableOutput.Create(graph,"Run",animator).SetSourcePlayable(playable);graph.Play();
                var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var knee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var hip=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                float minimum=100,baseLength=0,drift=0;var positions=new List<Vector3>();
                for(int i=0;i<=100;i++){playable.SetTime(clip.length*i/100.0);graph.Evaluate(0);var p=go.transform.InverseTransformPoint(foot.position);positions.Add(p);minimum=Mathf.Min(minimum,p.y);float length=Vector3.Distance(foot.position,knee.position)+Vector3.Distance(knee.position,hip.position);if(i==0)baseLength=length;drift=Mathf.Max(drift,Mathf.Abs(length-baseLength));}
                var samples=new List<float>();for(int i=1;i<positions.Count;i++){float speed=(positions[i-1].z-positions[i].z)/(clip.length/100);if(positions[i].y<minimum+.09f&&speed>.5f&&speed<8)samples.Add(speed);}
                samples.Sort();float stride=samples.Count>0?samples[samples.Count/2]:0;
                if(stride<.5f||drift>.02f)throw new Exception("Run retarget failed "+path+" stride "+stride+" leg drift "+drift);
                speeds[path]=stride;report.Add(path+" valid Humanoid; Run stance speed="+stride.ToString("F3")+" m/s; leg drift="+drift.ToString("F5"));graph.Destroy();Object.DestroyImmediate(go);
            }
            var s=Object.FindFirstObjectByType<SibucaoSession>();var residents=Object.FindObjectsByType<SibucaoResident>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.casting).OrderBy(r=>r.initialDistance).ToList();var sources=residents.ToArray();
            while(residents.Count<32){var source=sources[(residents.Count-sources.Length)%sources.Length];var copy=Object.Instantiate(source,source.transform.parent);copy.name="Evacuating resident "+(residents.Count+1);residents.Add(copy);}
            for(int i=0;i<residents.Count;i++)
            {
                var r=residents[i];r.name="Evacuating resident "+(i+1).ToString("00");r.initialDistance=(i+.5f)*s.route.length/residents.Count;r.routeDistance=r.initialDistance;r.lane=(i%3-1)*1.65f;r.variation=i;r.distributeFromCurrentHome=true;r.behavior=(ResidentBehavior)(i%4);
                r.transform.position=s.route.Sample(r.initialDistance);var actor=r.casting;
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.animator.gameObject);
                float scale=actor.animator.transform.lossyScale.y;
                actor.panic_PLACEHOLDER=clip;actor.panicIsLocomotion=true;actor.panicStrideSpeed=(speeds.ContainsKey(path)?speeds[path]:speeds.Values.Average())*scale;
                actor.missingAnimations="Run: owned Invector locomotion. Alert/scared gesture, player reaction, seated assistance and injured motion are still missing.";
            }
            var audio=s.GetComponent<SibucaoRouteAudio>();
            if(!audio.tierChange){var go=new GameObject("SFX_TierChange_PLACEHOLDER");go.transform.SetParent(s.transform,false);audio.tierChange=go.AddComponent<AudioSource>();audio.tierChange.playOnAwake=false;audio.tierChange.spatialBlend=0;audio.tierChange.volume=.5f;}
            report.Add("32 background evacuees; "+Object.FindObjectsByType<SibucaoActor>(FindObjectsSortMode.None).Length+" total real rig actors.");
            System.IO.File.WriteAllLines("Documentation/SIBUCAO_OWNED_RUN_AUDIT.txt",report);
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(s.gameObject.scene);return string.Join("\n",report);
        }
    }
}
