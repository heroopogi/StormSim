using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
 public static class SibucaoCockpitBuilder
 {
  const string Dir="Assets/StormSim/Sibucao/Art/Cockpits";
  static Material rubber,plastic,metal,cloth,paint,light,glass;
  static int meshIndex;
  static Material Mat(string name,Color color,float smooth=.3f,float metallic=0)
  {
   string path=Dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
   m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
  }
  static Transform Node(string n,Transform p,Vector3 pos){var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=pos;return g.transform;}
  static Transform Box(string n,Transform p,Vector3 pos,Vector3 scale,Material m)
  {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;return g.transform;}
  static Transform Rod(string n,Transform p,Vector3 a,Vector3 b,float radius,Material mat)
  {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(p,false);g.transform.localPosition=(a+b)*.5f;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);g.transform.localScale=new Vector3(radius*2,(a-b).magnitude*.5f,radius*2);g.GetComponent<Renderer>().sharedMaterial=mat;return g.transform;}
  static Transform Ring(string n,Transform parent,Vector3 pos,float radius,float tube,Material mat)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();const int segments=48,sides=10;
   for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++){float a=i*2*Mathf.PI/segments,b=j*2*Mathf.PI/sides;vertices.Add(new Vector3(Mathf.Cos(a)*(radius+tube*Mathf.Cos(b)),Mathf.Sin(a)*(radius+tube*Mathf.Cos(b)),tube*Mathf.Sin(b)));if(i<segments&&j<sides){int k=i*(sides+1)+j;triangles.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}}
   var mesh=new Mesh{name=n};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   var path=Dir+"/Ring "+meshIndex+++".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
   var t=Node(n,parent,pos);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;
  }
  static void Label(string text,Transform p,Vector3 pos,float size=.003f)
  {var t=Node(text,p,pos);var tm=t.gameObject.AddComponent<TextMesh>();tm.text=text;tm.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StormSim/Sibucao/Art/Roboto-Regular.ttf");tm.GetComponent<Renderer>().sharedMaterial=tm.font.material;tm.fontSize=48;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.78f,.87f,.81f);}
  static Transform Gauge(string name,Transform p,Vector3 pos,bool rpm)
  {
   Ring(name+" bezel",p,pos,.103f,.009f,metal);Box(name+" face",p,pos+Vector3.forward*.01f,new Vector3(.198f,.198f,.013f),rubber);
   for(int i=0;i<=8;i++){float a=(125-i*31.25f)*Mathf.Deg2Rad;var marker=Box("Gauge tick",p,pos+new Vector3(-Mathf.Sin(a)*.081f,Mathf.Cos(a)*.081f,-.015f),new Vector3(.004f,.014f,.004f),light);marker.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);}
   Label(rpm?"RPM":"km/h",p,pos+new Vector3(0,-.052f,-.018f),.003f);
   var needle=Node(name+" needle",p,pos+Vector3.back*.026f);Box("Needle",needle,new Vector3(0,.034f,0),new Vector3(.006f,.08f,.004f),light);return needle;
  }
  static GameObject Build(bool tri)
  {
   var root=new GameObject(tri?"Philippine tricycle cockpit":"Everyday sedan cockpit");var r=root.transform;var c=root.AddComponent<SibucaoCockpit>();c.tricycle=tri;
   if(!tri)
   {
    Box("Textured dashboard",r,new Vector3(.32f,-.49f,.85f),new Vector3(1.73f,.23f,.53f),plastic);
    Box("Lower dash fascia",r,new Vector3(.32f,-.65f,.64f),new Vector3(1.68f,.18f,.12f),rubber);
    Box("Instrument binnacle",r,new Vector3(0,-.29f,.74f),new Vector3(.52f,.27f,.18f),plastic);
    for(int side=0;side<2;side++){float x=side==0?-.65f:1.25f;Box("Door card",r,new Vector3(x,-.55f,-.22f),new Vector3(.085f,.54f,1.65f),cloth);Box("Door armrest",r,new Vector3(x+(side==0?.075f:-.075f),-.41f,-.03f),new Vector3(.13f,.085f,.45f),rubber);Box("Interior door handle",r,new Vector3(x+(side==0?.1f:-.1f),-.3f,.3f),new Vector3(.025f,.045f,.13f),metal);Rod("A pillar",r,new Vector3(x,-.38f,1.04f),new Vector3(x*.84f,.43f,.66f),.033f,cloth);Rod("B pillar",r,new Vector3(x,-.8f,-.68f),new Vector3(x*.85f,.45f,-.68f),.035f,cloth);}
    Box("Headliner",r,new Vector3(.25f,.5f,-.32f),new Vector3(1.84f,.065f,2.15f),cloth);
    Box("Cabin floor",r,new Vector3(.3f,-1.12f,-.28f),new Vector3(1.85f,.08f,2.7f),rubber);
    for(int j=0;j<2;j++){float x=j*.79f;Box("Seat cushion",r,new Vector3(x,-.86f,-.35f),new Vector3(.59f,.17f,.66f),cloth);Box("Seat back",r,new Vector3(x,-.49f,-.69f),new Vector3(.58f,.77f,.17f),cloth);Box("Head restraint",r,new Vector3(x,-.03f,-.7f),new Vector3(.34f,.23f,.13f),rubber);for(int k=-2;k<=2;k++)Box("Seat seam",r,new Vector3(x+k*.07f,-.763f,-.35f),new Vector3(.006f,.006f,.57f),rubber);}
    Box("Rear bench",r,new Vector3(.3f,-.79f,-1.35f),new Vector3(1.6f,.22f,.55f),cloth);Box("Rear backrest",r,new Vector3(.3f,-.42f,-1.61f),new Vector3(1.6f,.62f,.14f),cloth);
    Box("Center console",r,new Vector3(.43f,-.82f,.05f),new Vector3(.24f,.2f,.96f),plastic);Rod("Gear lever",r,new Vector3(.43f,-.74f,.25f),new Vector3(.43f,-.5f,.21f),.019f,metal);Box("Gear knob",r,new Vector3(.43f,-.49f,.21f),new Vector3(.063f,.06f,.07f),rubber);
    for(int v=0;v<3;v++){float x=.42f+v*.21f;Box("Vent recess",r,new Vector3(x,-.4f,.574f),new Vector3(.17f,.09f,.015f),rubber);for(int slat=0;slat<4;slat++)Box("Vent slat",r,new Vector3(x,-.43f+slat*.021f,.56f),new Vector3(.155f,.005f,.012f),metal);}
    Box("Radio",r,new Vector3(.63f,-.59f,.568f),new Vector3(.35f,.1f,.02f),rubber);Label("FM  98.5",r,new Vector3(.63f,-.58f,.55f));
    Box("Visible bonnet",r,new Vector3(.3f,-.48f,1.7f),new Vector3(1.85f,.12f,1.05f),paint);
    c.steering=Node("Steering wheel",r,new Vector3(0,-.27f,.5f));Ring("Leather wheel rim",c.steering,Vector3.zero,.17f,.021f,rubber);Box("Wheel hub",c.steering,Vector3.zero,new Vector3(.13f,.09f,.075f),plastic);
    Rod("Left spoke",c.steering,new Vector3(-.06f,0,0),new Vector3(-.16f,.03f,0),.018f,metal);Rod("Right spoke",c.steering,new Vector3(.06f,0,0),new Vector3(.16f,.03f,0),.018f,metal);Rod("Lower spoke",c.steering,new Vector3(0,-.02f,0),new Vector3(0,-.16f,0),.02f,plastic);
    c.leftGrip=Node("Left grip",c.steering,new Vector3(-.16f,.025f,-.02f));c.rightGrip=Node("Right grip",c.steering,new Vector3(.16f,.025f,-.02f));
    c.speedNeedle=Gauge("Speedometer",r,new Vector3(-.115f,-.28f,.636f),false);c.rpmNeedle=Gauge("Tachometer",r,new Vector3(.115f,-.28f,.636f),true);
    var windshield=Box("Rain on windshield",r,new Vector3(.28f,.02f,1.05f),new Vector3(1.75f,.85f,.004f),glass);c.rainGlass=glass;
    c.wipers=new Transform[2];for(int i=0;i<2;i++){c.wipers[i]=Node("Wiper pivot",r,new Vector3(-.35f+i*.8f,-.37f,1.03f));Rod("Wiper arm",c.wipers[i],Vector3.zero,new Vector3(0,.43f,0),.009f,rubber);Rod("Wiper blade",c.wipers[i],new Vector3(-.04f,.2f,-.01f),new Vector3(-.04f,.58f,-.01f),.012f,rubber);}
   }
   else
   {
    Rod("Motorcycle steering column",r,new Vector3(0,-.96f,.64f),new Vector3(0,-.39f,.53f),.04f,metal);
    c.steering=Node("Handlebar assembly",r,new Vector3(0,-.39f,.52f));Rod("Chrome handlebar",c.steering,new Vector3(-.43f,0,0),new Vector3(.43f,0,0),.019f,metal);
    for(int side=-1;side<=1;side+=2){Rod("Rubber grip",c.steering,new Vector3(side*.29f,0,0),new Vector3(side*.44f,0,0),.03f,rubber);Rod("Brake lever",c.steering,new Vector3(side*.22f,0,.035f),new Vector3(side*.43f,-.02f,.08f),.008f,metal);Rod("Mirror stalk",c.steering,new Vector3(side*.26f,.02f,.02f),new Vector3(side*.39f,.29f,.06f),.009f,metal);Box("Handlebar switch",c.steering,new Vector3(side*.26f,0,0),new Vector3(.055f,.07f,.075f),plastic);}
    c.leftGrip=Node("Left grip",c.steering,new Vector3(-.36f,.015f,-.02f));c.rightGrip=Node("Right grip",c.steering,new Vector3(.36f,.015f,-.02f));
    c.speedNeedle=Gauge("Motorcycle speed",r,new Vector3(0,-.31f,.69f),false);
    Box("Fuel tank",r,new Vector3(0,-.72f,.22f),new Vector3(.31f,.23f,.56f),paint);Ring("Fuel cap",r,new Vector3(0,-.593f,.27f),.037f,.006f,metal).localRotation=Quaternion.Euler(90,0,0);
    Box("Motorcycle seat",r,new Vector3(0,-.8f,-.4f),new Vector3(.31f,.14f,.75f),rubber);
    for(int side=0;side<2;side++){float x=side==0?.48f:1.37f;Rod("Sidecar canopy support",r,new Vector3(x,-.9f,.95f),new Vector3(x,.42f,.78f),.019f,metal);Rod("Rear canopy support",r,new Vector3(x,-.9f,-.85f),new Vector3(x,.42f,-.85f),.019f,metal);}
    Box("Sidecar passenger cushion",r,new Vector3(.93f,-.8f,-.36f),new Vector3(.7f,.15f,.61f),cloth);Box("Sidecar backrest",r,new Vector3(.93f,-.48f,-.69f),new Vector3(.7f,.55f,.13f),cloth);Box("Sidecar floor",r,new Vector3(.93f,-1.12f,.05f),new Vector3(.88f,.08f,1.65f),metal);Box("Sidecar front panel",r,new Vector3(.93f,-.65f,.9f),new Vector3(.89f,.58f,.06f),paint);Box("Sidecar roof",r,new Vector3(.93f,.46f,-.02f),new Vector3(1.04f,.05f,1.89f),paint);
   }
   var mirrorPos=tri?new Vector3(-.4f,-.04f,.58f):new Vector3(.26f,.3f,.8f);
   Box("Rear-view mirror housing",r,mirrorPos,new Vector3(tri?.19f:.28f,.12f,.04f),rubber);c.mirrorSurface=Box("Rear-view mirror glass",r,mirrorPos+Vector3.back*.025f,new Vector3(tri?.17f:.26f,.1f,.006f),metal).GetComponent<Renderer>();
   var fill=Node("Cabin instrument fill",r,new Vector3(0,.1f,.2f)).gameObject.AddComponent<Light>();fill.type=LightType.Point;fill.range=2;fill.intensity=.65f;fill.shadows=LightShadows.None;
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/StormSim/Sibucao/Prefabs/"+(tri?"Tricycle Cockpit":"Car Cockpit")+".prefab");Object.DestroyImmediate(root);return prefab;
  }
  public static string Install()
  {
   if(Application.isPlaying)throw new System.Exception("Stop Play mode before installing cockpits.");
   if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets/StormSim/Sibucao/Art","Cockpits");meshIndex=0;
   rubber=Mat("Charcoal rubber",new Color(.025f,.027f,.029f),.2f);plastic=Mat("Dashboard graphite",new Color(.07f,.075f,.08f),.24f);cloth=Mat("Worn grey upholstery",new Color(.21f,.22f,.2f),.05f);metal=Mat("Brushed metal",new Color(.42f,.45f,.46f),.55f,.8f);paint=Mat("Weathered blue painted steel",new Color(.12f,.24f,.28f),.4f,.25f);light=Mat("Gauge ivory",new Color(.85f,.94f,.75f));light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(.24f,.33f,.17f));
   glass=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Rain glass.mat");if(!glass){glass=new Material(Shader.Find("StormSim/CockpitRainGlass"));AssetDatabase.CreateAsset(glass,Dir+"/Rain glass.mat");}
   var car=Build(false);var tri=Build(true);int count=0;
   foreach(var v in Object.FindObjectsByType<SibucaoVehicle>(FindObjectsSortMode.None)){v.cockpitPrefab=v.displayName=="Tricycle"?tri:car;v.seat=new Vector3(v.displayName=="Tricycle"?-.35f:-.38f,-.28f,-.05f);EditorUtility.SetDirty(v);count++;}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Installed original car/tricycle cockpits on "+count+" vehicles.";
  }
 }
}



