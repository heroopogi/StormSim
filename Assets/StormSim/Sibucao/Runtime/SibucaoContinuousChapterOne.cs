using System;
using System.Collections.Generic;
using StormSim.Preparedness;
using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>
    /// The first continuous evacuation chapter. It follows the real route and records
    /// decisions without presenting the player with a visible decision-tree menu.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class SibucaoContinuousChapterOne : MonoBehaviour
    {
        const string RootName="CHAPTER 1 - Leaving the neighborhood";
        const float InteractionDistance=4.5f;
        readonly Dictionary<Collider,Target> targets=new Dictionary<Collider,Target>();
        Transform chapterRoot,advisory,parent,depthGauge,chapterExit;
        Transform[] packages=Array.Empty<Transform>();
        float advisoryDistance,parentDistance,depthDistance,exitDistance;
        bool ready;

        enum Kind { Advisory, Parent, Package, Depth }
        sealed class Target
        {
            public Kind kind;
            public int index;
            public Transform visual;
        }

        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S&&S.state!=null?S.state.physical:null;
        public bool Complete=>Has("chapter1_complete");
        public string ShortTitle=>Complete?"Continue to the evacuation center":Stage==0?"Barangay storm advisory":Stage<3?"Parent and supply cart":Stage==3?"Flood-depth marker":"Raised evacuation route";
        public Transform ContextTarget
        {
            get {var target=Focused();return target?.visual;}
        }
        public Vector3 NavigationTarget
        {
            get
            {
                if(!ready) return S&&S.route?S.route.Sample(0):transform.position;
                switch(Stage)
                {
                    case 0:return advisory.position;
                    case 1:return parent.position;
                    case 2:
                        foreach(var p in packages)if(p&&p.gameObject.activeSelf)return p.position;
                        return parent.position;
                    case 3:return depthGauge.position;
                    case 4:return chapterExit.position;
                    default:return S.route.Sample(Mathf.Min(S.route.length,exitDistance+16));
                }
            }
        }
        public string Objective
        {
            get
            {
                switch(Stage)
                {
                    case 0:return "Follow the evacuation route and check the barangay storm advisory.";
                    case 1:return "A parent is stranded beside a supply cart. Check what happened.";
                    case 2:return "Secure the scattered family supplies before the water carries them away.";
                    case 3:return "Check the flood depth before entering the next street.";
                    case 4:return "Use the raised edge of the street and reach the next dry marker.";
                    default:return "Continue evacuating toward Banago Elementary School I.";
                }
            }
        }
        public string Hint
        {
            get
            {
                var focus=Focused();
                if(focus!=null)return PromptFor(focus);
                switch(Stage)
                {
                    case 0:return "Follow the map marker. Aim at the small barangay advisory board and press E.";
                    case 1:return "Approach the parent beside the cart, look at them, and press E.";
                    case 2:return "Look at each loose supply pack and press E. Packed gloves or a whistle change how safely you can help.";
                    case 3:return "Look at the marked bamboo depth gauge and press E before crossing.";
                    case 4:return "Walk along the visible raised boards. You can still continue if you ignored the depth check, but the decision is recorded.";
                    default:return "Keep following the real street route toward the evacuation center.";
                }
            }
        }
        public string Prompt
        {
            get
            {
                var target=Focused();
                if(target!=null)return PromptFor(target);
                return Vector3.Distance(S.player.transform.position,NavigationTarget)<7?Hint:null;
            }
        }
        public string ClearInstructions=>Hint+"\nWASD move · Mouse look · E interact · Shift run";
        public int Stage
        {
            get
            {
                if(Has("chapter1_complete"))return 5;
                if(Has("chapter1_depth_checked")||Has("chapter1_depth_skipped"))return 4;
                if(Has("chapter1_parent_supplies_secured")||Has("chapter1_parent_skipped"))return 3;
                if(Has("chapter1_parent_contact"))return 2;
                if(Has("chapter1_advisory_checked")||Has("chapter1_advisory_skipped"))return 1;
                return 0;
            }
        }

        void Start(){BuildAndLayout();RestorePresentation();}

        public void ResetRun()
        {
            BuildAndLayout();
            RestorePresentation();
        }

        public void Layout()
        {
            if(!Application.isPlaying)return;
            BuildAndLayout();
            RestorePresentation();
        }

        public void RestoreRun()
        {
            BuildAndLayout();
            RestorePresentation();
        }

        void BuildAndLayout()
        {
            if(!S||!S.route||Data==null)return;
            if(chapterRoot)Destroy(chapterRoot.gameObject);
            targets.Clear();
            chapterRoot=new GameObject(RootName).transform;
            chapterRoot.SetParent(transform,false);

            float start=Mathf.Clamp(Data.routeStart,0,Mathf.Max(0,S.route.length-90));
            advisoryDistance=Mathf.Min(S.route.length-64,start+14);
            parentDistance=Mathf.Min(S.route.length-46,advisoryDistance+18);
            depthDistance=Mathf.Min(S.route.length-26,parentDistance+21);
            exitDistance=Mathf.Min(S.route.length-8,depthDistance+20);

            advisory=CreateAdvisory(Pose(advisoryDistance,-1.75f));
            parent=CreateParentIncident(Pose(parentDistance,1.9f));
            depthGauge=CreateDepthGauge(Pose(depthDistance,-1.65f));
            CreateRaisedRoute(depthDistance+2,exitDistance);
            chapterExit=CreateRouteMarker("Next dry marker",Pose(exitDistance,0),new Color(.96f,.72f,.18f));
            ready=true;Physics.SyncTransforms();
        }

        (Vector3 position,Quaternion rotation,Vector3 right) Pose(float distance,float lateral)
        {
            var p=S.route.Sample(distance);
            var tangent=S.route.Sample(Mathf.Min(S.route.length,distance+2))-S.route.Sample(Mathf.Max(0,distance-2));
            tangent.y=0;if(tangent.sqrMagnitude<.01f)tangent=Vector3.forward;tangent.Normalize();
            var right=Vector3.Cross(Vector3.up,tangent).normalized;
            return (p+right*lateral,Quaternion.LookRotation(tangent),right);
        }

        Transform CreateAdvisory((Vector3 position,Quaternion rotation,Vector3 right) pose)
        {
            var root=Group("Barangay storm advisory",pose.position,pose.rotation);
            Part(root,"Post",PrimitiveType.Cylinder,new Vector3(0,1.05f,0),new Vector3(.08f,1.05f,.08f),new Color(.34f,.24f,.13f));
            var board=Part(root,"Official advisory board",PrimitiveType.Cube,new Vector3(0,1.75f,0),new Vector3(1.25f,.55f,.09f),new Color(.9f,.72f,.2f));
            Part(board,"Raised-route stripe",PrimitiveType.Cube,new Vector3(0,-.1f,-.52f),new Vector3(.72f,.08f,.55f),new Color(.1f,.31f,.36f));
            Register(root,Kind.Advisory,0,board,new Vector3(1.55f,2.2f,.8f),new Vector3(0,1.35f,0));
            return root;
        }

        Transform CreateParentIncident((Vector3 position,Quaternion rotation,Vector3 right) pose)
        {
            var root=Group("Parent beside supply cart",pose.position,pose.rotation);
            var person=Group("Parent",new Vector3(-.75f,0,0),Quaternion.identity,root);
            Part(person,"Body",PrimitiveType.Capsule,new Vector3(0,1.05f,0),new Vector3(.34f,.58f,.28f),new Color(.22f,.43f,.48f));
            Part(person,"Head",PrimitiveType.Sphere,new Vector3(0,1.92f,0),new Vector3(.24f,.24f,.24f),new Color(.48f,.31f,.23f));
            Part(person,"Rain hood",PrimitiveType.Sphere,new Vector3(0,1.97f,.02f),new Vector3(.29f,.27f,.27f),new Color(.92f,.67f,.18f));
            Part(person,"Face",PrimitiveType.Sphere,new Vector3(0,1.9f,.21f),new Vector3(.17f,.18f,.08f),new Color(.55f,.36f,.26f));
            Part(person,"Left arm",PrimitiveType.Capsule,new Vector3(-.38f,1.12f,.06f),new Vector3(.11f,.42f,.11f),new Color(.22f,.43f,.48f),Quaternion.Euler(12,0,-18));
            Part(person,"Right arm",PrimitiveType.Capsule,new Vector3(.38f,1.12f,.06f),new Vector3(.11f,.42f,.11f),new Color(.22f,.43f,.48f),Quaternion.Euler(12,0,18));
            Part(person,"Left leg",PrimitiveType.Capsule,new Vector3(-.17f,.39f,0),new Vector3(.13f,.42f,.13f),new Color(.12f,.19f,.23f));
            Part(person,"Right leg",PrimitiveType.Capsule,new Vector3(.17f,.39f,0),new Vector3(.13f,.42f,.13f),new Color(.12f,.19f,.23f));
            Register(root,Kind.Parent,0,person,new Vector3(1.1f,2.25f,1.1f),new Vector3(-.75f,1.1f,0));

            var cart=Group("Family supply cart",new Vector3(.55f,0,.2f),Quaternion.identity,root);
            Part(cart,"Cart bed",PrimitiveType.Cube,new Vector3(0,.52f,0),new Vector3(.9f,.18f,.58f),new Color(.33f,.24f,.17f));
            Part(cart,"Cart rail",PrimitiveType.Cube,new Vector3(0,.83f,-.26f),new Vector3(.9f,.06f,.06f),new Color(.4f,.29f,.18f));
            Part(cart,"Left handle",PrimitiveType.Cylinder,new Vector3(-.38f,.84f,.48f),new Vector3(.035f,.48f,.035f),new Color(.29f,.21f,.15f),Quaternion.Euler(58,0,0));
            Part(cart,"Right handle",PrimitiveType.Cylinder,new Vector3(.38f,.84f,.48f),new Vector3(.035f,.48f,.035f),new Color(.29f,.21f,.15f),Quaternion.Euler(58,0,0));
            Part(cart,"Wheel left",PrimitiveType.Cylinder,new Vector3(-.48f,.32f,0),new Vector3(.2f,.08f,.2f),new Color(.08f,.08f,.08f),Quaternion.Euler(0,0,90));
            Part(cart,"Wheel right",PrimitiveType.Cylinder,new Vector3(.48f,.32f,0),new Vector3(.2f,.08f,.2f),new Color(.08f,.08f,.08f),Quaternion.Euler(0,0,90));

            packages=new Transform[3];
            var colors=new[]{new Color(.85f,.38f,.17f),new Color(.18f,.55f,.6f),new Color(.78f,.68f,.22f)};
            for(int i=0;i<packages.Length;i++)
            {
                var pack=Group("Loose family supply "+(i+1),new Vector3(-.25f+i*.62f,.18f,1.15f+(i%2)*.36f),Quaternion.Euler(0,i*17,0),root);
                var visual=Part(pack,"Sealed supply pack",PrimitiveType.Cube,Vector3.zero,new Vector3(.38f,.18f,.3f),colors[i]);
                Part(visual,"Reflective band",PrimitiveType.Cube,new Vector3(0,.54f,0),new Vector3(.75f,.05f,1.04f),new Color(.92f,.9f,.72f));
                Register(root,Kind.Package,i,visual,new Vector3(.72f,.65f,.72f),pack.localPosition+Vector3.up*.05f);
                packages[i]=pack;
            }
            return root;
        }

        Transform CreateDepthGauge((Vector3 position,Quaternion rotation,Vector3 right) pose)
        {
            var root=Group("Flood depth check",pose.position,pose.rotation);
            var pole=Part(root,"Bamboo depth gauge",PrimitiveType.Cylinder,new Vector3(0,.9f,0),new Vector3(.055f,.9f,.055f),new Color(.68f,.55f,.26f));
            for(int i=0;i<4;i++)Part(root,"Depth mark "+(i+1),PrimitiveType.Cube,new Vector3(0,.28f+i*.32f,-.07f),new Vector3(.18f,.025f,.025f),i<2?new Color(.28f,.74f,.57f):new Color(.92f,.55f,.17f));
            Part(root,"Stable footing",PrimitiveType.Cube,new Vector3(0,.08f,0),new Vector3(1.15f,.12f,.75f),new Color(.43f,.31f,.18f));
            Register(root,Kind.Depth,0,pole,new Vector3(1.1f,2.1f,1.1f),new Vector3(0,.9f,0));
            return root;
        }

        void CreateRaisedRoute(float start,float end)
        {
            int count=Mathf.Max(4,Mathf.CeilToInt((end-start)/2.2f));
            for(int i=0;i<count;i++)
            {
                float d=Mathf.Lerp(start,end,(i+.5f)/count);var pose=Pose(d,-.85f);
                var board=Group("Raised route board "+(i+1),pose.position+Vector3.up*.1f,pose.rotation,chapterRoot);
                Part(board,"Board",PrimitiveType.Cube,Vector3.zero,new Vector3(.72f,.09f,1.05f),new Color(.46f,.32f,.18f));
                Part(board,"Reflective edge",PrimitiveType.Cube,new Vector3(0,.56f,0),new Vector3(.76f,.025f,.92f),new Color(.95f,.72f,.16f),Quaternion.identity,false);
            }
        }

        Transform CreateRouteMarker(string name,(Vector3 position,Quaternion rotation,Vector3 right) pose,Color color)
        {
            var root=Group(name,pose.position,pose.rotation);
            Part(root,"Marker post",PrimitiveType.Cylinder,new Vector3(0,.75f,0),new Vector3(.055f,.75f,.055f),new Color(.3f,.24f,.15f),Quaternion.identity,false);
            Part(root,"Reflective marker",PrimitiveType.Cube,new Vector3(0,1.45f,0),new Vector3(.22f,.22f,.08f),color,Quaternion.identity,false);
            return root;
        }

        Transform Group(string name,Vector3 position,Quaternion rotation,Transform parentOverride=null)
        {
            var go=new GameObject(name);var t=go.transform;t.SetParent(parentOverride?parentOverride:chapterRoot,false);t.localPosition=position;t.localRotation=rotation;return t;
        }

        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 localPosition,Vector3 localScale,Color color,Quaternion? localRotation=null,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=localPosition;go.transform.localRotation=localRotation??Quaternion.identity;go.transform.localScale=localScale;
            var renderer=go.GetComponent<Renderer>();if(renderer){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}
            if(!collider){var c=go.GetComponent<Collider>();if(c)Destroy(c);}
            return go.transform;
        }

        void Register(Transform root,Kind kind,int index,Transform visual,Vector3 size,Vector3 localCenter)
        {
            var zone=new GameObject("Interaction - "+kind+(kind==Kind.Package?" "+(index+1):""));zone.transform.SetParent(root,false);zone.transform.localPosition=localCenter;
            var collider=zone.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=size;
            var target=new Target{kind=kind,index=index,visual=visual};targets[collider]=target;
            foreach(var visibleCollider in visual.GetComponentsInChildren<Collider>(true))targets[visibleCollider]=target;
        }

        Target Focused()
        {
            if(!ready||!S||!S.player||!S.player.view)return null;
            var camera=S.player.view.transform;
            var hits=Physics.RaycastAll(camera.position,camera.forward,InteractionDistance,~0,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(S.player.transform))continue;
                if(targets.TryGetValue(hit.collider,out var target)&&Available(target))return target;
                if(!hit.collider.isTrigger)break;
            }
            return null;
        }

        bool Available(Target target)
        {
            switch(target.kind)
            {
                case Kind.Advisory:return Stage==0;
                case Kind.Parent:return Stage==1;
                case Kind.Package:return Stage==2&&target.index>=0&&target.index<packages.Length&&packages[target.index].gameObject.activeSelf;
                case Kind.Depth:return Stage==3;
                default:return false;
            }
        }

        string PromptFor(Target target)
        {
            switch(target.kind)
            {
                case Kind.Advisory:return "E  Check the barangay storm advisory";
                case Kind.Parent:return "E  Ask the parent what help is needed";
                case Kind.Package:return "E  Secure family supply "+(target.index+1)+" of 3";
                case Kind.Depth:return "E  Read the bamboo flood-depth marker";
                default:return null;
            }
        }

        public bool Interact()
        {
            var target=Focused();if(target==null)return false;
            switch(target.kind)
            {
                case Kind.Advisory:ReadAdvisory();break;
                case Kind.Parent:TalkToParent();break;
                case Kind.Package:SecurePackage(target.index);break;
                case Kind.Depth:CheckDepth();break;
            }
            S.RequestCheckpoint();return true;
        }

        void ReadAdvisory()
        {
            Flag("chapter1_advisory_checked");
            if(ReadyRadio())
            {
                Flag("chapter1_advisory_radio");S.state.scores[0]+=20;
                S.Say("Your tested radio confirms the barangay instruction: keep to the raised edge and check water depth before crossing.",7);
            }
            else if(ReadyPhone())
            {
                Flag("chapter1_advisory_phone");S.state.scores[0]+=15;
                S.Say("Your charged phone shows the latest barangay alert: the raised street edge remains the safer route.",7);
            }
            else
            {
                Flag("chapter1_advisory_posted");S.state.scores[0]+=8;
                S.Say("The posted barangay marker directs evacuees to the raised edge of the street and the next depth gauge.",7);
            }
            S.Notify("Route information checked","Continue along the street. Someone ahead may need help.");
        }

        void TalkToParent()
        {
            Flag("chapter1_parent_contact");S.state.scores[2]+=10;
            if(HasPrepared("whistle"))
            {
                Flag("chapter1_parent_whistle");
                var item=PackedPrepared("whistle");var whistle=item?item.GetComponent<PosterSupply>():null;if(whistle)whistle.Signal();
                if(packages.Length>0&&packages[0]){packages[0].gameObject.SetActive(false);Flag("chapter1_package_0");}
                S.Say("Parent: Thank you. Your whistle brought another adult; they secured one pack. Please move the other supplies onto the cart.",8);
            }
            else if(HasPrepared("gloves"))
            {
                Flag("chapter1_parent_gloves");S.Say("Parent: Your gloves will help. Please secure the loose packs while I keep the cart from rolling.",7);
            }
            else S.Say("Parent: The cart shifted in the runoff. Please secure the loose packs carefully; the edges may be sharp.",7);
            S.Notify("Help the parent","Aim at each loose supply pack and press E.");
        }

        void SecurePackage(int index)
        {
            if(index<0||index>=packages.Length||Has("chapter1_package_"+index))return;
            Flag("chapter1_package_"+index);packages[index].gameObject.SetActive(false);
            bool gloves=Has("chapter1_parent_gloves");
            if(!gloves){Data.stamina=Mathf.Max(0,Data.stamina-3);Flag("chapter1_bare_hand_exposure");}
            int secured=0;for(int i=0;i<packages.Length;i++)if(Has("chapter1_package_"+i))secured++;
            SibucaoSoundFeedback.Play(SibucaoSoundKind.Placement);
            if(secured<packages.Length)S.Notify("Supply secured",(packages.Length-secured)+" pack"+(packages.Length-secured==1?"":"s")+" still loose.");
            else
            {
                Flag("chapter1_parent_supplies_secured");S.state.scores[2]+=gloves?30:20;S.state.scores[3]+=15;
                S.Say("Parent: The cart is stable now. The water gets deeper ahead—please check the bamboo gauge before you cross.",8);
                S.Notify("Parent ready to evacuate","Continue to the flood-depth marker ahead.");
            }
        }

        void CheckDepth()
        {
            Flag("chapter1_depth_checked");S.state.scores[0]+=25;S.state.scores[3]+=15;
            var flood=S.GetComponent<FloodSimulationManager>();float depth=flood?flood.GetWaterDepth(depthGauge.position):0;
            string reading=depth<.18f?"ankle depth":depth<.45f?"below-knee depth":"unsafe depth in the road centre";
            S.Say("The gauge shows "+reading+". The marked raised edge has the firmest visible footing.",7);
            S.Notify("Water assessed","Follow the raised boards to the next dry marker.");
        }

        void Update()
        {
            if(!ready||!S||S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked)return;
            float progress=S.route.Progress(S.player.transform.position);
            if(Stage==0&&progress>parentDistance-3){Flag("chapter1_advisory_skipped");S.Notify("Advisory missed","You continued without checking the latest local route information.");}
            if(Stage==1&&progress>depthDistance-4){Flag("chapter1_parent_skipped");S.Notify("Resident left behind","You passed the parent and supply cart without checking their situation.");}
            if(Stage==2&&progress>depthDistance+2){Flag("chapter1_parent_skipped");S.Notify("Assistance unfinished","Loose supplies remain beside the parent’s cart.");}
            if(Stage==3&&progress>depthDistance+6){Flag("chapter1_depth_skipped");S.Notify("Water entered without assessment","You crossed without reading the visible depth marker.");}
            if(Stage==4&&Flat(S.player.transform.position,chapterExit.position)<4)FinishChapter();
            RestorePresentation();
        }

        void FinishChapter()
        {
            if(Complete)return;Flag("chapter1_complete");S.state.scores[3]+=20;
            S.Notify("Neighborhood route cleared","Chapter 1 complete. Continue toward the evacuation center; later chapters will increase the pressure.");
            S.Say("You have cleared the first neighborhood section. Keep following the evacuation route toward the school.",7);
            S.RequestCheckpoint();
        }

        void RestorePresentation()
        {
            if(!ready)return;
            for(int i=0;i<packages.Length;i++)if(packages[i])packages[i].gameObject.SetActive(Stage==2&&!Has("chapter1_package_"+i));
            if(advisory)advisory.gameObject.SetActive(Stage<=0);
            if(parent)parent.gameObject.SetActive(Stage>=1&&Stage<=2);
            if(depthGauge)depthGauge.gameObject.SetActive(Stage==3);
            if(chapterExit)chapterExit.gameObject.SetActive(Stage==4);
        }

        PhysicalGrabbable PackedPrepared(string id)
        {
            if(!S.inventory)return null;
            for(int i=0;i<SibucaoInventory.Capacity;i++)
            {
                var item=S.inventory.Slot(i);var prepared=item?item.GetComponent<PreparednessItem>():null;
                if(prepared&&prepared.id==id)return item;
            }
            return null;
        }

        PhysicalGrabbable PackedTag(string tag)
        {
            if(!S.inventory)return null;
            for(int i=0;i<SibucaoInventory.Capacity;i++){var item=S.inventory.Slot(i);if(item&&item.inventoryTag==tag)return item;}
            return null;
        }

        bool HasPrepared(string id){var item=PackedPrepared(id);var prep=item?item.GetComponent<PreparednessItem>():null;return prep&&prep.prepared;}
        bool ReadyRadio(){var item=PackedTag("radio");var prep=item?item.GetComponent<PreparednessItem>():null;return prep&&prep.prepared&&prep.cells>=2;}
        bool ReadyPhone()
        {
            var phoneItem=PackedTag("phone");var phone=phoneItem?phoneItem.GetComponent<PreparednessItem>():null;
            if(phone&&phone.charge>=PreparednessSession.UsefulCharge)return true;
            var powerItem=PackedTag("powerbank");var power=powerItem?powerItem.GetComponent<PreparednessItem>():null;
            return phone&&phone.charge>0&&power&&power.charge>=PreparednessSession.UsefulCharge&&PackedTag("cable");
        }
        bool Has(string flag)=>Data!=null&&Data.flags!=null&&Data.flags.Contains(flag);
        void Flag(string flag){if(S.physicalDecisions)S.physicalDecisions.SetFlag(flag);else if(Data!=null&&!Data.flags.Contains(flag))Data.flags.Add(flag);}
        static float Flat(Vector3 a,Vector3 b){var d=a-b;return new Vector2(d.x,d.z).magnitude;}
    }
}
