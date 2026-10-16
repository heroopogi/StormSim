using System;
using System.Linq;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    [Serializable] public sealed class PreparationChapterSave
    {
        public int money=500;
        public bool needsDiscussed,meetingAgreed,assistanceAgreed,contactAgreed;
        public string[] purchased=Array.Empty<string>();
    }
    [Serializable] public sealed class PreparationProduct
    {
        public PreparednessItem item;
        public int price;
        public string description;
        public Vector3 shelfPosition;
    }
    // Text input and a future speech adapter share SubmitTranscript. All mutations remain local.
    public sealed class PreparationChapter : MonoBehaviour
    {
        public PreparednessSession session;
        public PreparednessItem family,shopkeeper,barangay;
        public PreparationProduct[] products=Array.Empty<PreparationProduct>();
        public PreparationChapterSave state=new PreparationChapterSave();
        public bool IsOpen=>speaker!=null;
        public event Action<string,string> ResponseReceived;
        PreparednessItem speaker;
        string draft="",reply="";
        Vector2 scroll;
        int selected=-1;
        public string StormglassSpeaker => speaker ? speaker.displayName : "Household";
        public string StormglassReply => reply;
        public bool StormglassIsShop => speaker == shopkeeper;
        public string[] StormglassTopics => speaker == family ? new[]{"What supplies do we need?","Where will we meet?","Who will help Lola?","What is our emergency contact?","Are we ready?"} : speaker == shopkeeper ? new[]{"What is available?","Do you have an alternative to food that needs cooking?"} : new[]{"What is the latest advisory?","Which route goes to school?","Who can help us?"};
        int xrChoice;bool xrNext,xrSelect,xrClose;
        string[] Topics()=>speaker==family?new[]{"What supplies do we need?","Where will we meet?","Who will help Lola?","What is our emergency contact?","Are we ready?"}:speaker==shopkeeper?new[]{"What is available?","Do you have an alternative to food that needs cooking?"}:new[]{"What is the latest advisory?","Which route goes to school?","Who can help us?"};
        string[] XRChoices()=>speaker==shopkeeper?products.Select(p=>p.item.displayName+" P"+p.price+(Owns(p.item)?" PAID":"")).Concat(new[]{"Confirm selected purchase"}).Concat(Topics()).ToArray():Topics();
        public string XRText=>!IsOpen?"":speaker.displayName+"\n"+reply+"\n> "+XRChoices()[Mathf.Clamp(xrChoice,0,XRChoices().Length-1)]+"\nLeft primary: next | Right primary: choose | Right secondary: end";
        public void UpdateXR()
        {
            var left=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);var right=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out bool next);right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out bool select);right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton,out bool close);
            var choices=XRChoices();if(next&&!xrNext)xrChoice=(xrChoice+1)%choices.Length;
            if(select&&!xrSelect){if(speaker==shopkeeper&&xrChoice<products.Length){selected=xrChoice;Reply(products[selected].description);}else if(speaker==shopkeeper&&xrChoice==products.Length)Purchase(selected);else SubmitTranscript(choices[xrChoice]);}
            if(close&&!xrClose)Close();xrNext=next;xrSelect=select;xrClose=close;
        }
        public bool Owns(PreparednessItem item)=>!products.Any(p=>p.item==item)||state.purchased.Contains(item.id);
        public string Summary=>"DAY BEFORE THE TYPHOON\n"+
            (state.needsDiscussed?"[done] Household needs discussed":"[ ] Speak with your family beside the dining table")+"\n"+
            "Shopping: "+string.Join(", ",products.Where(p=>!Owns(p.item)).Select(p=>p.item.displayName))+"\n"+
            "Household money: P"+state.money+" (scenario prices)\n"+
            (state.meetingAgreed?"[done] Meeting: school reception; backup: school entrance staff desk":"[ ] Agree on primary and backup meeting points")+"\n"+
            (state.assistanceAgreed?"[done] Family member will accompany Lola; you carry the go-bag":"[ ] Agree who assists Lola")+"\n"+
            (state.contactAgreed?"[done] Emergency contact written on the family card":"[ ] Agree on an emergency contact")+"\nPurchases still need to be carried, checked and stored or packed.";
        public PreparationChapterSave Capture()=>JsonUtility.FromJson<PreparationChapterSave>(JsonUtility.ToJson(state));
        public void Restore(PreparationChapterSave saved)
        {
            Close();state=saved==null?new PreparationChapterSave():JsonUtility.FromJson<PreparationChapterSave>(JsonUtility.ToJson(saved));
            state.purchased=state.purchased??Array.Empty<string>();
            // Old checkpoints keep their existing supplies, instead of taking them away.
            if(saved==null&&session.phase!=PrepPhase.Peaceful)state.purchased=products.Select(p=>p.item.id).ToArray();
            foreach(var p in products)if(!Owns(p.item)){p.item.transform.position=p.shelfPosition;p.item.storageDoor=null;p.item.storedIn="store";p.item.SetPhysics(false);}
        }
        public bool TryOpen(PreparednessItem target)
        {
            if(target!=family&&target!=shopkeeper&&target!=barangay&&!products.Any(p=>p.item==target))return false;
            if(!session.CanPrepare||!session.advisoryRead){session.Say("Listen to the weather bulletin first.");return true;}
            if(target!=family&&!session.OutdoorSafe){session.Say("The store and outdoor visits are closed. Follow the evacuation advisory.");return true;}
            session.player.CloseDesktopPanels();speaker=target==family?family:target==barangay?barangay:shopkeeper;
            xrChoice=0;xrNext=xrSelect=xrClose=true;selected=Array.FindIndex(products,p=>p.item==target);draft="";scroll=Vector2.zero;
            Reply(speaker==family?"The typhoon may arrive tomorrow. Let's check our supplies and agree how we will leave together.":speaker==shopkeeper?"Welcome. Ask about supplies or select an item below. Check the price before confirming a purchase.":"Monitor the training advisories. Ask about our route, assistance, or the latest update.");
            session.player.RefreshPanelCursor();return true;
        }
        public void HideUnpurchased(){foreach(var p in products)if(!Owns(p.item))p.item.gameObject.SetActive(false);}
        public void Close(){speaker=null;selected=-1;draft="";}
        void Reply(string text){reply=text;session.Say((speaker?speaker.displayName:"Household")+": "+text);ResponseReceived?.Invoke(speaker?speaker.id:"",text);}
        public void SubmitTranscript(string transcript)
        {
            if(!IsOpen||!session.CanPrepare||string.IsNullOrWhiteSpace(transcript))return;
            string t=transcript.Trim().ToLowerInvariant();
            if(t.Length>500){Reply("Please ask one short question at a time.");return;}
            if(speaker==shopkeeper)
            {
                var matches=products.Select((p,i)=>new {p,i}).Where(x=>t.Contains(x.p.item.kind==PrepKind.Batteries?"batter":x.p.item.kind==PrepKind.Food?"food":"water")).ToArray();
                if(matches.Length==1){selected=matches[0].i;var p=products[selected];Reply(Owns(p.item)?"You already bought that. Collect it from the counter and bring it home.":p.item.displayName+" costs P"+p.price+". "+p.description+" Select Confirm purchase if this is what you want.");}
                else if(t.Contains("money")||t.Contains("budget")||t.Contains("price"))Reply("You have P"+state.money+". Prices are listed below. Only confirmed purchases spend money.");
                else if(t.Contains("alternative")||t.Contains("rice")||t.Contains("cook"))Reply("Choose the ready-to-eat food pack if cooking is unavailable. Our food pack includes easy-open cans.");
                else Reply("We have drinking water, ready-to-eat food and matching batteries. Which would you like to check?");
            }
            else if(speaker==family)
            {
                if(t.Contains("need")||t.Contains("suppl")||t.Contains("missing")||t.Contains("buy")||t.Contains("budget")){state.needsDiscussed=true;Reply("We have P500 for preparation. Check our kitchen and equipment. The shopping list shows missing food, portable water and spare batteries. Leave some money for emergencies.");}
                else if(t.Contains("meet")||t.Contains("school")){state.meetingAgreed=true;Reply("Agreed: school reception. If separated there, ask staff at the school entrance desk. We follow the designated route and any updated instructions.");}
                else if(t.Contains("lola")||t.Contains("assist")||t.Contains("help")){state.assistanceAgreed=true;Reply("I will accompany Lola. You carry the go-bag. We leave together and ask the barangay team for assistance if needed.");}
                else if(t.Contains("contact")||t.Contains("phone")){state.contactAgreed=true;Reply("Our fictional emergency contact is on the family card. Keep the paper copy with your protected documents and charge the phone.");}
                else if(t.Contains("ready")||t.Contains("pack"))Reply(Summary+"\n"+session.PlanSummary);
                else Reply("Let's discuss missing supplies, our meeting place, help for Lola, or our emergency contact. What would you like to arrange?");
            }
            else
            {
                if(t.Contains("route")||t.Contains("school"))Reply("Use the marked inland school route. Review the primary and backup route cards at home. Recheck the advisory before departure.");
                else if(t.Contains("help")||t.Contains("assist"))Reply("Tell the reception team who needs assistance. Keep your household together; a conversation does not confirm that another household has arrived.");
                else Reply(session.evacuationOrdered?"The training evacuation advisory is active. Leave now. Do not delay for purchases or chores.":"This is the day before the typhoon. Prepare now and monitor the TV for updated training advisories. You may leave early.");
            }
            draft="";
        }
        public bool Purchase(int index)
        {
            if(speaker!=shopkeeper||!session.CanPrepare||!session.OutdoorSafe||index<0||index>=products.Length)return false;
            var p=products[index];
            if(Owns(p.item)){Reply("That item is already paid for.");return false;}
            if(state.money<p.price){Reply("There isn't enough money. Review the other supplies before choosing.");return false;}
            state.money-=p.price;state.purchased=state.purchased.Concat(new[]{p.item.id}).ToArray();p.item.storedIn="";
            Reply("Paid P"+p.price+". Collect "+p.item.displayName+" from the counter. Bring it home to check and pack or store it.");return true;
        }
        public void Draw(SibucaoHudLayout layout)
        {
            var r=layout.Modal;SibucaoDesktopHud.Panel(r);GUILayout.BeginArea(new Rect(r.x+22,r.y+18,r.width-44,r.height-36));
            GUILayout.Label("DAY BEFORE · "+speaker.displayName,SibucaoDesktopHud.Heading);
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label(reply,SibucaoDesktopHud.Text);GUILayout.Space(12);
            if(speaker==shopkeeper)
            {
                GUILayout.Label("Household money: P"+state.money+" · Fictional scenario prices",SibucaoDesktopHud.Small);
                for(int i=0;i<products.Length;i++){var p=products[i];if(GUILayout.Button((selected==i?"> ":"")+p.item.displayName+" · P"+p.price+(Owns(p.item)?" · PAID":""),SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40))){selected=i;Reply(p.description);}}
                if(selected>=0&&!Owns(products[selected].item)&&GUILayout.Button("Confirm purchase · P"+products[selected].price,SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(44)))Purchase(selected);
            }
            var topics=Topics();
            foreach(string topic in topics)if(GUILayout.Button(topic,SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))SubmitTranscript(topic);
            GUILayout.Label("Say something",SibucaoDesktopHud.Small);draft=GUILayout.TextField(draft,500,GUILayout.MinHeight(40));
            if(GUILayout.Button("Send",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(40)))SubmitTranscript(draft);
            GUILayout.EndScrollView();
            if(GUILayout.Button("End conversation",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(42))){Close();session.player.RefreshPanelCursor();}
            GUILayout.EndArea();
        }
    }
}
