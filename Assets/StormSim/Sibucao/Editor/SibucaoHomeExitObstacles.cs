using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StormSim.Sibucao;
using Object = UnityEngine.Object;

public static partial class SibucaoDiegeticInstaller
{
    [MenuItem("StormSim/Route choices/Make the northern home exit obstacles unique")]
    public static string MakeHomeExitObstaclesUnique()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        Context();
        var first = s.physicalDecisions.stations.Single(x => x.node && x.node.id == "sibucao_d33");
        var second = s.physicalDecisions.stations.Single(x => x.node && x.node.id == "sibucao_d34");
        timber = Mat("Storm-soaked coconut timber", new Color(.22f,.15f,.095f));
        metal = Mat("Oxidised galvanized roofing", new Color(.28f,.30f,.29f));
        leaf = Mat("Rain-darkened fallen fronds", new Color(.16f,.23f,.085f));
        blue = Mat("Faded blue HDPE", new Color(.07f,.2f,.35f));
        var orange = Mat("Supply cart orange", new Color(.75f,.30f,.07f));
        foreach (var station in new[] { first, second })
        {
            foreach (Transform child in station.transform.Cast<Transform>().ToArray())
                if (new[] { "Snapped lean-to rafter", "Collapsed nipa post", "Bent corrugated GI sheet",
                    "Rain barrel knocked beside roofing", "Unique home-exit obstacle" }.Contains(child.name))
                    Undo.DestroyObjectImmediate(child.gameObject);
            Undo.RecordObject(station, "Distinct home exit obstacle");
        }

        var palm = Node(first.transform, "Unique home-exit obstacle", Vector3.zero);
        Undo.RegisterCreatedObjectUndo(palm.gameObject, "Fallen palm");
        Tree(palm);
        DescribeExit(first, SibucaoHazardKind.FallenTree, "Fallen palm across the street",
            "A storm-uprooted palm blocks the first street after the house. Climb the trunk or use the surrounding lane.");
        var climb = first.branches[0];
        climb.mechanism = PhysicalMechanism.Mantle;
        LabelExitBranch(climb, "Climb over the fallen palm", "You reached the ground beyond the fallen palm.");
        var climbPoints = new[] { new Vector3(-.35f,0,-2),new Vector3(-.35f,.99f,-.85f),
            new Vector3(-.35f,.99f,-.35f),new Vector3(-.35f,.99f,.35f),new Vector3(-.35f,.99f,.85f),
            new Vector3(-.35f,0,2.2f),new Vector3(-.35f,0,4) };
        for (int i=0;i<climb.path.Length;i++) climb.path[i].localPosition=climbPoints[i];
        Beat(first,new[]{"I'm over the palm and back on the ground.","The side lane took me around the fallen palm."});

        var cart = Node(second.transform,"Unique home-exit obstacle",Vector3.zero);
        Undo.RegisterCreatedObjectUndo(cart.gameObject,"Overturned supply cart");
        // Keep the walking line (x=-.65) clear, with the recognizable cart beside it.
        var body = Node(cart,"Overturned orange handcart",new Vector3(1.15f,.55f,.2f));
        body.localRotation=Quaternion.Euler(0,12,72);
        Shape(body,"Cart load bed",PrimitiveType.Cube,Vector3.zero,new Vector3(1.05f,.12f,1.8f),orange);
        for(int side=-1;side<=1;side+=2)
        {
            Shape(body,"Cart side rail",PrimitiveType.Cube,new Vector3(side*.5f,.35f,0),new Vector3(.07f,.7f,1.8f),metal);
            var wheel=Shape(body,"Exposed cart wheel",PrimitiveType.Cylinder,new Vector3(side*.65f,-.25f,.35f),new Vector3(.6f,.1f,.6f),metal);
            wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            Shape(body,"Cart push handle",PrimitiveType.Cube,new Vector3(side*.4f,.22f,-1.25f),new Vector3(.06f,.06f,.8f),metal);
        }
        Shape(body,"Cart handle crossbar",PrimitiveType.Cube,new Vector3(0,.22f,-1.63f),new Vector3(.85f,.07f,.07f),metal);
        DescribeExit(second,SibucaoHazardKind.Supplies,"Supplies spilled from an overturned cart",
            "The second street obstacle is an overturned delivery cart. Recover the two sealed supplies with E, then continue, or use the side lane.");
        var gather=second.branches[0];gather.mechanism=PhysicalMechanism.GatherItems;
        LabelExitBranch(gather,"Recover the two sealed supplies","You recovered the sealed supplies and continued past the cart.");
        foreach(var point in gather.path) {var p=point.localPosition;p.y=0;point.localPosition=p;}
        gather.actionPoint.localPosition=new Vector3(-.65f,1,-2);
        var templates=s.homes[0].GetComponentsInChildren<PhysicalGrabbable>(true).Where(x=>x.kind==PhysicalItemKind.Supply).ToArray();
        for(int i=0;i<2;i++)
        {
            var item=Object.Instantiate(templates.First(x=>x.supplyBit==i),cart);item.name=i==0?"Sealed drinking water - E pickup":"Sealed food pack - E pickup";
            if(i==1)foreach(var detail in item.GetComponentsInChildren<Transform>().Where(x=>x.name=="Flashlight").ToArray())Object.DestroyImmediate(detail.gameObject);
            item.persistentId="D34_exit_supply_"+i;item.station=second;item.supplyBit=-1;
            item.inventoryTag=i==0?"water":"food";item.secured=item.delivered=false;
            item.transform.localPosition=new Vector3(i==0?-1.25f:.05f,.35f,i==0?-1.4f:1.5f);
            item.Body.isKinematic=true;item.gameObject.SetActive(true);
        }
        Beat(second,new[]{"Both sealed supplies are in my bag. I'm continuing past the cart.","I'm taking the side lane around the overturned cart."});
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        return "D33: fallen palm climb. D34: overturned supply cart and two pickups. Existing side lanes and station IDs retained.";
    }
    static void DescribeExit(PhysicalDecisionStation station,SibucaoHazardKind kind,string title,string description)
    {
        station.title=title;station.name=station.node.id+" - "+title;
        var meta=station.GetComponent<SibucaoDiegeticHazard>();meta.kind=kind;meta.reason=description;
        station.node.title=title;station.node.prompt=description;
        EditorUtility.SetDirty(meta);EditorUtility.SetDirty(station.node);EditorUtility.SetDirty(station);
    }
    static void LabelExitBranch(SibucaoPhysicalOption branch,string label,string outcome)
    {
        branch.verb=label;branch.name="C0 - "+label;branch.Choice.label=label;branch.Choice.outcome=outcome;
        EditorUtility.SetDirty(branch);EditorUtility.SetDirty(branch.Choice);
    }
}
