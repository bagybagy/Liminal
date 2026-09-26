using UnityEngine;

namespace Liminal
{
    public sealed class Flight : MonoBehaviour
    {
        public Camera View { get; private set; }
        public Vector3 Emitter => View.transform.position + View.transform.forward*9 - View.transform.up*2.8f;
        public bool ReducedMotion;
        Vector3 drift, velocity;
        float yaw, pitch, lastOrbit;
        GameObject avatar;
        public void Initialize(ParticleWorld world, Camera camera)
        {
            View=camera;
            var p = new PointCloud();
            var random = new System.Random(41);
            for(int i=0;i<1700;i++) {
                float u=(float)random.NextDouble(), side=i%2==0?1:-1;
                float x=side*u*1.1f, y=Mathf.Sin(u*3.8f)*0.25f, z=-u*u*0.6f;
                if(i%4==0) { x*=0.16f; y=u*0.7f-0.25f; z=0; }
                p.Add(new Vector3(x,y,z),0.013f,new Color(0.8f,0.93f,1)*0.9f,u);
            }
            avatar=PointCloud.Place("Traveler",p.Build("Traveler",10),world.NodeMaterial,transform);
            ResetFlight();
        }
        public void ResetFlight() { drift=Vector3.zero; velocity=Vector3.zero; yaw=0; pitch=0; }
        public void Tick(float song,float dt,bool controls)
        {
            if(controls) {
                float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
                float y=(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0);
                float z=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
                drift+=new Vector3(x,y,z)*dt*(Input.GetKey(KeyCode.LeftShift)?12:7);
                drift.x=Mathf.Clamp(drift.x,-16,16); drift.y=Mathf.Clamp(drift.y,-7,12); drift.z=Mathf.Clamp(drift.z,-7,15);
                if(Input.GetMouseButton(1)) {
                    yaw+=Input.GetAxisRaw("Mouse X")*2.1f;
                    pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.7f,-48,48);
                    lastOrbit=song;
                } else if(song-lastOrbit>2.5f) {
                    yaw=Mathf.LerpAngle(yaw,0,dt*0.7f); pitch=Mathf.Lerp(pitch,0,dt*0.7f);
                }
            }
            Vector3 position=new Vector3(Mathf.Sin(song*0.08f)*2,3+Mathf.Sin(song*0.13f),-16)+drift;
            View.transform.position=Vector3.SmoothDamp(View.transform.position,position,ref velocity,0.3f,100,dt);
            Quaternion baseRotation=Quaternion.LookRotation(new Vector3(0,0,37)-View.transform.position);
            View.transform.rotation=baseRotation*Quaternion.Euler(pitch,yaw,ReducedMotion?0:Mathf.Sin(song*0.11f)*0.5f);
            View.fieldOfView=Mathf.Lerp(View.fieldOfView,54+(!ReducedMotion?Score.Pulse(song)*0.3f:0),dt*4);
            avatar.transform.position=Emitter;
            avatar.transform.rotation=View.transform.rotation*Quaternion.Euler(0,0,-drift.x*1.1f);
        }
    }
}
