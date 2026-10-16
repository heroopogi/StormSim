using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Read-only site search: a compact 18 x 16 m lot, clear of existing roads/buildings/trees.</summary>
public static class BanagoParkingSite
{
    public sealed class Site {public Vector3 center;public float yaw,variation,entryGap,width,depth;public string road;public int nearbyHomes;}
    sealed class Poly {public Vector2[] points;public Bounds bounds;public string name;}
    static List<Poly> houses,roads,protectedAreas;
    static List<Vector3> trees;
    static MeshCollider[] ground;
    public static readonly Dictionary<string,int> diagnostics=new Dictionary<string,int>();
    static void Reject(string key){if(!diagnostics.ContainsKey(key))diagnostics[key]=0;diagnostics[key]++;}
    static Vector2 XZ(Vector3 v)=>new Vector2(v.x,v.z);
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool b=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)b=!b;return b;}
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static bool Segments(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
    {float den=Cross(b-a,d-c);if(Mathf.Abs(den)<.000001f)return false;float u=Cross(c-a,d-c)/den,v=Cross(c-a,b-a)/den;return u>=0&&u<=1&&v>=0&&v<=1;}
    static float Distance(Vector2 p,Vector2[] poly)
    {if(Inside(p,poly))return 0;float d=9999;for(int i=0;i<poly.Length;i++){var a=poly[i];var b=poly[(i+1)%poly.Length]-a;d=Mathf.Min(d,Vector2.Distance(p,a+b*Mathf.Clamp01(Vector2.Dot(p-a,b)/Mathf.Max(.00001f,b.sqrMagnitude))));}return d;}
    static Poly Shape(Vector2[] p,string name)
    {var b=new Bounds(new Vector3(p[0].x,0,p[0].y),Vector3.zero);foreach(var v in p)b.Encapsulate(new Vector3(v.x,0,v.y));return new Poly{points=p,bounds=b,name=name};}
    static bool Intersects(Poly a,Poly b)
    {if(!a.bounds.Intersects(b.bounds))return false;if(a.points.Any(p=>Inside(p,b.points))||b.points.Any(p=>Inside(p,a.points)))return true;for(int i=0;i<a.points.Length;i++)for(int j=0;j<b.points.Length;j++)if(Segments(a.points[i],a.points[(i+1)%a.points.Length],b.points[j],b.points[(j+1)%b.points.Length]))return true;return false;}
    static Poly Rect(Vector2 c,Vector2 right,Vector2 forward,float w,float d)
    {return Shape(new[]{c-right*w-forward*d,c+right*w-forward*d,c+right*w+forward*d,c-right*w+forward*d},"lot");}
    static Poly Box(Bounds b,string name)
    {return Shape(new[]{new Vector2(b.min.x,b.min.z),new Vector2(b.max.x,b.min.z),new Vector2(b.max.x,b.max.z),new Vector2(b.min.x,b.max.z)},name);}
    static bool IsRoad(MeshCollider c)=>c.enabled&&c.sharedMesh&&!c.name.StartsWith("Measured terrain")&&(AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/StudyRoads_")||c.name.IndexOf("road",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("street",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("asphalt",StringComparison.OrdinalIgnoreCase)>=0);
    public static bool Height(Vector2 p,out float y)
    {y=-999;foreach(var c in ground){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,60,p.y),Vector3.down),out var hit,140)&&hit.normal.y>.88f)y=Mathf.Max(y,hit.point.y);}return y>-998;}
    static void Protect(Transform t)
    {var rs=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(rs.Length==0)return;var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);b.Expand(.8f);protectedAreas.Add(Box(b,t.name));}
    public static void Prepare()
    {
        var roots=EditorSceneManager.GetActiveScene().GetRootGameObjects();var cs=roots.SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh).ToArray();ground=cs.Where(c=>c.name.StartsWith("Measured terrain")).ToArray();roads=new List<Poly>();
        foreach(var c in cs.Where(IsRoad)){var v=c.sharedMesh.vertices;var ix=c.sharedMesh.triangles;for(int i=0;i<ix.Length;i+=3){var a=XZ(c.transform.TransformPoint(v[ix[i]]));var b=XZ(c.transform.TransformPoint(v[ix[i+1]]));var d=XZ(c.transform.TransformPoint(v[ix[i+2]]));if(Mathf.Abs(Cross(b-a,d-a))<.00001f)continue;roads.Add(Shape(new[]{a,b,d},c.name));}}
        houses=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(d=>Shape(d["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray(),(string)d["name"])).ToList();protectedAreas=new List<Poly>();trees=new List<Vector3>();
        foreach(var r in roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.enabled&&(r.name.IndexOf("pond",StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("basin",StringComparison.OrdinalIgnoreCase)>=0)))protectedAreas.Add(Box(r.bounds,r.name));
        foreach(string name in new[]{"School campus and covered court","Neighborhood market at mapped location","House 1267"}){var go=GameObject.Find(name);if(go)Protect(go.transform);}
        var civic=GameObject.Find("Banago Architecture - roadside homes and public buildings");if(civic)foreach(Transform t in civic.transform)if(t.name!="House upper-floor slabs")Protect(t);
        var life=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName);if(life)foreach(Transform group in life.transform)if(group.childCount>0)foreach(Transform t in group)Protect(t);
        var details=GameObject.Find(BanagoLivedInBuilder.RootName);if(details)foreach(Transform group in details.transform)if(!group.name.Contains("rooftop")&&!group.name.Contains("satellite"))foreach(Transform t in group)Protect(t);
        var older=GameObject.Find("Trees - satellite canopy locations");if(older)foreach(Transform t in older.transform)if(t.gameObject.activeInHierarchy)trees.Add(new Vector3(t.position.x,1.4f,t.position.z));
        var newer=GameObject.Find(BanagoDenseCanopyBuilder.RootName);if(newer)foreach(var p in newer.GetComponent<BanagoDenseCanopyReview>().plants)if(p.target&&p.target.gameObject.activeInHierarchy)trees.Add(new Vector3(p.target.position.x,p.species.Contains("Banana")||p.species.Contains("Bamboo")?2f:1.3f,p.target.position.z));
        if(ground.Length==0||roads.Count<9000)throw new Exception("Measured ground or road data missing");
    }
    static bool Clear(Poly plot,bool checkRoad=true)
    {if(houses.Any(h=>Intersects(plot,h))||protectedAreas.Any(p=>Intersects(plot,p))||(checkRoad&&roads.Any(r=>Intersects(plot,r))))return false;foreach(var t in trees)if(Distance(new Vector2(t.x,t.z),plot.points)<t.y)return false;return true;}
    public static List<Site> Find(float width=18,float depth=16,int minHomes=3)
    {
        Prepare();diagnostics.Clear();var result=new List<Site>();var seen=new HashSet<string>();var reference=new Vector2(-60,210);
        foreach(var road in roads.OrderBy(r=>Vector2.Distance(XZ(r.bounds.center),reference)))
        {
            if(Vector2.Distance(XZ(road.bounds.center),reference)>650)continue;var pp=road.points;int longest=Enumerable.Range(0,3).OrderByDescending(i=>(pp[(i+1)%3]-pp[i]).sqrMagnitude).First();var along=(pp[(longest+1)%3]-pp[longest]).normalized;
            foreach(int side in new[]{-1,1})foreach(float offset in new[]{depth/2+3,depth/2+5,depth/2+7,depth/2+9})
            {
                var f=new Vector2(-along.y,along.x)*side;var right=new Vector2(f.y,-f.x);var center=XZ(road.bounds.center)+f*offset;string key=Mathf.RoundToInt(center.x/4)+"_"+Mathf.RoundToInt(center.y/4)+"_"+Mathf.RoundToInt(Mathf.Atan2(f.x,f.y)*8);if(!seen.Add(key))continue;
                var plot=Rect(center,right,f,width/2+.6f,depth/2+.6f);if(!Clear(plot)){Reject("occupied");continue;}float lo=999,hi=-999;bool groundOK=true;for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){var p=center+right*x*width/4+f*z*depth/4;if(!Height(p,out var y)){groundOK=false;break;}lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);}if(!groundOK||hi-lo>.8f){Reject("ground");continue;}
                // Check a broad driveway corridor up to the existing lane edge, without changing that lane.
                var entry=center-f*depth/2;float gap=0;for(;gap<9;gap+=.25f){var p=entry-f*gap;if(roads.Any(r=>Distance(p,r.points)<.10f))break;}if(gap>=9){Reject("no approach");continue;}var apron=Rect(entry-f*gap*.5f,right,f,3.8f,gap*.5f);if(!Clear(apron,false)){Reject("approach occupied");continue;}
                int neighbors=houses.Count(h=>Vector2.Distance(center,XZ(h.bounds.center))<100);if(neighbors<minHomes){Reject("isolated");continue;}
                Height(center,out float baseY);result.Add(new Site{center=new Vector3(center.x,baseY,center.y),yaw=Mathf.Atan2(f.x,f.y)*Mathf.Rad2Deg,variation=hi-lo,entryGap=gap,road=road.name,nearbyHomes=neighbors,width=width,depth=depth});if(result.Count>=8)return result;
            }
        }
        return result;
    }
    public static object Validate(Site site)
    {Prepare();var f=new Vector2(Mathf.Sin(site.yaw*Mathf.Deg2Rad),Mathf.Cos(site.yaw*Mathf.Deg2Rad));var r=new Vector2(f.y,-f.x);return new{clear=Clear(Rect(XZ(site.center),r,f,site.width/2+.6f,site.depth/2+.6f)),site.road,site.nearbyHomes,site.variation,site.entryGap};}
}
