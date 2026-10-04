using UnityEngine;

namespace Liminal
{
    public sealed class Hud : MonoBehaviour
    {
        public Experience Experience;
        public int VisibleCreditRows { get; private set; }
        bool pointStudyControlsExpanded;
        GUIStyle small, regular, title, number, button, creditBody, creditHeading, comparisonStatus;
        Texture2D pixel;
        static readonly Color White=new(0.89f,0.96f,0.96f), Muted=new(0.43f,0.60f,0.65f), Cyan=new(0.4f,1,0.87f), Gold=new(1,0.7f,0.32f);
        void Setup()
        {
            if(pixel) return;
            pixel=Texture2D.whiteTexture;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            small=new GUIStyle {font=font,fontSize=12,normal={textColor=Muted}};
            comparisonStatus=new GUIStyle(small) {fontSize=10};
            regular=new GUIStyle(small) {fontSize=16,normal={textColor=White}};
            title=new GUIStyle(regular) {fontSize=34};
            number=new GUIStyle(regular) {fontSize=23};
            button=new GUIStyle(GUI.skin.button) {font=font,fontSize=15};
            creditBody=new GUIStyle(small) {
                fontSize=18,alignment=TextAnchor.MiddleCenter,wordWrap=true,richText=false,
                padding=new RectOffset(2,2,2,2)
            };
            creditHeading=new GUIStyle(creditBody) {fontSize=20,fontStyle=FontStyle.Bold};
        }
        public static Rect CreditsViewport(float width,float height)
        {
            const float rightMargin=28f, top=88f, bottom=78f;
            float columnWidth=Mathf.Min(Mathf.Clamp(width*0.28f,220f,360f),Mathf.Max(0,width-rightMargin*2));
            return new Rect(width-rightMargin-columnWidth,top,columnWidth,Mathf.Max(0,height-top-bottom));
        }
        public static bool ShouldShowCredits(ParticleCredits credits,bool paused,bool vr)
        {
            return !vr&&!paused&&credits!=null&&credits.Active&&!credits.Completed;
        }
        public void OnGUI()
        {
            VisibleCreditRows=0;
            if(!Experience || !Experience.Ready) return;
            bool vr=(Experience.Vr && Experience.Vr.Enabled) || (Experience.Flight && Experience.Flight.VrEnabled);
            if(vr) return;
            Setup();
            var e=Experience.Combat; var music=Experience.Music;
            float w=Screen.width,h=Screen.height,t=(float)music.Time;
            Text(new Rect(32,25,220,32),"L I M I N A L",number,White);
            string roomName=Experience.CavernMode?CaveLayout.Rooms[Experience.CurrentRoom].Name:"ABYSSAL CHOIR";
            Text(new Rect(33,59,300,20),(Experience.CavernMode?Experience.CurrentRoom+1:1).ToString("D2")+"   /   "+roomName,small,Muted);
            Text(new Rect(w-246,27,214,32),e.Points.ToString("D7"),number,White,TextAnchor.UpperRight);
            Text(new Rect(w-246,59,214,20),"RESONANCE  " + (1+Mathf.Min(7,e.Combo/8)).ToString("D2"),small,Cyan,TextAnchor.UpperRight);
            if(!music.Paused && !e.Ended) {
                float bw=Mathf.Min(340,w*0.28f);
                string status=Score.SectionName(e.Section);
                float progressValue=1-e.BossDamage/(float)e.BossDamageGoal;
                if(Experience.CavernMode) {
                    int room=Experience.CurrentRoom;
                    status=room==0?"GARDEN  "+Experience.Marine.CompletedJellies.ToString("D2")+" / "+Experience.Marine.JellyCount.ToString("D2"):
                        room==1?(e.SerpentComplete?"SERPENT RELEASED":"RESONANCE  "+e.BossDamage.ToString("D2")+" / "+e.BossDamageGoal):
                        (Experience.Marine.WhaleReleased?"HORIZON RELEASED":Experience.Marine.WhaleRegenerating?
                            "HORIZON REASSEMBLING":"HORIZON  "+Experience.Marine.WhaleResonance+" / "+MarineLife.WhaleDamageGoal);
                    progressValue=room==0?Experience.Marine.CompletedJellies/(float)Mathf.Max(1,Experience.Marine.JellyCount):
                        room==1?e.BossDamage/(float)e.BossDamageGoal:Experience.Marine.WhaleResonance/(float)MarineLife.WhaleDamageGoal;
                    if(room==3) {status=Experience.Hermits.Status;progressValue=Experience.Hermits.Progress;}
                    if(room==4) {status=Experience.Submarines.Status;progressValue=Experience.Submarines.Progress;}
                }
                Text(new Rect((w-bw)*0.5f,28,bw,24),status,small,Muted,TextAnchor.MiddleCenter);
                Fill(new Rect((w-bw)*0.5f,56,bw,1),new Color(0.15f,0.25f,0.27f));
                Fill(new Rect((w-bw)*0.5f,56,bw*Mathf.Clamp01(progressValue),1),Experience.CavernMode?CaveLayout.Rooms[Experience.CurrentRoom].Accent:e.Section>=3?Gold:Cyan);
                DrawTargets();
                DrawBattleReadouts(w,h,t);
                if(Experience.CavernMode) DrawPassageReadout(w,h);
                Vector2 aim=Experience.Flight.AimScreenPosition;
                aim.y=h-aim.y;
                Color cursor=e.Locks.Count>0?Gold:White;
                Ring(aim,e.LockRadiusPixels,new Color(White.r,White.g,White.b,0.16f),96);
                Ring(aim,13+e.Locks.Count*0.55f,cursor*0.8f,32);
                Line(aim+Vector2.left*23,aim+Vector2.left*17,cursor,1);
                Line(aim+Vector2.right*17,aim+Vector2.right*23,cursor,1);
                Line(aim+Vector2.up*17,aim+Vector2.up*23,cursor,1);
                Line(aim+Vector2.down*17,aim+Vector2.down*23,cursor,1);
                if(e.Locks.Count>0) Text(new Rect(aim.x+26,aim.y-9,70,22),e.Locks.Count.ToString("D2"),regular,Gold);
                for(int i=0;i<8;i++) Fill(new Rect(w*0.5f-62+i*16,h-43,11,3),i<e.Locks.Count?Gold:new Color(0.2f,0.32f,0.35f,0.8f));
            }
            Text(new Rect(32,h-66,150,18),"INTEGRITY",small,Muted);
            for(int i=0;i<8;i++) Fill(new Rect(33+i*17,h-39,12,3),i<e.Life?Cyan:new Color(0.28f,0.22f,0.24f));
            Text(new Rect(w-205,h-66,172,18),"OVERDRIVE",small,e.Charge>=1?Gold:Muted,TextAnchor.UpperRight);
            Fill(new Rect(w-174,h-39,140,2),new Color(0.2f,0.27f,0.29f));
            Fill(new Rect(w-174,h-39,140*e.Charge,2),Gold);
            float progress=Experience.CavernMode?Experience.RoomsVisited/(float)CaveLayout.Rooms.Length:Mathf.Clamp01(t/(float)Score.Duration);
            Fill(new Rect(0,h-2,w*progress,2),Cyan*0.65f);
            if(t<7 && !music.Paused) {
                float a=Mathf.Min(t/2,1)*Mathf.Clamp01((7-t)/2);
                Text(new Rect(20,h*0.30f,w-40,50),Experience.CavernMode?"LANTERN GROTTO":"ABYSSAL CHOIR",title,new Color(0.85f,0.98f,0.94f,a),TextAnchor.MiddleCenter);
                Text(new Rect(20,h*0.30f+52,w-40,24),"T I D A L   M E M O R Y",small,new Color(0.46f,0.7f,0.74f,a),TextAnchor.MiddleCenter);
            }
            if(e.Combo>=2 && t-e.LastHitTime<1.2f && !e.Ended) {
                float a=Mathf.Clamp01(1.2f-(t-e.LastHitTime));
                Text(new Rect(w*0.5f-80,h-97,160,22),e.Combo.ToString("D3")+"  CHAIN",small,new Color(Cyan.r,Cyan.g,Cyan.b,a),TextAnchor.MiddleCenter);
            }
            if(e.DamageFlash>0 && !Experience.ReducedMotion) {
                Fill(new Rect(0,0,w,5),new Color(1,0.18f,0.07f,e.DamageFlash));
                Fill(new Rect(0,h-5,w,5),new Color(1,0.18f,0.07f,e.DamageFlash));
            }
            if(Experience.CavernMode && Experience.WhaleAwakenedAt>=0 && t-Experience.WhaleAwakenedAt<7 && !music.Paused)
                Text(new Rect(20,h*.74f,w-40,46),"HORIZON RELEASED",title,White,TextAnchor.MiddleCenter);
            if(e.Ended && (music.Paused || t-e.EndTime>4)) Results(w,h);
            else if(music.Paused) PausePanel(w,h);
            var finale=Experience.Finale;
            var credits=finale?finale.Credits:null;
            if(ShouldShowCredits(credits,music.Paused,vr)) DrawCredits(credits,w,h);
        }
        void DrawCredits(ParticleCredits credits,float width,float height)
        {
            Rect viewport=CreditsViewport(width,height);
            if(viewport.width<=0||viewport.height<=0) return;
            GUI.BeginGroup(viewport);
            float fadeHeight=Mathf.Min(32f,viewport.height*0.22f);
            float previousBottom=float.NegativeInfinity;
            float lifetime=ParticleCredits.GatherDuration+ParticleCredits.ScrollDuration;
            for(int i=0;i<ParticleCredits.LineCount;i++) {
                float age=credits.Elapsed-i*ParticleCredits.LineInterval;
                if(age<0||age>=lifetime) continue;
                string value=ParticleCredits.GetLineText(i);
                GUIStyle style=ParticleCredits.IsHeading(i)?creditHeading:creditBody;
                GUIContent content=new(value);
                float rowHeight=Mathf.Max(style.fontSize+6,style.CalcHeight(content,viewport.width)+2);
                float scroll=Mathf.Clamp01((age-ParticleCredits.GatherDuration)/ParticleCredits.ScrollDuration);
                float rowY=viewport.height-rowHeight-scroll*(viewport.height+rowHeight);
                if(!float.IsNegativeInfinity(previousBottom)) rowY=Mathf.Max(rowY,previousBottom+6);
                previousBottom=rowY+rowHeight;
                float centerY=rowY+rowHeight*0.5f;
                float edgeAlpha=Mathf.Min(
                    Mathf.SmoothStep(0,1,centerY/Mathf.Max(1,fadeHeight)),
                    Mathf.SmoothStep(0,1,(viewport.height-centerY)/Mathf.Max(1,fadeHeight)));
                float gatherAlpha=Mathf.Clamp01(age/ParticleCredits.GatherDuration);
                Color color=ParticleCredits.GetLineColor(i);
                color.a*=gatherAlpha*edgeAlpha;
                style.normal.textColor=color;
                if(color.a>0.005f) {
                    GUI.Label(new Rect(0,rowY,viewport.width,rowHeight),content,style);
                    if(Event.current.type==EventType.Repaint) VisibleCreditRows++;
                }
            }
            GUI.EndGroup();
        }
        void DrawTargets()
        {
            var e=Experience.Combat;
            foreach(var target in e.Targets) {
                if(!target.Available && !e.Locks.Contains(target)) continue;
                Vector3 projected=Experience.Flight.View.WorldToScreenPoint(target.position);
                if(projected.z<=0 || projected.x<12 || projected.x>Screen.width-12 || projected.y<80 || projected.y>Screen.height-85) continue;
                Vector2 center=new(projected.x,Screen.height-projected.y);
                int index=e.Locks.IndexOf(target);
                bool canAcquire=e.CanAcquire(target);
                if(target.kind==TargetKind.Environment && index<0 && !canAcquire) continue;
                if(target.kind==TargetKind.Environment && !target.isWhale && index<0 &&
                    Vector2.Distance(projected,Experience.Flight.AimScreenPosition)>e.LockRadiusPixels*1.5f) continue;
                bool distantOrgan=target.kind==TargetKind.Organ && index<0 && !canAcquire;
                float radius=distantOrgan?11:target.isWhale?9:target.kind==TargetKind.Organ?15:20;
                Color col=index>=0?Gold:distantOrgan?new Color(0.54f,0.68f,0.69f,0.15f):target.kind==TargetKind.Threat?new Color(1,0.34f,0.20f,0.88f):new Color(Cyan.r,Cyan.g,Cyan.b,0.42f);
                if(index>=0) {
                    Ring(center,radius+5,col,36);
                    int count=e.LockCount(target);
                    if(count>1) Ring(center,radius+10,col*.55f,36);
                    string label=count>1?"x"+count:(index+1).ToString("D2");
                    Text(new Rect(center.x+radius+9,center.y-8,40,18),label,small,Gold);
                    if(index>0) {
                        Vector3 prev=Experience.Flight.View.WorldToScreenPoint(e.Locks[index-1].position);
                        if(prev.z>0) Line(center,new Vector2(prev.x,Screen.height-prev.y),new Color(1,0.72f,0.3f,0.19f),1);
                    }
                } else {
                    float corner=distantOrgan?3:5;
                    for(int i=0;i<4;i++) {
                        float a=Mathf.PI*(i*0.5f+0.25f);
                        Vector2 q=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                        Line(q,q+new Vector2(-Mathf.Sign(Mathf.Cos(a))*corner,0),col,1);
                        Line(q,q+new Vector2(0,-Mathf.Sign(Mathf.Sin(a))*corner),col,1);
                    }
                }
            }
        }
        void DrawBattleReadouts(float w,float h,float song)
        {
            var flight=Experience.Flight;
            Vector3 focus=Anatomy.Focus(song);
            string destination="SERPENT";
            if(Experience.CavernMode) {
                int room=Experience.CurrentRoom;
                if(room==2 && !Experience.Marine.WhaleReleased) {focus=Experience.Marine.WhalePosition;destination="HORIZON";}
                else if(room==3 && !Experience.Hermits.Complete) {focus=Experience.Hermits.BossPosition;destination="TIDAL SHELLS";}
                else if(room==4) {focus=Experience.Submarines.Focus;destination="SCARLET ENGINE";}
                else if(room==0 || room>=2 || Experience.Combat.SerpentComplete) {focus=CaveLayout.ForwardWaypoint(flight.Position,room);destination="DESCENT";}
            }
            float distance=Vector3.Distance(flight.Position,focus);
            Text(new Rect(33,h-98,190,20),flight.Speed.ToString("F0")+" M/S",small,Muted);
            Text(new Rect(w*0.5f-120,65,240,22),destination+"  "+distance.ToString("F0")+" M",small,
                distance<=Encounter.LockRange?Cyan:Muted,TextAnchor.MiddleCenter);
            Vector3 relative=flight.View.transform.InverseTransformPoint(focus);
            Vector3 viewport=flight.View.WorldToViewportPoint(focus);
            if(relative.z>0 && viewport.x>.04f && viewport.x<.96f && viewport.y>.1f && viewport.y<.89f) return;
            Vector2 direction=new(relative.x,-relative.y);
            if(direction.sqrMagnitude<0.001f) direction=Vector2.down;
            direction.Normalize();
            Vector2 center=new(w*0.5f,h*0.5f);
            Vector2 extents=new(Mathf.Max(60,w*0.5f-64),Mathf.Max(60,h*0.5f-120));
            float scale=Mathf.Min(extents.x/Mathf.Max(.001f,Mathf.Abs(direction.x)),extents.y/Mathf.Max(.001f,Mathf.Abs(direction.y)));
            Vector2 tip=center+direction*scale;
            Vector2 cross=new(-direction.y,direction.x);
            Line(tip,tip-direction*14+cross*7,Gold,2);
            Line(tip,tip-direction*14-cross*7,Gold,2);
            Vector2 label=tip-direction*38;
            Text(new Rect(label.x-60,label.y-10,120,22),relative.z<0?"BEHIND":distance.ToString("F0")+" M",small,Gold,TextAnchor.MiddleCenter);
        }
        void DrawPassageReadout(float w,float h)
        {
            var flight=Experience.Flight;
            int room=Experience.CurrentRoom;
            if(CaveLayout.RoomDistance(room,flight.Position)<1) {
                foreach(int passage in CaveLayout.IncidentPassages(room)) {
                    bool forward=CaveLayout.FromRoom(passage)==room;
                    int destination=CaveLayout.Destination(passage,forward);
                    CaveLayout.GetPortal(passage,forward,out var mouth,out _);
                    var screen=flight.View.WorldToScreenPoint(mouth);
                    if(screen.z<=0 || screen.x<100 || screen.x>w-100 || screen.y<130 || screen.y>h-135) continue;
                    var point=new Vector2(screen.x,h-screen.y);
                    bool cleared=destination==1?Experience.Progress.Has(BossId.Serpent):
                        destination==3?Experience.Progress.Has(BossId.Hermit):
                        destination==4?Experience.Progress.Has(BossId.Submarine):Experience.Progress.Has(BossId.Whale);
                    Color color=cleared?Muted:Cyan;
                    Ring(point,9,color,24);
                    string destinationName=CaveLayout.Rooms[destination].Name;
                    Text(new Rect(point.x-100,point.y+14,200,36),destinationName+"\n"+
                        Vector3.Distance(flight.Position,mouth).ToString("F0")+" M",small,color,TextAnchor.MiddleCenter);
                }
                return;
            }
            if(!CaveLayout.NextPassage(flight.Position,out Vector3 waypoint,out int next)) return;
            float distance=Vector3.Distance(flight.Position,waypoint);
            string name=CaveLayout.Rooms[next].Name;
            Text(new Rect(w*.5f-210,91,420,22),"NEXT  "+(next+1).ToString("D2")+" / "+name+"  "+distance.ToString("F0")+" M",
                small,Cyan,TextAnchor.MiddleCenter);
            Vector3 relative=flight.View.transform.InverseTransformPoint(waypoint);
            Vector3 projected=flight.View.WorldToScreenPoint(waypoint);
            Vector2 tip=new(projected.x,h-projected.y);
            bool onScreen=relative.z>0 && tip.x>115 && tip.x<w-115 && tip.y>135 && tip.y<h-150;
            if(onScreen) {
                float radius=9;
                Line(tip+Vector2.up*radius,tip+Vector2.right*radius,Cyan,2);
                Line(tip+Vector2.right*radius,tip+Vector2.down*radius,Cyan,2);
                Line(tip+Vector2.down*radius,tip+Vector2.left*radius,Cyan,2);
                Line(tip+Vector2.left*radius,tip+Vector2.up*radius,Cyan,2);
                Text(new Rect(tip.x-80,tip.y+14,160,20),"PASSAGE  "+distance.ToString("F0")+" M",small,Cyan,TextAnchor.MiddleCenter);
                return;
            }
            Vector2 direction=new(relative.x,-relative.y);
            if(direction.sqrMagnitude<.001f) direction=Vector2.down;
            direction.Normalize();
            Vector2 center=new(w*.5f,h*.5f),extents=new(Mathf.Max(60,w*.5f-100),Mathf.Max(60,h*.5f-155));
            float scale=Mathf.Min(extents.x/Mathf.Max(.001f,Mathf.Abs(direction.x)),extents.y/Mathf.Max(.001f,Mathf.Abs(direction.y)));
            tip=center+direction*scale;
            Vector2 cross=new(-direction.y,direction.x);
            Line(tip,tip-direction*16+cross*8,Cyan,2);
            Line(tip,tip-direction*16-cross*8,Cyan,2);
            Vector2 label=tip-direction*46;
            Text(new Rect(label.x-80,label.y-10,160,20),relative.z<0?"PASSAGE / BEHIND":"PASSAGE",small,Cyan,TextAnchor.MiddleCenter);
        }
        void PausePanel(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(0.004f,0.009f,0.014f,0.08f));
            Matrix4x4 previous=GUI.matrix;
            var study=Experience.PointStudy;
            bool studyControls=Experience.CavernMode && study;
            bool expanded=studyControls && pointStudyControlsExpanded;
            float contentHeight=studyControls?(expanded?694:574):(Experience.CavernMode?550:494);
            float scale=Mathf.Min(1,w/360,(h-24)/(contentHeight+24));
            GUI.matrix=previous*Matrix4x4.Scale(new Vector3(scale,scale,1));
            w/=scale;h/=scale;
            float x=w*0.5f-150,y=(h-contentHeight)*.5f;
            Fill(new Rect(x-20,y-12,340,contentHeight+24),new Color(0.004f,0.009f,0.014f,0.92f));
            Text(new Rect(x,y,300,42),"PAUSED",title,White,TextAnchor.MiddleCenter);
            if(GUI.Button(new Rect(x,y+58,300,36),"RESUME",button)) Experience.TogglePause();
            Text(new Rect(x,y+113,100,23),"MUSIC",small,Muted);
            float volume=GUI.HorizontalSlider(new Rect(x+104,y+118,196,20),Experience.Music.Volume,0,1);
            Experience.Music.SetVolume(volume);
            var brightness=Experience.Brightness;
            Text(new Rect(x,y+153,140,23),"BRIGHTNESS",small,Muted);
            Text(new Rect(x+140,y+153,160,23),brightness.Offset==0?"ORIGINAL":brightness.Offset.ToString("+0.0;-0.0")+" EV",
                small,Cyan,TextAnchor.UpperRight);
            bool enabled=GUI.enabled;GUI.enabled=enabled && brightness.Available;
            float offset=GUI.HorizontalSlider(new Rect(x,y+185,196,20),brightness.Offset,DisplayBrightness.MinOffset,DisplayBrightness.MaxOffset);
            brightness.SetOffset(offset);
            if(GUI.Button(new Rect(x+208,y+173,92,32),"DEFAULT",button)) brightness.SetOffset(0);
            GUI.enabled=enabled;
            bool motion=GUI.Toggle(new Rect(x,y+230,300,26),Experience.ReducedMotion,"  REDUCED MOTION");
            Experience.SetReducedMotion(motion);
            var particleLook=Experience.ParticleLook;
            bool sharp=GUI.Toggle(new Rect(x,y+263,146,25),!particleLook.IsLegacy,"SHARP QUAD",GUI.skin.button);
            bool legacy=GUI.Toggle(new Rect(x+150,y+263,150,25),particleLook.IsLegacy,"ORIGINAL QUAD",GUI.skin.button);
            if(sharp&&particleLook.IsLegacy) particleLook.SetStyle(false);
            else if(legacy&&!particleLook.IsLegacy) particleLook.SetStyle(true);
            bool sideBySide=GUI.Toggle(new Rect(x,y+290,125,25),ParticleLook.SideBySide,"  SIDE BY SIDE");
            if(sideBySide!=ParticleLook.SideBySide) particleLook.SetSideBySide(sideBySide);
            if(sideBySide)
                Text(new Rect(x+128,y+290,172,25),"SHARP MAIN / ORIGINAL GHOST",comparisonStatus,Cyan,TextAnchor.MiddleCenter);
            float actionShift=Experience.CavernMode?56:0;
            if(Experience.CavernMode) {
                if(GUI.Button(new Rect(x,y+330,194,36),"PARTICLE TUTORIAL",button)) Experience.ReplayTutorial();
                if(GUI.Button(new Rect(x+202,y+330,98,36),"SKIP",button)) Experience.SkipTutorial();
            }
            if(GUI.Button(new Rect(x,y+330+actionShift,300,36),"RESTART",button)) Experience.Restart();
            if(Experience.Vr) {
                bool enabledBefore=GUI.enabled;
                GUI.enabled=enabledBefore&&!Experience.Vr.Starting;
                if(GUI.Button(new Rect(x,y+382+actionShift,300,36),"PC VR",button)) Experience.Vr.RequestEnable();
                GUI.enabled=enabledBefore;
                Text(new Rect(x,y+422+actionShift,300,20),Experience.Vr.Status,small,Muted,TextAnchor.MiddleCenter);
            }
            if(GUI.Button(new Rect(x,y+450+actionShift,300,36),"EXIT",button)) Experience.Quit();
            if(studyControls) {
                expanded=GUI.Toggle(new Rect(x,y+494+actionShift,300,24),pointStudyControlsExpanded,pointStudyControlsExpanded?"  POINT STUDY  -":"  POINT STUDY  +");
                pointStudyControlsExpanded=expanded;
                if(expanded) {
                    bool visible=GUI.Toggle(new Rect(x,y+520+actionShift,300,22),study.Visible,"  VISIBLE");
                    if(visible!=study.Visible) study.SetVisible(visible);
                    bool native=study.Mode==PointStudy.RenderMode.NativePoint;
                    bool chooseNative=GUI.Toggle(new Rect(x,y+544+actionShift,144,24),native,"NATIVE POINT");
                    bool chooseQuad=GUI.Toggle(new Rect(x+150,y+544+actionShift,150,24),!native,"SHARP QUAD");
                    if(chooseNative&&!native) study.SetMode(PointStudy.RenderMode.NativePoint);
                    else if(chooseQuad&&native) study.SetMode(PointStudy.RenderMode.SharpQuad);
                    bool density3=study.DensityMultiplier==3;
                    bool choose3=GUI.Toggle(new Rect(x+150,y+570+actionShift,150,22),density3,"3X DENSITY");
                    bool choose1=GUI.Toggle(new Rect(x,y+570+actionShift,144,22),!density3,"1X DENSITY");
                    if(choose3&&!density3) study.SetDensity(3);
                    else if(choose1&&density3) study.SetDensity(1);
                    Text(new Rect(x,y+594+actionShift,44,20),"GAIN",small,Muted);
                    float gain=GUI.HorizontalSlider(new Rect(x+48,y+596+actionShift,252,18),study.Gain,.25f,12f);
                    if(!Mathf.Approximately(gain,study.Gain)) study.SetGain(gain);
                    Text(new Rect(x,y+614+actionShift,44,20),"FLOW",small,Muted);
                    float flow=GUI.HorizontalSlider(new Rect(x+48,y+616+actionShift,252,18),study.Flow,0f,2f);
                    if(!Mathf.Approximately(flow,study.Flow)) study.SetFlow(flow);
                }
            }
            GUI.matrix=previous;
        }
        void Results(float w,float h)
        {
            if(!Experience.BackgroundProof) {
                Cursor.lockState=CursorLockMode.None;
                Cursor.visible=true;
            }
            Fill(new Rect(0,0,w,h),new Color(0.003f,0.008f,0.012f,0.62f));
            Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Max(.1f,Mathf.Min(1f,Mathf.Min(w/480f,h/470f)));
            GUI.matrix=previous*Matrix4x4.Scale(new Vector3(scale,scale,1));
            w/=scale;h/=scale;
            var e=Experience.Combat;
            float x=w*0.5f-220,y=Mathf.Max(100,h*0.5f-140);
            Text(new Rect(x,y,440,50),e.Won?"RESONANCE COMPLETE":"SIGNAL LOST",title,White,TextAnchor.MiddleCenter);
            Text(new Rect(x,y+69,440,45),e.Points.ToString("D7"),title,Cyan,TextAnchor.MiddleCenter);
            Text(new Rect(x,y+127,440,25),"BEST CHAIN   "+e.BestCombo.ToString("D3")+"     /     HITS   "+e.Hits.ToString("D3"),small,Muted,TextAnchor.MiddleCenter);
            float buttonY=y+191;
            if(Experience.CanRetryRoom) {
                string room=CaveLayout.Rooms[Experience.RoomCheckpointRoom].Name;
                if(GUI.Button(new Rect(w*0.5f-150,buttonY,300,38),"RETRY  "+room,button)) Experience.RetryCurrentRoom();
                buttonY+=48;
            }
            if(GUI.Button(new Rect(w*0.5f-150,buttonY,300,38),"RESTART RUN",button)) Experience.Restart();
            if(GUI.Button(new Rect(w*0.5f-150,buttonY+48,300,34),"EXIT",button)) Experience.Quit();
            GUI.matrix=previous;
        }
        void Text(Rect r,string value,GUIStyle style,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            style.normal.textColor=color;style.alignment=align;GUI.Label(r,value,style);
        }
        void Fill(Rect rect,Color color) { GUI.color=color;GUI.DrawTexture(rect,pixel);GUI.color=Color.white; }
        void Line(Vector2 from,Vector2 to,Color color,float width)
        {
            Matrix4x4 matrix=GUI.matrix;
            Vector2 delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            Fill(new Rect(from.x,from.y-width*0.5f,delta.magnitude,width),color);
            GUI.matrix=matrix;
        }
        void Ring(Vector2 c,float radius,Color color,int segments)
        {
            for(int i=0;i<segments;i++) {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,1);
            }
        }
    }
}
