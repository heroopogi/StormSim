using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>Collision-preserving desktop swimming using the visible flood/sea surface.</summary>
    [RequireComponent(typeof(SibucaoPlayer),typeof(CharacterController))]
    public sealed class SibucaoSwimming:MonoBehaviour
    {
        public float swimSpeed=1.55f,fastSwimSpeed=2.05f,verticalSpeed=1.2f,acceleration=3.2f;
        public bool IsSwimming {get;private set;}
        public bool IsUnderwater {get;private set;}
        public float HeadDepth {get;private set;}
        public Vector3 Velocity {get;private set;}
        public float SurfaceHeight {get;private set;}
        public static bool WantsSwimming(bool wasSwimming,bool wet,float feetDepth)=>wet&&feetDepth>(wasSwimming?.85f:1.25f);
        SibucaoPlayer player;CharacterController controller;FloodSimulationManager flood;
        Transform left,right;Vector3 leftRest,rightRest;Quaternion leftRotation,rightRotation;
        float stroke,blend,landHeight,landCenter,landStep;
        void Awake()
        {
            player=GetComponent<SibucaoPlayer>();controller=GetComponent<CharacterController>();landHeight=controller.height;landCenter=controller.center.y;landStep=controller.stepOffset;
            if(player.hands){left=player.hands.Find("Left hand");right=player.hands.Find("Right hand");if(left){leftRest=left.localPosition;leftRotation=left.localRotation;}if(right){rightRest=right.localPosition;rightRotation=right.localRotation;}}
        }
        public void ResetMotion(){Velocity=Vector3.zero;IsSwimming=IsUnderwater=false;HeadDepth=0;blend=0;RestoreCapsule();RestoreHands();}
        public bool Move(Vector2 input,bool fast,bool ascend,bool dive,float dt)
        {
            var s=SibucaoSession.Instance;if(!flood&&s)flood=s.GetComponent<FloodSimulationManager>();
            if(flood&&flood.IsCatchingUp)return true; // Hold a loaded underwater position until its saved water field is reconstructed.
            bool wet=flood&&flood.TrySurface(transform.position,out _,out _);
            float surface=0,ground=0;if(wet)flood.TrySurface(transform.position,out surface,out ground);
            SurfaceHeight=surface;HeadDepth=wet?surface-player.view.transform.position.y:-1;
            IsUnderwater=wet&&HeadDepth>.04f;
            float feetDepth=wet?surface-transform.position.y:0;
            // Enter at chest depth; keep swimming until there is footing in shallower water.
            bool swim=WantsSwimming(IsSwimming,wet,feetDepth);
            if(!swim)
            {
                if(IsSwimming)RestoreCapsule();IsSwimming=false;Velocity=Vector3.MoveTowards(Velocity,Vector3.zero,dt*5);blend=Mathf.MoveTowards(blend,0,dt*4);Animate(input.magnitude,dt);return false;
            }
            IsSwimming=true;
            // Shorter submerged capsule represents bent legs, retaining the same head position.
            controller.stepOffset=Mathf.Min(.3f,landStep);controller.height=1.25f;controller.center=new Vector3(0,1.025f,0);
            var forward=IsUnderwater?player.view.transform.forward:transform.forward;
            var requested=forward*input.y+transform.right*input.x;
            float speed=(fast?fastSwimSpeed:swimSpeed)*(s?s.state.speedMultiplier:1);
            if(player.grabber)speed*=player.grabber.MovementScale;
            if(player.ActivePhysicalOption)speed=Mathf.Min(speed,player.ActivePhysicalOption.SpeedLimit);
            if(s)speed*=Mathf.Lerp(.65f,1,Mathf.Clamp01(s.state.physical.stamina/100));
            var current=flood.GetFlowVelocity(transform.position);current.y=0;
            var target=Vector3.ClampMagnitude(requested,1)*speed+current;
            bool lowBreath=s&&s.state.physical.submergedSeconds>StormSim.Preparedness.StormSurvivalVitals.BreathSeconds-10;
            if(dive&&!lowBreath)target.y=-verticalSpeed;
            else if(lowBreath&&IsUnderwater)target.y=verticalSpeed;
            else if(ascend)target.y=verticalSpeed;
            else if(!IsUnderwater)
            {
                // Buoyant equilibrium leaves the eyes 16 cm above the waterline.
                float targetFeet=surface-player.view.transform.localPosition.y+.16f;
                target.y=Mathf.Clamp((targetFeet-transform.position.y)*3,-1.1f,1.1f);
            }
            else target.y+=.12f; // Slight positive buoyancy; swimming into the view direction can overcome it.
            if(!dive&&HeadDepth<.03f)target.y=Mathf.Min(target.y,Mathf.Max(0,(surface+.19f-player.view.transform.position.y)*3));
            Velocity=Vector3.MoveTowards(Velocity,target,acceleration*dt);
            var flags=controller.Move(Velocity*dt);
            if((flags&CollisionFlags.Sides)!=0&&input.sqrMagnitude>.01f)TryShoreExit(requested,dt);
            if((flags&CollisionFlags.Above)!=0&&Velocity.y>0)Velocity=new Vector3(Velocity.x,0,Velocity.z);
            if((flags&CollisionFlags.Below)!=0&&Velocity.y<0)Velocity=new Vector3(Velocity.x,0,Velocity.z);
            // Preserve a real ground recovery point, without treating long swims as falling.
            if(controller.isGrounded&&feetDepth<1.1f)player.lastGrounded=transform.position;
            blend=Mathf.MoveTowards(blend,1,dt*3);Animate(input.magnitude+(ascend||dive?.5f:0),dt);return true;
        }
        bool TryShoreExit(Vector3 direction,float dt)
        {
            direction.y=0;if(direction.sqrMagnitude<.01f)return false;
            var candidate=transform.position+direction.normalized*.45f;
            if(!Physics.Raycast(candidate+Vector3.up*1.1f,Vector3.down,out var ground,1.8f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<.7f)return false;
            var feet=ground.point+Vector3.up*.04f;
            if(feet.y-transform.position.y>.55f||flood.GetWaterDepth(feet)>.85f)return false;
            foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*(controller.radius+.06f),feet+Vector3.up*(landHeight-controller.radius),controller.radius,~0,QueryTriggerInteraction.Ignore))
                if(!c.transform.IsChildOf(transform))return false;
            // Step only onto nearby clear ground; the actual transition still uses collision movement.
            controller.Move(Vector3.up*Mathf.Max(0,feet.y-transform.position.y));
            controller.Move(direction.normalized*Mathf.Min(.45f,swimSpeed*dt));
            return true;
        }
        void Animate(float effort,float dt)
        {
            stroke+=dt*(effort>.1f?1.1f:.45f);
            if(player.hands&&blend>0)player.hands.localPosition=Vector3.Lerp(new Vector3(0,-.25f,.44f),new Vector3(0,-.23f,.40f),blend);
            Pose(left,-1,leftRest,leftRotation,effort);Pose(right,1,rightRest,rightRotation,effort);
        }
        void Pose(Transform hand,float side,Vector3 rest,Quaternion rotation,float effort)
        {
            if(!hand)return;float t=stroke*Mathf.PI*2;
            // Symmetric reach, outward catch and inward recovery: a procedural breaststroke/tread.
            float reach=(Mathf.Sin(t)+1)*.5f,sweep=(Mathf.Cos(t)+1)*.5f;
            float power=effort>.1f?1:.38f;
            Vector3 pose=new Vector3(side*(.15f+.27f*sweep*power),-.07f-.09f*Mathf.Sin(t),.10f+.26f*reach*power);
            hand.localPosition=Vector3.Lerp(rest,pose,blend);
            hand.localRotation=Quaternion.Slerp(rotation,Quaternion.Euler(15+35*reach,side*(-25+50*sweep),side*(-18-35*sweep)),blend);
        }
        void RestoreCapsule(){if(controller){controller.height=landHeight;controller.center=new Vector3(0,landCenter,0);controller.stepOffset=landStep;}}
        void RestoreHands(){if(left){left.localPosition=leftRest;left.localRotation=leftRotation;}if(right){right.localPosition=rightRest;right.localRotation=rightRotation;}}
        void OnDisable(){ResetMotion();}
    }
}
