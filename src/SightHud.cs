using GHPC.Camera;
using GHPC.Weapons;
using HarmonyLib;
using UnityEngine;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static readonly string[] LockNumbers={"1","2","3","4"};
        private static readonly string[] LockStatuses={"LOCK 0/4","LOCK 1/4","LOCK 2/4","LOCK 3/4","LOCK 4/4"};
        private readonly HudBox[] _lockLabelBoxes=new HudBox[4];
        private readonly int[] _lockLabelIndices=new int[4];
        private TargetLock _selectedLock;
        private static Texture2D _hudCircle;
        private static readonly System.Reflection.FieldInfo ClipFeedTimeField=AccessTools.Field(typeof(AmmoFeed),"_clipFeedTime");

        private static Rect GuiRect(HudBox box) {return new Rect(box.X,box.Y,box.W,box.H);}
        private static void GuiLine(Vector2 a,Vector2 b,float width)
        {
            Matrix4x4 saved=GUI.matrix;
            Vector2 delta=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y-width*.5f,delta.magnitude,width),Texture2D.whiteTexture);
            GUI.matrix=saved;
        }
        private static void GuiBorder(Rect r,float width)
        {
            GUI.DrawTexture(new Rect(r.x,r.y,r.width,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,r.yMax-width,r.width,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,r.y,width,r.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax-width,r.y,width,r.height),Texture2D.whiteTexture);
        }
        private static Texture2D HudCircle()
        {
            if(_hudCircle!=null) return _hudCircle;
            const int size=64;
            Color32[] pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float d=Mathf.Sqrt((x-31.5f)*(x-31.5f)+(y-31.5f)*(y-31.5f));
                byte alpha=(byte)(Mathf.Clamp01(1f-Mathf.Abs(d-29f)/2f)*255);
                pixels[y*size+x]=new Color32(255,255,255,alpha);
            }
            _hudCircle=new Texture2D(size,size,TextureFormat.RGBA32,false);
            _hudCircle.name="LOSAT_HUD_Cached_Four_Tube_Ring";
            _hudCircle.SetPixels32(pixels);_hudCircle.Apply(false,true);
            _hudCircle.filterMode=FilterMode.Bilinear;_hudCircle.wrapMode=TextureWrapMode.Clamp;
            UnityEngine.Object.DontDestroyOnLoad(_hudCircle);return _hudCircle;
        }
        private void DrawTubeHud(OpticsUpgrade sight,Rect view)
        {
            SalvoWeapon state;
            if(!SalvoWeapons.TryGetValue(sight.Weapon,out state)) return;
            Rect panel=GuiRect(SightHudRules.TubePanel(new HudBox(view.x,view.y,view.width,view.height),Screen.height));
            Color old=GUI.color;
            GUI.color=new Color(.015f,.055f,.018f,.65f);
            GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color=new Color(.35f,1f,.18f,1f);GuiBorder(panel,1f);
            for(int i=0;i<4;i++)
            {
                Rect ring=new Rect(panel.x+16+i*46,panel.y+7,29,29);
                GUI.DrawTexture(ring,HudCircle());
                if(state.TubeLoad.Loaded(i))
                {
                    GuiLine(new Vector2(ring.x+8,ring.y+8),new Vector2(ring.xMax-8,ring.yMax-8),1.5f);
                    GuiLine(new Vector2(ring.x+8,ring.yMax-8),new Vector2(ring.xMax-8,ring.y+8),1.5f);
                }
            }
            if(!string.IsNullOrEmpty(state.HudStatus))
                GUI.Label(new Rect(panel.x,panel.yMax+2,panel.width+80,20),state.HudStatus,_lockStyle);
            GUI.color=old;
        }
        private static void RefreshTubeHud(SalvoWeapon state)
        {
            if(!IsPlayerWeapon(state) || Time.unscaledTime<state.NextHudUpdate) return;
            state.NextHudUpdate=Time.unscaledTime+.1f;
            AmmoFeed feed=state.Weapon.Feed;
            if(feed==null) return;
            if(feed.Reloading && ClipFeedTimeField!=null)
            {
                int seconds=TubeLoadState.Remaining(feed.TotalReloadTime,(float)ClipFeedTimeField.GetValue(feed));
                if(!state.HudWasReloading || seconds!=state.HudSeconds)
                {
                    state.HudStatus="RELOAD "+seconds+"s";
                    state.HudSeconds=seconds;
                }
                state.HudWasReloading=true;
            }
            else
            {
                state.HudWasReloading=false;
                state.HudStatus=state.TubeLoad.Mask==0 && feed.ReserveCount==0 ? "EMPTY" : "";
            }
        }
        private void DrawLockHud(Rect view)
        {
            HudBox viewport=new HudBox(view.x,view.y,view.width,view.height);
            int count=0;
            for(int i=0;i<_targetLocks.Count;i++)
            {
                TargetLock target=_targetLocks[i];if(!target.OnScreen) continue;
                Rect r=LockDisplayRect(target);
                _lockLabelBoxes[count]=SightHudRules.Place(new HudBox(r.x,r.y,r.width,r.height),viewport,
                    _lockLabelBoxes,count,20,20,count);
                _lockLabelIndices[count++]=i;
            }
            for(int n=0;n<count;n++)
            {
                int i=_lockLabelIndices[n];TargetLock target=_targetLocks[i];Rect r=LockDisplayRect(target);
                if(!TargetLockRules.FrameOn(target.Reacquiring,Time.unscaledTime)) continue;
                bool selected=target==_selectedLock;
                GUI.color=new Color(.35f,1f,.18f,selected ? 1f : .82f);
                float arm=Mathf.Clamp(Mathf.Min(r.width,r.height)*.22f,5f,14f),line=selected ? 2f : 1f;
                GUI.DrawTexture(new Rect(r.x,r.y,arm,line),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x,r.y,line,arm),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax-arm,r.y,arm,line),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax-line,r.y,line,arm),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x,r.yMax-line,arm,line),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x,r.yMax-arm,line,arm),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax-arm,r.yMax-line,arm,line),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax-line,r.yMax-arm,line,arm),Texture2D.whiteTexture);
                HudBox label=_lockLabelBoxes[n];
                GUI.Label(GuiRect(label),LockNumbers[i],_lockStyle);
                if(Mathf.Abs(label.Y-r.y)>28f)
                    GuiLine(new Vector2(label.X+label.W*.5f,label.Y+label.H*.5f),new Vector2(r.xMax,r.y),1f);
            }
            // Selected target distance replaces its model name; no per-repaint formatting.
            if(_selectedLock!=null && _selectedLock.OnScreen && TargetLockRules.FrameOn(_selectedLock.Reacquiring,Time.unscaledTime))
            {
                Rect r=LockDisplayRect(_selectedLock);
                HudBox caption=SightHudRules.Place(new HudBox(r.x,r.y,r.width,r.height),viewport,
                    _lockLabelBoxes,count,Mathf.Min(90,view.width*.4f),20,4);
                GUI.color=new Color(.35f,1f,.18f,1f);
                GUI.Label(GuiRect(caption),_selectedLock.Caption,_lockStyle);
            }
            GUI.color=new Color(.35f,1f,.18f,1f);
            GUI.Label(new Rect(view.center.x-40,view.y+view.height-42,100,20),LockStatuses[_targetLocks.Count],_lockStyle);
        }
    }
}
