using System;
using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed class SibucaoRouteSection : MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public SibucaoRouteChoices director;
        public SibucaoSideMission left, right;
        public RouteSectionLayout[] layouts;
        public RouteSectionLayout[] floodLayouts;
        public Transform approachMarker, mergeMarker;
        public Transform leftSign, rightSign;
        public AudioSource centerVoice;
        public string centerLine;
        public SibucaoRouteDeck decks;
        public float FloodLift => 0;
        public Vector3 Approach => approachMarker ? approachMarker.position : station.transform.position - station.transform.forward * 16;
        public Vector3 Merge => mergeMarker ? mergeMarker.position : station.transform.position + station.transform.forward * 14;
        public Vector3 EntryGround => Approach;
        public bool TryExitTarget(Vector3 player, out Vector3 target) { target=Merge; return false; }
        public Vector3 AccessTarget(Vector3 player) => Approach;
        public void ApplyLayout(int home)
        {
            var layout = Array.Find(layouts, l => l.home == home);
            if (layout == null) return;
            approachMarker.position = layout.approach; mergeMarker.position = layout.merge;
            left.transform.SetPositionAndRotation(layout.left, station.transform.rotation);
            right.transform.SetPositionAndRotation(layout.right, station.transform.rotation);
            left.route = layout.leftPath; right.route = layout.rightPath;
            left.Restore(); right.Restore();
            if(decks)decks.Rebuild(layout);
            FitApproachSign(leftSign); FitApproachSign(rightSign);
            ImproveDestinationContrast(left.transform); ImproveDestinationContrast(right.transform);
            PositionSign(leftSign, layout.left, layout.approach, -1);
            PositionSign(rightSign, layout.right, layout.approach, 1);
            PrepareAccess();
        }
        // Difficulty changes water and hazards, never the map's elevation.
        public void PrepareAccess() { }
        public void RestoreFloodLift(float lift) { }
        void PositionSign(Transform sign, Vector3 destination, Vector3 approach, int side)
        {
            if (!sign) return;
            sign.position = approach + station.transform.forward + station.transform.right * side * (FloodLift>0?3.6f:2.4f);
            sign.rotation = station.transform.rotation;
        }
        static void ImproveDestinationContrast(Transform mission)
        {
            foreach(var text in mission.GetComponentsInChildren<TextMesh>(true))
            {
                if(text.name!="Printed destination")continue;
                text.color=new Color(.96f,.95f,.81f);text.fontStyle=FontStyle.Bold;
                var board=text.transform.parent.Find("Direction board");
                var renderer=board?board.GetComponent<Renderer>():null;
                if(renderer){var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);properties.SetColor("_BaseColor",new Color(.055f,.22f,.18f));renderer.SetPropertyBlock(properties);}
            }
        }
        public static void FitApproachSign(Transform sign)
        {
            if(!sign)return;
            var post=sign.Find("Sign post");
            var board=sign.Find("Direction board");
            var printed=sign.Find("Printed destination");
            if(post){post.localPosition=new Vector3(0,.75f,0);post.localScale=new Vector3(.055f,1.5f,.055f);}
            if(board)
            {
                board.localPosition=new Vector3(0,1.55f,0);board.localScale=new Vector3(1.6f,.34f,.07f);
                var renderer=board.GetComponent<Renderer>();
                if(renderer){var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);properties.SetColor("_BaseColor",new Color(.055f,.22f,.18f));renderer.SetPropertyBlock(properties);}
            }
            if(printed)
            {
                printed.localPosition=new Vector3(0,1.55f,-.041f);
                var text=printed.GetComponent<TextMesh>();
                if(text){text.characterSize=.028f;text.color=new Color(.96f,.95f,.81f);text.fontStyle=FontStyle.Bold;}
            }
        }
    }
}
