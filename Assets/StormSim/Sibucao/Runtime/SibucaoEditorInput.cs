#if UNITY_EDITOR
using UnityEngine;
namespace StormSim.Sibucao
{
    // Play-mode automation only. The build always reads real keyboard / mouse input.
    public static class SibucaoEditorInput
    {
        public static bool Active,Interact,UseHeld,GripDown,GripHeld,Sprint,Jump,Ascend,Dive,Cancel,SkipCinematic;
        public static Vector2 Move,Look,ToolTurn;
        public static float Scroll;
        public static bool Take(ref bool edge){bool value=edge;edge=false;return value;}
        public static void Reset(){Active=Interact=UseHeld=GripDown=GripHeld=Sprint=Jump=Ascend=Dive=Cancel=SkipCinematic=false;Move=Look=ToolTurn=Vector2.zero;Scroll=0;}
    }
}
#endif
