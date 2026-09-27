using UnityEngine;

namespace Liminal
{
    public static class CaveLayout
    {
        public readonly struct Chamber
        {
            public readonly string Name;
            public readonly Vector3 Center, Radius;
            public readonly Color Color, Accent;
            public Chamber(string name, Vector3 center, Vector3 radius, Color color, Color accent)
            { Name=name; Center=center; Radius=radius; Color=color; Accent=accent; }
        }

        public static readonly Chamber[] Rooms = {
            new("LANTERN GROTTO",new Vector3(0,210,-450),new Vector3(150,85,165),
                new Color(.12f,.83f,.76f),new Color(.83f,.98f,.88f)),
            new("SERPENT SANCTUM",new Vector3(0,10,35),new Vector3(215,110,225),
                new Color(.20f,.55f,.66f),new Color(1,.62f,.22f)),
            new("HORIZON WHALE",new Vector3(90,-260,650),new Vector3(310,160,330),
                new Color(.24f,.44f,.86f),new Color(.80f,.88f,1))
        };
        public const float PassageRadius=34f;
        public static readonly Vector3[][] Passages = {
            new[] {new Vector3(0,180,-360),new Vector3(60,130,-285),new Vector3(55,65,-195),new Vector3(0,35,-115)},
            new[] {new Vector3(110,-25,175),new Vector3(190,-95,320),new Vector3(170,-190,430),new Vector3(90,-235,475)}
        };
        public static Vector3 Spawn => Rooms[0].Center+new Vector3(0,-4,-70);
        public static Quaternion SpawnRotation => Quaternion.LookRotation(new Vector3(0,-.05f,1));

        public static float RoomDistance(int room,Vector3 position)
        {
            var c=Rooms[room];
            Vector3 p=position-c.Center;
            return new Vector3(p.x/c.Radius.x,p.y/c.Radius.y,p.z/c.Radius.z).magnitude;
        }

        public static int NearestRoom(Vector3 position)
        {
            int room=0; float best=float.MaxValue;
            for(int i=0;i<Rooms.Length;i++) {
                float d=RoomDistance(i,position);
                if(d<best) {best=d;room=i;}
            }
            return room;
        }

        public static Vector3 ClosestOnSegment(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 delta=b-a;
            return a+delta*Mathf.Clamp01(Vector3.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
        }

        public static bool InPassage(Vector3 point,float padding=0)
        {
            float radius=PassageRadius-padding;
            foreach(var route in Passages)
                for(int i=1;i<route.Length;i++)
                    if((point-ClosestOnSegment(point,route[i-1],route[i])).sqrMagnitude<radius*radius) return true;
            return false;
        }

        public static bool Contains(Vector3 point,float padding=0)
        {
            foreach(var room in Rooms) {
                Vector3 p=point-room.Center,r=room.Radius-Vector3.one*padding;
                if(new Vector3(p.x/r.x,p.y/r.y,p.z/r.z).sqrMagnitude<=1) return true;
            }
            return InPassage(point,padding);
        }

        public static bool LineOfSight(Vector3 from,Vector3 to)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/8f);
            for(int i=1;i<steps;i++) if(!Contains(Vector3.Lerp(from,to,i/(float)steps))) return false;
            return true;
        }

        // The same volumes define rendered openings and swimmer boundaries.
        public static Vector3 Constrain(Vector3 desired,ref Vector3 velocity,float padding=3f)
        {
            padding=Mathf.Clamp(padding,0,PassageRadius-1);
            if(Contains(desired,padding)) return desired;
            Vector3 nearest=desired; float best=float.MaxValue;
            foreach(var room in Rooms) {
                Vector3 r=room.Radius-Vector3.one*padding,d=desired-room.Center;
                float scale=new Vector3(d.x/r.x,d.y/r.y,d.z/r.z).magnitude;
                Vector3 point=room.Center+d/Mathf.Max(1,scale);
                float distance=(desired-point).sqrMagnitude;
                if(distance<best) {best=distance;nearest=point;}
            }
            foreach(var route in Passages) for(int i=1;i<route.Length;i++) {
                Vector3 c=ClosestOnSegment(desired,route[i-1],route[i]);
                Vector3 point=c+Vector3.ClampMagnitude(desired-c,PassageRadius-padding);
                float distance=(desired-point).sqrMagnitude;
                if(distance<best) {best=distance;nearest=point;}
            }
            Vector3 outward=(desired-nearest).normalized;
            float speed=Vector3.Dot(velocity,outward);
            if(speed>0) velocity-=outward*speed;
            return nearest;
        }

        public static Vector3 ForwardWaypoint(Vector3 position,int room)
        {
            if(room>=Passages.Length) return Rooms[2].Center;
            var route=Passages[room];
            int closest=0;float best=float.MaxValue;
            for(int i=0;i<route.Length;i++) {
                float distance=Vector3.Distance(position,route[i]);
                if(distance<best) {best=distance;closest=i;}
            }
            return route[Mathf.Min(route.Length-1,closest+(best<45?1:0))];
        }
    }
}
