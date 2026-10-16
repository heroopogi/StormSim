using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    public sealed class PreparednessActivity : MonoBehaviour
    {
        public string id,title,aftermathKey;
        public string[] requiredIds;
        public enum Check { Tasks, Documents, Windows, RaisedItems, IndoorObjects, Waste }
        public Check check;
        public bool Complete(PreparednessSession s)
        {
            switch(check)
            {
                case Check.Documents:return s.items.Any(i=>i.kind==PrepKind.Documents&&i.closed&&(i.IsPacked||i.storedIn=="high-shelf"));
                case Check.Windows:var windows=s.items.Where(i=>i.kind==PrepKind.Window).ToArray();return windows.Length>0&&windows.All(i=>i.closed&&i.prepared);
                case Check.RaisedItems:return s.items.Any(i=>i.kind==PrepKind.Electronics&&!i.plugged&&i.storedIn=="high-shelf")&&s.items.Any(i=>i.kind==PrepKind.Valuables&&i.storedIn=="high-shelf");
                case Check.IndoorObjects:return s.items.Where(i=>i.kind==PrepKind.Outdoor).Any()&&s.items.Where(i=>i.kind==PrepKind.Outdoor).All(i=>i.storedIn=="indoors");
                case Check.Waste:return s.items.Any(i=>i.id=="indoor-waste-bin"&&i.cells>0&&i.closed);
                default:return requiredIds!=null&&requiredIds.Length>0&&requiredIds.All(id=>s.items.Any(i=>i.id==id&&i.prepared));
            }
        }
        public string Evidence(PreparednessSession s)=>Complete(s)?"Verified from object condition and placement":"Optional; not yet verified";
        public static string Journal(PreparednessSession s)=>"\n\nOPTIONAL HOME ACTIVITIES — 2 points each; supplies and safe departure take priority\n"+string.Join("\n",s.GetComponentsInChildren<PreparednessActivity>(true).Select(a=>(a.Complete(s)?"[done] ":"[  ] ")+a.title));
    }
}
