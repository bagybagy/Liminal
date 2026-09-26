using UnityEngine;

namespace Liminal
{
    public sealed class Hud : MonoBehaviour
    {
        public Experience Experience;
        GUIStyle small, regular, title, number, button;
        Texture2D pixel;
        static readonly Color White=new(0.89f,0.96f,0.96f), Muted=new(0.43f,0.60f,0.65f), Cyan=new(0.4f,1,0.87f), Gold=new(1,0.7f,0.32f);
        void Setup()
        {
            if(pixel) return;
            pixel=Texture2D.whiteTexture;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            small=new GUIStyle {font=font,fontSize=12,normal={textColor=Muted}};
            regular=new GUIStyle(small) {fontSize=16,normal={textColor=White}};
            title=new GUIStyle(regular) {fontSize=34};
            number=new GUIStyle(regular) {fontSize=23};
            button=new GUIStyle(GUI.skin.button) {font=font,fontSize=15};
        }
        public void OnGUI()
        {
            if(!Experience || !Experience.Ready) return;
            Setup();
            var e=Experience.Combat; var music=Experience.Music;
            float w=Screen.width,h=Screen.height,t=(float)music.Time;
            Text(new Rect(32,25,220,32),"L I M I N A L",number,White);
            Text(new Rect(33,59,250,20),"01   /   ABYSSAL CHOIR",small,Muted);
            Text(new Rect(w-246,27,214,32),e.Points.ToString("D7"),number,White,TextAnchor.UpperRight);
            Text(new Rect(w-246,59,214,20),"RESONANCE  " + (1+Mathf.Min(7,e.Combo/8)).ToString("D2"),small,Cyan,TextAnchor.UpperRight);
            if(!music.Paused && !e.Ended) {
                float bw=Mathf.Min(340,w*0.28f);
                Text(new Rect((w-bw)*0.5f,28,bw,24),Score.SectionName(e.Section),small,Muted,TextAnchor.MiddleCenter);
                Fill(new Rect((w-bw)*0.5f,56,bw,1),new Color(0.15f,0.25f,0.27f));
                Fill(new Rect((w-bw)*0.5f,56,bw*(1-e.BossDamage/240f),1),e.Section>=3?Gold:Cyan);
                DrawTargets();
                Vector2 aim=Experience.ProofActive?new Vector2(w*0.5f,h*0.5f):new Vector2(Input.mousePosition.x,h-Input.mousePosition.y);
                Color cursor=e.Locks.Count>0?Gold:White;
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
            float progress=Mathf.Clamp01(t/(float)Score.Duration);
            Fill(new Rect(0,h-2,w*progress,2),Cyan*0.65f);
            if(t<7 && !music.Paused) {
                float a=Mathf.Min(t/2,1)*Mathf.Clamp01((7-t)/2);
                Text(new Rect(20,h*0.30f,w-40,50),"ABYSSAL CHOIR",title,new Color(0.85f,0.98f,0.94f,a),TextAnchor.MiddleCenter);
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
            if(music.Paused) PausePanel(w,h);
            else if(e.Ended && t-e.EndTime>4) Results(w,h);
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
                float radius=target.kind==TargetKind.Organ?15:20;
                Color col=index>=0?Gold:target.kind==TargetKind.Threat?new Color(1,0.34f,0.20f,0.88f):new Color(Cyan.r,Cyan.g,Cyan.b,0.42f);
                if(index>=0) {
                    Ring(center,radius+5,col,36);
                    Text(new Rect(center.x+radius+9,center.y-8,30,18),(index+1).ToString("D2"),small,Gold);
                    if(index>0) {
                        Vector3 prev=Experience.Flight.View.WorldToScreenPoint(e.Locks[index-1].position);
                        if(prev.z>0) Line(center,new Vector2(prev.x,Screen.height-prev.y),new Color(1,0.72f,0.3f,0.19f),1);
                    }
                } else {
                    for(int i=0;i<4;i++) {
                        float a=Mathf.PI*(i*0.5f+0.25f);
                        Vector2 q=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                        Line(q,q+new Vector2(-Mathf.Sign(Mathf.Cos(a))*5,0),col,1);
                        Line(q,q+new Vector2(0,-Mathf.Sign(Mathf.Sin(a))*5),col,1);
                    }
                }
            }
        }
        void PausePanel(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(0.004f,0.009f,0.014f,0.88f));
            float x=w*0.5f-150,y=Mathf.Max(80,h*0.5f-172);
            Text(new Rect(x,y,300,42),"PAUSED",title,White,TextAnchor.MiddleCenter);
            if(GUI.Button(new Rect(x,y+70,300,38),"RESUME",button)) Experience.TogglePause();
            Text(new Rect(x,y+133,110,23),"MUSIC",small,Muted);
            float volume=GUI.HorizontalSlider(new Rect(x+104,y+138,196,20),Experience.Music.Volume,0,1);
            Experience.Music.SetVolume(volume);
            bool motion=GUI.Toggle(new Rect(x,y+178,300,26),Experience.ReducedMotion,"  REDUCED MOTION");
            Experience.SetReducedMotion(motion);
            if(GUI.Button(new Rect(x,y+227,300,36),"RESTART",button)) Experience.Restart();
            if(GUI.Button(new Rect(x,y+277,300,36),"EXIT",button)) Experience.Quit();
        }
        void Results(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(0.003f,0.008f,0.012f,0.62f));
            var e=Experience.Combat;
            float x=w*0.5f-220,y=Mathf.Max(100,h*0.5f-140);
            Text(new Rect(x,y,440,50),e.Won?"RESONANCE COMPLETE":"SIGNAL LOST",title,White,TextAnchor.MiddleCenter);
            Text(new Rect(x,y+69,440,45),e.Points.ToString("D7"),title,Cyan,TextAnchor.MiddleCenter);
            Text(new Rect(x,y+127,440,25),"BEST CHAIN   "+e.BestCombo.ToString("D3")+"     /     HITS   "+e.Hits.ToString("D3"),small,Muted,TextAnchor.MiddleCenter);
            if(GUI.Button(new Rect(w*0.5f-150,y+191,300,38),"REENTER",button)) Experience.Restart();
            if(GUI.Button(new Rect(w*0.5f-150,y+243,300,34),"EXIT",button)) Experience.Quit();
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
