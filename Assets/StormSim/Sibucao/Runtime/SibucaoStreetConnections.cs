using System;
using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    // Grounded connections are baked against scene colliders. No path search runs in the player.
    public static class SibucaoStreetConnections
    {
        [Serializable] sealed class Connection { public int home,difficulty,after;public Vector3[] points; }
        [Serializable] sealed class Archive { public int schema;public Connection[] connections; }
        static Dictionary<int,Vector3[]> paths;
        static TextAsset loadedAsset;
        static int Key(int home,int difficulty,int after)=>home*1000+difficulty*10+after;
        static void Load()
        {
            var profile=SibucaoSession.Instance?SibucaoSession.Instance.GetComponent<CoastalAreaProfile>():null;
            var asset=profile?profile.streetConnections:Resources.Load<TextAsset>("SibucaoStreetConnections");
            if(paths!=null&&loadedAsset==asset)return;loadedAsset=asset;paths=new Dictionary<int,Vector3[]>();if(!asset)return;
            var archive=JsonUtility.FromJson<Archive>(asset.text);
            if(archive==null||archive.schema!=1||archive.connections==null)return;
            foreach(var item in archive.connections)
            {
                if(item.points==null||item.points.Length<2)continue;
                bool valid=true;foreach(var p in item.points)if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)){valid=false;break;}
                if(valid)paths[Key(item.home,item.difficulty,item.after)]=item.points;
            }
        }
        public static bool TryGet(int home,int difficulty,int after,out Vector3[] points)
        {Load();return paths.TryGetValue(Key(home,difficulty,after),out points);}
        public static bool TryRemaining(SibucaoSession session,int after,out Vector3[] points,out int start)
        {
            start=0;if(!TryGet(session.state.homeIndex,session.state.difficultyLevel,after,out points))return false;
            var player=session.player.transform.position;
            if(SibucaoRouteChoices.Flat(player,points[points.Length-1])<.8f)return false;
            float nearest=float.PositiveInfinity;int segment=-1;
            for(int i=0;i<points.Length-1;i++)
            {
                var delta=points[i+1]-points[i];delta.y=0;var offset=player-points[i];offset.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
                float distance=(offset-delta*t).sqrMagnitude;
                if(distance<nearest){nearest=distance;segment=i;}
            }
            if(segment<0||nearest>9)return false;
            start=segment+1;
            while(start<points.Length-1&&SibucaoRouteChoices.Flat(player,points[start])<.4f)start++;
            return true;
        }
    }
}
