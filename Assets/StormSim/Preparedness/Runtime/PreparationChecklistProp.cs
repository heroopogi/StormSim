using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    [DisallowMultipleComponent]
    public sealed class PreparationChecklistProp : MonoBehaviour
    {
        PreparednessPlayer player;
        PreparednessSession session;
        Transform root,frontCoverPivot,pencil;
        TextMesh leftPage,rightPage,coverTitle;
        float blend,nextRefresh;
        bool wasOpen;
        readonly Queue<int> pendingChecks=new Queue<int>();
        bool[] observed,inked;
        int activeCheck=-1,displayStart;
        float checkStartedAt=-1;

        public bool BookReady=>root&&frontCoverPivot&&leftPage&&rightPage;
        public int PageCount=>BookReady?2:0;
        public bool HasPencil=>pencil;
        public string ChecklistFontName=>leftPage&&leftPage.font?leftPage.font.name:"";

        void Awake()
        {
            player=GetComponent<PreparednessPlayer>();
            session=player?player.session:null;
            Build();
            if(session!=null){observed=(bool[])session.decisions.Clone();inked=(bool[])session.decisions.Clone();}
        }

        void Build()
        {
            root=new GameObject("Animated preparation field guide").transform;
            root.SetParent(transform,false);root.gameObject.SetActive(false);

            // A hinged cover reveals two paper pages, replacing the former flat panel.
            var back=Part("Weathered back cover",new Vector3(.31f,.43f,.022f),new Color(.20f,.075f,.035f));
            back.transform.SetParent(root,false);back.transform.localPosition=new Vector3(.155f,0,.018f);
            var spine=Part("Bound spine",new Vector3(.035f,.445f,.052f),new Color(.12f,.045f,.025f));
            spine.transform.SetParent(root,false);spine.transform.localPosition=new Vector3(0,0,-.006f);

            frontCoverPivot=new GameObject("Opening cover hinge").transform;
            frontCoverPivot.SetParent(root,false);frontCoverPivot.localPosition=new Vector3(0,0,-.012f);
            PageBlock("Left page block",frontCoverPivot,new Vector3(-.154f,0,0),-3);
            PageBlock("Right page block",root,new Vector3(.154f,0,-.012f),3);
            var cover=Part("Embossed front cover",new Vector3(.31f,.43f,.026f),new Color(.27f,.10f,.045f));
            cover.transform.SetParent(frontCoverPivot,false);cover.transform.localPosition=new Vector3(-.155f,0,.026f);
            var band=Part("Guide cover band",new Vector3(.22f,.055f,.012f),new Color(.76f,.57f,.25f));
            band.transform.SetParent(frontCoverPivot,false);band.transform.localPosition=new Vector3(-.155f,.08f,.041f);
            coverTitle=Writing("Handbook cover title",frontCoverPivot,new Vector3(-.269f,.145f,.043f));
            coverTitle.characterSize=.0035f;coverTitle.text="PREPARATION\nGUIDE\n\nBARANGAY\nHANDBOOK";

            leftPage=Writing("Left page writing",frontCoverPivot,new Vector3(-.278f,.182f,-.025f));
            rightPage=Writing("Right page writing",root,new Vector3(.022f,.182f,-.041f));
            BuildTabs();BuildPencil();
        }

        void BuildTabs()
        {
            Color[] colors={new Color(.72f,.18f,.14f),new Color(.10f,.30f,.52f),new Color(.86f,.59f,.12f),new Color(.18f,.49f,.31f),new Color(.56f,.24f,.48f)};
            for(int i=0;i<5;i++)
            {
                var tab=Part(PreparednessSession.GroupNames[i]+" tab",new Vector3(.04f,.055f,.012f),colors[i]);
                tab.transform.SetParent(root,false);tab.transform.localPosition=new Vector3(.325f,.145f-i*.07f,-.035f);
            }
        }

        void BuildPencil()
        {
            pencil=new GameObject("Checklist pencil").transform;pencil.SetParent(frontCoverPivot,false);
            Cylinder("Yellow pencil",pencil,new Vector3(0,0,0),new Vector3(.008f,.078f,.008f),new Color(.92f,.66f,.08f));
            Cylinder("Metal ferrule",pencil,new Vector3(0,.086f,0),new Vector3(.009f,.009f,.009f),new Color(.55f,.58f,.55f));
            Cylinder("Pink eraser",pencil,new Vector3(0,.102f,0),new Vector3(.009f,.008f,.009f),new Color(.78f,.35f,.38f));
            Cylinder("Graphite tip",pencil,new Vector3(0,-.088f,0),new Vector3(.004f,.012f,.004f),new Color(.08f,.07f,.055f));
            SetPencilRest();
        }

        static void Cylinder(string name,Transform parent,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider)Destroy(collider);
            go.GetComponent<Renderer>().material.color=color;
        }

        void PageBlock(string name,Transform parent,Vector3 position,float tilt)
        {
            var page=Part(name,new Vector3(.292f,.402f,.018f),new Color(.94f,.90f,.77f));
            page.transform.SetParent(parent,false);page.transform.localPosition=position;
            page.transform.localRotation=Quaternion.Euler(0,tilt,0);
            for(int i=0;i<3;i++)
            {
                var edge=Part(name+" sheet "+(i+1),new Vector3(.287f,.397f,.002f),new Color(.82f+.025f*i,.80f+.025f*i,.69f+.025f*i));
                edge.transform.SetParent(parent,false);edge.transform.localPosition=position+new Vector3(0,0,-.011f-.004f*i);
                edge.transform.localRotation=page.transform.localRotation;
            }
        }

        TextMesh Writing(string name,Transform parent,Vector3 position)
        {
            var textObject=new GameObject(name);textObject.transform.SetParent(parent,false);
            textObject.transform.localPosition=position;textObject.transform.localRotation=Quaternion.identity;
            var writing=textObject.AddComponent<TextMesh>();
            writing.font=Resources.Load<Font>("SignalStation/RobotoMono-Bold");
            if(!writing.font)writing.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            writing.fontSize=48;writing.characterSize=.0032f;writing.anchor=TextAnchor.UpperLeft;writing.alignment=TextAlignment.Left;
            writing.color=new Color(.055f,.075f,.065f);writing.richText=true;return writing;
        }

        static GameObject Part(string name,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider)Destroy(collider);
            go.GetComponent<Renderer>().material.color=color;return go;
        }

        void LateUpdate()
        {
            if(!player||!player.view||!session)return;
            DetectCompletedChecks();UpdatePencilAnimation();
            bool requestedOpen=player.GuideOpen;
            bool open=requestedOpen||activeCheck>=0;
            if(requestedOpen&&!wasOpen)session.NarrateChecklist();
            wasOpen=requestedOpen;
            blend=Mathf.MoveTowards(blend,open?1:0,Time.unscaledDeltaTime*3.4f);
            if(blend<=0){root.gameObject.SetActive(false);return;}
            root.gameObject.SetActive(true);
            float eased=blend*blend*(3-2*blend);
            float coverOpen=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.82f,blend));
            frontCoverPivot.localRotation=Quaternion.Euler(0,Mathf.Lerp(178,3,coverOpen),0);
            // The title belongs to the outside face of the cover. Hiding it once
            // opened prevents it from showing through the checklist page.
            if(coverTitle)coverTitle.gameObject.SetActive(coverOpen<.45f);

            var view=player.view.transform;
            Vector3 hidden=view.TransformPoint(new Vector3(0,-.62f,.82f));
            Vector3 shown=view.TransformPoint(new Vector3(0,-.16f,.90f));
            root.position=Vector3.Lerp(hidden,shown,eased);
            root.rotation=Quaternion.Slerp(view.rotation*Quaternion.Euler(20,0,0),view.rotation*Quaternion.Euler(7,0,0),eased);
            root.localScale=Vector3.one*Mathf.Lerp(.58f,.82f,eased);
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;RefreshText();}
        }

        void DetectCompletedChecks()
        {
            if(observed==null||inked==null){observed=(bool[])session.decisions.Clone();inked=(bool[])session.decisions.Clone();return;}
            var changed=new List<int>();
            for(int i=0;i<session.decisions.Length;i++)
            {
                if(session.decisions[i]&&!observed[i])changed.Add(i);
                if(!session.decisions[i])inked[i]=false;
                observed[i]=session.decisions[i];
            }
            // Loading an established save can change many decisions at once; restore
            // its ink without replaying a long queue of celebration animations.
            if(changed.Count>3){for(int i=0;i<inked.Length;i++)inked[i]=session.decisions[i];return;}
            foreach(int index in changed)pendingChecks.Enqueue(index);
            if(activeCheck<0&&pendingChecks.Count>0)StartNextCheck();
        }

        void StartNextCheck()
        {
            activeCheck=pendingChecks.Dequeue();checkStartedAt=Time.unscaledTime;
            displayStart=Mathf.Clamp(activeCheck-2,0,Mathf.Max(0,PreparednessSession.Titles.Length-6));
        }

        void UpdatePencilAnimation()
        {
            if(!pencil)return;
            if(activeCheck<0){SetPencilRest();return;}
            float t=(Time.unscaledTime-checkStartedAt)/1.35f;
            int row=Mathf.Clamp(activeCheck-displayStart,0,5);
            Vector3 rest=new Vector3(-.315f,-.02f,-.073f);
            Vector3 target=new Vector3(-.268f,.042f-row*.039f,-.073f);
            float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.38f,t));
            float leave=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1.15f,t));
            Vector3 tip=Vector3.Lerp(rest,target,approach);tip=Vector3.Lerp(tip,rest,leave);
            float stroke=t<.58f?Mathf.InverseLerp(.38f,.58f,t):1-Mathf.InverseLerp(.58f,.82f,t);
            pencil.localPosition=tip+new Vector3(.018f*stroke,.012f*Mathf.Abs(stroke-.5f),0);
            pencil.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-18,22,stroke));
            if(t>=.68f)inked[activeCheck]=true;
            if(t>=1.35f)
            {
                inked[activeCheck]=true;activeCheck=-1;checkStartedAt=-1;PreparationFeedback.Confirm();
                if(pendingChecks.Count>0)StartNextCheck();
            }
        }

        void SetPencilRest()
        {
            pencil.localPosition=new Vector3(-.315f,-.02f,-.073f);
            pencil.localRotation=Quaternion.Euler(0,0,-18);
        }

        void RefreshText()
        {
            int complete=session.decisions.Count(value=>value);
            int anchor=activeCheck>=0?activeCheck:session.decisions.TakeWhile(value=>value).Count();
            if(activeCheck<0)displayStart=Mathf.Clamp(anchor-2,0,Mathf.Max(0,PreparednessSession.Titles.Length-6));
            var rows=Enumerable.Range(displayStart,Mathf.Min(6,PreparednessSession.Titles.Length-displayStart))
                .Select(i=>(inked!=null&&inked[i]?"[✓] ":"[ ] ")+PreparednessSession.Titles[i]);
            leftPage.text="PREPARATION GUIDE\n"+complete+" / 20 READY\n\n"+string.Join("\n",rows)+
                "\n\nChecklist "+(displayStart+1)+"–"+Mathf.Min(displayStart+6,PreparednessSession.Titles.Length);
            rightPage.text="NEXT STEP\n\n"+PreparationGuidanceText.Wrap(session.NextPreparationStep,27,6)+
                "\n\nCONTROLS\nJ / LEFT X  CLOSE\nE / TRIGGER  USE\nQ / RELEASE  PUT DOWN";
        }

        void OnDestroy(){if(root)Destroy(root.gameObject);}
    }

    public static class PreparationGuidanceText
    {
        public static string GroupMark(PreparednessSession session,int group)
        {
            var checks=PreparednessSession.GroupChecks[group];int done=checks.Count(i=>session.decisions[i]);
            return done==checks.Length?"[x]":done>0?"[-]":"[ ]";
        }

        public static string Wrap(string value,int width,int maxLines)
        {
            if(string.IsNullOrWhiteSpace(value))return "Review the television advisory.";
            var words=value.Split(' ');string result="",line="";int lines=1;
            foreach(var word in words)
            {
                if(line.Length>0&&line.Length+word.Length+1>width)
                {
                    result+=line+"\n";line="";lines++;
                    if(lines>maxLines){result=result.TrimEnd()+"…";return result;}
                }
                line+=(line.Length>0?" ":"")+word;
            }
            return result+line;
        }
    }
}
