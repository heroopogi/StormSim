using System.Linq;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed partial class PreparednessPlayer
    {
        bool departureReview,reviewEvacuate;
        bool householdHelp,layoutHelp;
        Vector2 reviewScroll,bagScroll;
        public bool StormglassDepartureOpen=>departureReview;
        public void StormglassConfirmDeparture()
        {
            if(!departureReview)return;
            if(session.TryFinish(reviewEvacuate,true))CloseDesktopPanels();
        }
        public void OpenDepartureReview(bool evacuate)
        {
            if(XRActive){session.Say(session.CapacityText+". Hold left primary to review the household plan. Use again to confirm.");session.Finish(evacuate);return;}
            departureReview=true;reviewEvacuate=evacuate;bagPanel=journal=false;reviewScroll=Vector2.zero;SetPanelCursor();
        }
        void DrawSelectedBag(Rect viewport)
        {
            GUILayout.BeginArea(viewport);bagScroll=GUILayout.BeginScrollView(bagScroll);
            GUILayout.Label(session.CapacityText,SibucaoDesktopHud.Text);
            if(held)GUILayout.Label($"Held: {held.displayName} — {held.mass:0.0} kg\n"+session.ItemReadiness(held),SibucaoDesktopHud.Small);
            var bag=session.Bag;
            bool accessible=bag&&session.CanPrepare&&(carriedBag==bag||Vector3.Distance(view.transform.position,bag.WorldBounds().ClosestPoint(view.transform.position))<=reach);
            bool previous=GUI.enabled;GUI.enabled=accessible;
            if(GUILayout.Button(bag&&bag.closed?"Open bag to retrieve items":"Close bag",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))session.Use(bag);
            GUI.enabled=previous;
            GUILayout.Label(!accessible?"Collect the bag or stand within reach to change its contents.":held?"Put down your held item before retrieving another.":bag.closed?"Open the bag first.":"Choose the item you want to retrieve.",SibucaoDesktopHud.Small);
            var contents=session.items.Where(i=>i.IsPacked).OrderBy(i=>session.ItemCategory(i)).ThenBy(i=>i.displayName).ToArray();
            if(contents.Length==0)GUILayout.Label("Your bag is empty.",SibucaoDesktopHud.Text);
            string category=null;
            foreach(var item in contents)
            {
                var nextCategory=session.ItemCategory(item);
                if(nextCategory!=category){category=nextCategory;GUILayout.Space(10);GUILayout.Label(category.ToUpperInvariant(),SibucaoDesktopHud.Small);}
                GUILayout.Space(8);
                GUILayout.Label($"{item.displayName} · {item.mass:0.0} kg\n"+session.ItemReadiness(item),SibucaoDesktopHud.Text);
                GUI.enabled=previous&&accessible&&!bag.closed&&!held;
                if(GUILayout.Button("Remove from bag and place nearby",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))
                    if(session.DropFromBag(item))CloseDesktopPanels();
                GUI.enabled=previous;
            }
            GUILayout.Space(8);GUILayout.Label("Preparation time pauses while reading. Items packed without checking may still need preparation.",SibucaoDesktopHud.Small);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void DrawDepartureReview(SibucaoHudLayout layout)
        {
            var r=layout.Modal;SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+18,r.width-44,40),reviewEvacuate?"Before you leave":"Before you rest",SibucaoDesktopHud.Heading);
            GUILayout.BeginArea(new Rect(r.x+22,r.y+66,r.width-44,r.height-154));reviewScroll=GUILayout.BeginScrollView(reviewScroll);
            GUILayout.Label(reviewEvacuate?"Leave with what you have. No checklist completion is required.":"Rest advances to the morning evacuation advisory. Your supplies stay where you left them.",SibucaoDesktopHud.Text);
            GUILayout.Label(session.DepartureReview,SibucaoDesktopHud.Small);GUILayout.EndScrollView();GUILayout.EndArea();
            float w=(r.width-56)/2;
            if(GUI.Button(new Rect(r.x+22,r.yMax-64,w,44),reviewEvacuate?"Leave now":"Rest until morning",SibucaoDesktopHud.ButtonStyle))
            {
                if(session.TryFinish(reviewEvacuate,true))CloseDesktopPanels();
            }
            if(GUI.Button(new Rect(r.x+34+w,r.yMax-64,w,44),"Keep preparing",SibucaoDesktopHud.ButtonStyle))CloseDesktopPanels();
        }
        void DrawPreparationHelp()
        {
            GUILayout.Label(session.WeatherText,SibucaoDesktopHud.Small);
            if(GUILayout.Button(session.toggleWork?"Work controls: press once (switch to hold)":"Work controls: hold (switch to press once)",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))
            {session.CancelInteraction();session.toggleWork=!session.toggleWork;}
            GUILayout.Label(SibucaoControls.Prompt("Work pauses when you look away. Partial work is kept; Q stops work safely. Reading panels pause the preparation clock and charging."),SibucaoDesktopHud.Small);
            if(GUILayout.Button("Replay weather bulletin",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))session.Say(PreparednessSession.BulletinText);
            GUILayout.Label(PreparednessSession.BulletinText,SibucaoDesktopHud.Small);
            if(GUILayout.Button((householdHelp?"− ":"+ ")+"Household care, supplies and route plan",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))householdHelp=!householdHelp;
            if(householdHelp)GUILayout.Label(PreparednessSession.HouseholdCard+"\n\n"+PreparednessSession.QuantityGuide+"\n\n"+session.RouteDescription(true)+"\n\n"+session.RouteDescription(false),SibucaoDesktopHud.Text);
            if(GUILayout.Button((layoutHelp?"− ":"+ ")+"Where to find things",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))layoutHelp=!layoutHelp;
            if(layoutHelp)
            {
                foreach(var kind in new[]{PrepKind.Bag,PrepKind.Flashlight,PrepKind.Radio,PrepKind.Documents,PrepKind.FirstAid,PrepKind.Phone})
                {var item=session.items.FirstOrDefault(i=>i.kind==kind);if(item)GUILayout.Label(item.displayName+" — "+session.LocationOf(item),SibucaoDesktopHud.Small);}
                GUILayout.Label("Household water: containers beside the supply shelf. Portable bottle: packing table. Home protection: high shelf and window. Route cards: beside the TV. Keep the exit path clear.",SibucaoDesktopHud.Small);
            }
            if(GUILayout.Button("Review current supplies and departure",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))OpenDepartureReview(true);
            if(GUILayout.Button("Save preparation checkpoint",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))session.Save();
            if(session.HasPreparationSave&&GUILayout.Button("Resume preparation checkpoint",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))session.Load();
            GUILayout.Label(session.CheckpointText,SibucaoDesktopHud.Small);
        }
        static bool IsInteractable(PreparednessItem item)=>item&&(item.portable||item.kind!=PrepKind.Decoration||item.GetComponent<PreparednessWasteBin>()||item.GetComponent<PreparednessLadder>());
        void ForgivingFocus(Transform source,float obstructionDistance)
        {
            foreach(var hit in Physics.SphereCastAll(source.position,.065f,source.forward,reach,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
            {
                var item=hit.collider.GetComponentInParent<PreparednessItem>();
                if(!IsInteractable(item)||!item.portable||item==held||item==carriedBag||hit.distance>obstructionDistance+.02f)continue;
                var target=hit.collider.ClosestPoint(source.position);var delta=target-source.position;
                bool blocked=Physics.RaycastAll(source.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(transform)&&h.collider.GetComponentInParent<PreparednessItem>()!=item&&h.collider.GetComponentInParent<PreparednessItem>()!=held&&h.collider.GetComponentInParent<PreparednessItem>()!=carriedBag);
                if(!blocked){focused=item;return;}
            }
        }
    }
    public sealed class PreparednessFocusFeedback : MonoBehaviour
    {
        public PreparednessPlayer player;
        void OnGUI()
        {
            if(!player||player.XRActive||player.ReadingPanel||!player.focused||SibucaoSession.Instance&&SibucaoSession.Instance.Mode!=SibucaoMode.Preparation)return;
            var point=player.view.WorldToScreenPoint(player.focused.WorldBounds().center);if(point.z<=0)return;
            var old=GUI.color;GUI.color=new Color(.8f,1,.92f,.9f);float x=point.x,y=Screen.height-point.y;
            // Small brackets plus the action text provide a cue that does not rely on color.
            foreach(float sign in new[]{-1f,1f}){GUI.DrawTexture(new Rect(x+sign*14,y-10,2,20),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x+(sign<0?-14:8),y-10,8,2),Texture2D.whiteTexture);}
            GUI.color=old;
        }
    }
}
