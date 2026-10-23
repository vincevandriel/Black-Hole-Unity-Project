using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>Isolated M3 reference. Launch with --reference-lab; never attached to SXS gas.</summary>
    public sealed class ReferenceFlowLab : MonoBehaviour
    {
        private ReferenceFlowCache cache;
        private ReferenceFlowSnapshot snapshot;
        private Texture3D lowerTexture, upperTexture;
        private Material volumeMaterial, tracerMaterial;
        private Mesh tracerMesh;
        private MeshRenderer volumeRenderer, tracerRenderer;
        private ReferenceFlowTracer[] tracers;
        private Vector3[] vertices;
        private int uploadedLower = -1, uploadedUpper = -1;
        private Text status, detail, modeTitle, opticsNote;
        private Slider timeline;
        private Font font;
        private Texture2D opticalTexture;
        private Color32[] opticalPixels;
        private RawImage opticalImage;
        private int opticalRow;
        private Vector3 imageOrigin, imageForward, imageRight, imageUp;
        private float imageAspect, imageTangent;
        private int opticalUnresolved;
        private const int ImageWidth = 192, ImageHeight = 108;
        private readonly Color accent = new Color(.34f,.85f,.91f);
        private readonly Color muted = new Color(.67f,.74f,.83f);
        public Camera View { get; private set; }
        public double TimeTg { get; private set; }
        public bool Playing { get; private set; }
        public double PlaybackRate { get; private set; } = 64;
        public int TracerCount => tracers?.Length ?? 0;
        public double TracerTotalWeight { get { double sum=0; if(tracers!=null) foreach(var t in tracers) sum+=t.WeightRelativeMass; return sum; } }
        public int CacheFrameCount => cache?.ResidentFrameCount ?? 0;
        public long CacheBytes => cache?.AllocatedFieldBytes ?? 0;
        public int CacheGenerationCount => cache?.GeneratedFrameCount ?? 0;
        public int TextureUploadCount { get; private set; }
        public bool TracersVisible { get; private set; }
        public bool OpticalMode { get; private set; }
        public bool OpticalReady { get; private set; }
        public int OpticalRevision { get; private set; }
        public float OpacityScale { get; private set; } = 1;
        public bool EmulatedInputEnabled { get; set; } = true;
        public string StatusText => status != null ? status.text : "";
        public Material VolumeMaterial => volumeMaterial;

        public ulong FieldFingerprint
        {
            get
            {
                ulong hash=14695981039346656037UL;
                for(int i=0;i<snapshot.Lower.PackedRgba.Length;i++)
                {
                    float value=(float)(snapshot.Lower.PackedRgba[i]*(1-snapshot.Blend)+snapshot.Upper.PackedRgba[i]*snapshot.Blend);
                    hash=unchecked((hash^(uint)BitConverter.SingleToInt32Bits(value))*1099511628211UL);
                }
                return hash;
            }
        }
        public ulong TracerFingerprint
        {
            get
            {
                ulong h=14695981039346656037UL;
                if(vertices!=null) for(int i=0;i<vertices.Length;i+=4)
                {
                    h=unchecked((h^(uint)BitConverter.SingleToInt32Bits(vertices[i].x))*1099511628211UL);
                    h=unchecked((h^(uint)BitConverter.SingleToInt32Bits(vertices[i].y))*1099511628211UL);
                    h=unchecked((h^(uint)BitConverter.SingleToInt32Bits(vertices[i].z))*1099511628211UL);
                }
                return h;
            }
        }

        private void Awake()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            View=Camera.main;
            if(View==null) { var go=new GameObject("Reference camera");go.tag="MainCamera";View=go.AddComponent<Camera>(); }
            View.clearFlags=CameraClearFlags.SolidColor;View.backgroundColor=new Color(.007f,.013f,.025f);
            View.nearClipPlane=.03f;View.farClipPlane=150;View.fieldOfView=50;View.useOcclusionCulling=false;
            if(!SystemInfo.supports3DTextures || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
                throw new NotSupportedException("The reference lab requires 3D RGBAFloat textures.");
            cache=new ReferenceFlowCache(new ReferenceFlowModel(),32);
            lowerTexture=NewFieldTexture();upperTexture=NewFieldTexture();
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="Assumed 3D flow / coordinate inspection";
            box.transform.SetParent(transform);box.transform.localScale=new Vector3(40,12,40);Destroy(box.GetComponent<Collider>());
            volumeMaterial=new Material(Resources.Load<Shader>("DTET/ReferenceVolume"));
            volumeMaterial.SetTexture("_Lower",lowerTexture);volumeMaterial.SetTexture("_Upper",upperTexture);
            volumeMaterial.SetFloat("_Extinction",(float)ReferenceFlowModel.GrayExtinctionPerRg);
            volumeMaterial.SetFloat("_Exposure",3);volumeMaterial.SetFloat("_Steps",192);
            volumeRenderer=box.GetComponent<MeshRenderer>();volumeRenderer.sharedMaterial=volumeMaterial;
            tracerMaterial=new Material(Resources.Load<Shader>("DTET/ReferenceTracer"));
            var points=new GameObject("Weighted flow tracers",typeof(MeshFilter),typeof(MeshRenderer));points.transform.SetParent(transform);
            tracerMesh=new Mesh {name="Bounded tracer billboards"};tracerMesh.MarkDynamic();
            points.GetComponent<MeshFilter>().sharedMesh=tracerMesh;
            tracerRenderer=points.GetComponent<MeshRenderer>();tracerRenderer.sharedMaterial=tracerMaterial;
            SetTracerCount(1024);SetTracers(false);
            BuildInterface();SetViewPreset(0);Seek(0);
        }

        private static Texture3D NewFieldTexture() => new Texture3D(32,32,32,TextureFormat.RGBAFloat,false)
            { wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,name="Two-frame field cache" };

        public void Seek(double timeTg)
        {
            var next=cache.Seek(timeTg);
            // Synchronous transaction: upload both required frames before publishing the epoch.
            if(uploadedLower!=next.Lower.Index) { lowerTexture.SetPixelData(next.Lower.PackedRgba,0);lowerTexture.Apply(false,false);uploadedLower=next.Lower.Index;TextureUploadCount++; }
            if(uploadedUpper!=next.Upper.Index) { upperTexture.SetPixelData(next.Upper.PackedRgba,0);upperTexture.Apply(false,false);uploadedUpper=next.Upper.Index;TextureUploadCount++; }
            snapshot=next;TimeTg=next.TimeTg;volumeMaterial.SetFloat("_Blend",(float)next.Blend);
            Playing=false;UpdateTracers();timeline?.SetValueWithoutNotify((float)TimeTg);
            if(OpticalMode) InvalidateOptical();RefreshLabels();
        }
        public void SetPlaying(bool play) { Playing=play&&!OpticalMode;RefreshLabels(); }
        public void SetRate(double rate)
        {
            if(double.IsNaN(rate)||double.IsInfinity(rate)||Math.Abs(rate)>256||Math.Abs(rate)<1) throw new ArgumentOutOfRangeException(nameof(rate));
            PlaybackRate=rate;RefreshLabels();
        }
        public void SetOpacity(float value)
        {
            if(!float.IsFinite(value)||value<0||value>8) throw new ArgumentOutOfRangeException(nameof(value));
            OpacityScale=value;volumeMaterial.SetFloat("_OpacityScale",value);RefreshLabels();
        }
        public void SetTracers(bool visible) { TracersVisible=visible;tracerRenderer.enabled=visible&&!OpticalMode;RefreshLabels(); }
        public void SetTracerCount(int count)
        {
            if(count<64||count>4096) throw new ArgumentOutOfRangeException(nameof(count));
            tracers=ReferenceFlowTracers.Create(count);vertices=new Vector3[count*4];
            var uv=new Vector2[count*4];var indices=new int[count*6];
            for(int i=0;i<count;i++)
            {
                uv[4*i]=new Vector2(-1,-1);uv[4*i+1]=new Vector2(-1,1);uv[4*i+2]=new Vector2(1,1);uv[4*i+3]=new Vector2(1,-1);
                int j=i*6,k=i*4;indices[j]=k;indices[j+1]=k+1;indices[j+2]=k+2;indices[j+3]=k;indices[j+4]=k+2;indices[j+5]=k+3;
            }
            tracerMesh.Clear();tracerMesh.vertices=vertices;tracerMesh.uv=uv;tracerMesh.triangles=indices;
            tracerMesh.bounds=new Bounds(Vector3.zero,new Vector3(42,14,42));UpdateTracers();RefreshLabels();
        }
        private void UpdateTracers()
        {
            for(int i=0;i<tracers.Length;i++)
            {
                var p=tracers[i].Evaluate(TimeTg);var v=new Vector3((float)p.X,(float)p.Y,(float)p.Z);
                for(int j=0;j<4;j++) vertices[i*4+j]=v;
            }
            tracerMesh.SetVertices(vertices);
        }

        public void SetViewPreset(int preset)
        {
            Vector3[] poses={new Vector3(0,19,-37),new Vector3(0,40,-.01f),new Vector3(0,.6f,-40),new Vector3(0,-21,-36),new Vector3(0,3,-10)};
            if(preset<0||preset>=poses.Length) throw new ArgumentOutOfRangeException(nameof(preset));
            View.transform.position=poses[preset];View.transform.LookAt(Vector3.zero);if(OpticalMode) InvalidateOptical();
        }
        public void MoveCamera(Vector3 delta)
        {
            Vector3 p=View.transform.position+delta;
            if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)) return;
            if(p.sqrMagnitude<2.2f*2.2f) p=p.sqrMagnitude<.001f?Vector3.back*2.2f:p.normalized*2.2f;
            View.transform.position=Vector3.ClampMagnitude(p,90);if(OpticalMode) InvalidateOptical();
        }
        public void SetOpticalMode(bool enabled)
        {
            Playing=false;OpticalMode=enabled;volumeRenderer.enabled=!enabled;tracerRenderer.enabled=!enabled&&TracersVisible;
            opticalImage.gameObject.SetActive(enabled);if(enabled) InvalidateOptical();RefreshLabels();
        }
        private void InvalidateOptical()
        {
            OpticalReady=false;OpticalRevision++;opticalRow=0;opticalUnresolved=0;opticalImage.color=Color.clear;
            imageOrigin=View.transform.position;imageForward=View.transform.forward;imageRight=View.transform.right;imageUp=View.transform.up;
            imageAspect=View.aspect;imageTangent=Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        }
        private void RenderOpticalRows()
        {
            if(OpticalReady) return;
            var origin=ToNumerical(imageOrigin);
            double reference=RadiativeTransferReference.PlanckRadiance(2.417989242e17,3.0e6);
            int end=Math.Min(ImageHeight,opticalRow+4);
            for(int y=opticalRow;y<end;y++) for(int x=0;x<ImageWidth;x++)
            {
                Vector3 dir=(imageForward+imageRight*((2*(x+.5f)/ImageWidth-1)*imageTangent*imageAspect)+imageUp*((2*(y+.5f)/ImageHeight-1)*imageTangent)).normalized;
                var ray=SchwarzschildReference.TraceDisk(origin,ToNumerical(dir),new NumericalVector3(0,-1,0),6,18,1000,.012,4096);
                Color color=new Color(.007f,.013f,.025f);
                if(ray.Outcome==SchwarzschildRayOutcome.DiskHit)
                {
                    double r=Math.Sqrt(ray.PositionM.X*ray.PositionM.X+ray.PositionM.Y*ray.PositionM.Y+ray.PositionM.Z*ray.PositionM.Z);
                    double temp=RadiativeTransferReference.NewtonianZeroTorqueTemperature(r);
                    double g=ray.ObservedToEmittedFrequencyRatio;
                    float value=(float)(RadiativeTransferReference.ShiftedPlanckRadiance(2.417989242e17,temp,g)/reference);
                    Color tint=Color.Lerp(new Color(1,.26f,.04f),new Color(.55f,.82f,1),Mathf.Clamp01(((float)(temp*g)-1.3e6f)/3e6f));
                    color=new Color(1-Mathf.Exp(-value*tint.r),1-Mathf.Exp(-value*tint.g),1-Mathf.Exp(-value*tint.b));
                }
                else if(ray.Outcome==SchwarzschildRayOutcome.Captured) color=Color.black;
                else if(ray.Outcome!=SchwarzschildRayOutcome.Escaped) { color=new Color(.18f,.025f,.2f);opticalUnresolved++; }
                opticalPixels[y*ImageWidth+x]=color;
            }
            opticalRow=end;
            if(opticalRow==ImageHeight)
            {
                opticalTexture.SetPixels32(opticalPixels);opticalTexture.Apply(false,false);opticalImage.color=Color.white;OpticalReady=true;
            }
            RefreshLabels();
        }
        private static NumericalVector3 ToNumerical(Vector3 v)=>new NumericalVector3(v.x,v.y,v.z);

        private void Update()
        {
            if(EmulatedInputEnabled) HandleInput();
            if(Playing)
            {
                double requested=TimeTg+Time.unscaledDeltaTime*PlaybackRate;Seek(requested);
                Playing=requested>0&&requested<ReferenceFlowModel.DurationTg;
            }
            if(OpticalMode) RenderOpticalRows();
            RefreshLabels();
        }
        private void HandleInput()
        {
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null)
            {
                if(k.spaceKey.wasPressedThisFrame) SetPlaying(!Playing);
                if(k.rKey.wasPressedThisFrame) Seek(0);
                if(k.commaKey.wasPressedThisFrame) SetRate(-PlaybackRate);
                if(k.tKey.wasPressedThisFrame) SetTracers(!TracersVisible);
                if(k.oKey.wasPressedThisFrame) SetOpticalMode(!OpticalMode);
                if(k.digit1Key.wasPressedThisFrame) SetViewPreset(0);
                if(k.digit2Key.wasPressedThisFrame) SetViewPreset(1);
                if(k.digit3Key.wasPressedThisFrame) SetViewPreset(2);
                if(k.digit4Key.wasPressedThisFrame) SetViewPreset(3);
                if(k.escapeKey.wasPressedThisFrame) SetPlaying(false);
                Vector3 motion=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                if(motion.sqrMagnitude>0) MoveCamera(View.transform.TransformDirection(motion.normalized)*Time.unscaledDeltaTime*(k.leftShiftKey.isPressed?18:8));
            }
            if(m!=null&&m.rightButton.isPressed)
            {
                Vector2 delta=m.delta.ReadValue();
                if(delta.sqrMagnitude>0) { View.transform.Rotate(Vector3.up,delta.x*.12f,Space.World);View.transform.Rotate(Vector3.right,-delta.y*.12f,Space.Self);if(OpticalMode) InvalidateOptical(); }
            }
        }

        private void BuildInterface()
        {
            if(EventSystem.current==null) { var es=new GameObject("Reference UI events",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform);es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            var root=new GameObject("Reference lab HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var imageGo=new GameObject("Schwarzschild reference image",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));imageGo.transform.SetParent(root.transform,false);
            var ir=imageGo.GetComponent<RectTransform>();ir.anchorMin=Vector2.zero;ir.anchorMax=Vector2.one;ir.offsetMin=ir.offsetMax=Vector2.zero;
            opticalImage=imageGo.GetComponent<RawImage>();opticalImage.raycastTarget=false;
            opticalTexture=new Texture2D(ImageWidth,ImageHeight,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Bilinear};opticalPixels=new Color32[ImageWidth*ImageHeight];opticalImage.texture=opticalTexture;imageGo.SetActive(false);
            Panel(root.transform,0,0,1600,100,new Color(.025f,.045f,.075f,.94f));
            Label(root.transform,"DT / ET     FIELD & LIGHT LAB",28,32,18,920,40,Color.white);
            modeTitle=Label(root.transform,"",16,32,62,1450,28,accent);
            Panel(root.transform,20,125,278,503,new Color(.025f,.045f,.075f,.92f));
            Label(root.transform,"ISOLATED REFERENCE",15,38,143,250,26,accent);
            Button(root.transform,"FLOW INSPECTION",38,182,242,36,()=>SetOpticalMode(false));
            Button(root.transform,"SCHWARZSCHILD / O",38,226,242,36,()=>SetOpticalMode(true));
            Button(root.transform,"TRACERS / T",38,280,242,36,()=>SetTracers(!TracersVisible));
            Button(root.transform,"256 / 1024 MARKERS",38,324,242,36,()=>SetTracerCount(TracerCount==256?1024:256));
            Label(root.transform,"GRAY OPACITY / ASSUMED",13,38,375,242,25,muted);
            MakeSlider(root.transform,38,408,242,0,4,1,x=>SetOpacity(x));
            Button(root.transform,"ABOVE / 2",38,458,116,36,()=>SetViewPreset(1));
            Button(root.transform,"EDGE / 3",162,458,118,36,()=>SetViewPreset(2));
            Button(root.transform,"OVERVIEW / 1",38,502,116,36,()=>SetViewPreset(0));
            Button(root.transform,"BELOW / 4",162,502,118,36,()=>SetViewPreset(3));
            Button(root.transform,"EXIT LAB",38,566,242,36,()=>Application.Quit());
            Panel(root.transform,0,710,1600,190,new Color(.025f,.045f,.075f,.95f));
            status=Label(root.transform,"",18,32,723,1490,30,Color.white);
            Button(root.transform,"PLAY / PAUSE",32,767,145,34,()=>SetPlaying(!Playing));
            Button(root.transform,"REVERSE",188,767,110,34,()=>SetRate(-PlaybackRate));
            Button(root.transform,"RESET",310,767,90,34,()=>Seek(0));
            timeline=MakeSlider(root.transform,430,775,900,0,1024,0,x=>Seek(x));
            Button(root.transform,"RATE",1360,767,170,34,()=>SetRate(Math.Sign(PlaybackRate)*(Math.Abs(PlaybackRate)==64?16:Math.Abs(PlaybackRate)==16?256:64)));
            detail=Label(root.transform,"",14,32,815,1510,38,muted);
            opticsNote=Label(root.transform,"",14,32,858,1510,30,accent);
        }
        private void RefreshLabels()
        {
            if(status==null) return;
            modeTitle.text=OpticalMode?"STATIC SCHWARZSCHILD OBSERVER  /  curved rays + thin emitting surface  /  1 keV false colour":"ASSUMED 3D TORUS  /  straight-ray coordinate inspection  /  gray thermal emission, false colour";
            status.text=OpticalMode? $"{(OpticalReady?"Reference ready":"Rebuilding reference")}  |  {opticalRow}/{ImageHeight} rows  |  unresolved rays: {opticalUnresolved}  |  observer r={View.transform.position.magnitude:0.00} rg"
                :$"{(Playing?"Playing":"Paused")}  |  t = {TimeTg:0.00} tg  |  rate {PlaybackRate:+0;-0} tg/s  |  tracers {(TracersVisible?TracerCount.ToString():"off")}  |  gray opacity ×{OpacityScale:0.00}";
            detail.text=OpticalMode?"Steady Newtonian zero-torque temperature profile; assumed 35 solar masses, mass supply 5×10^15 kg/s. Static camera; no inferred observer velocity."
                :$"32³ × 2: {CacheBytes/1048576.0:0.00} MiB field arrays + 1 MiB texture payload per CPU/GPU copy; other application memory is additional. Tracers do not change emission.";
            opticsNote.text=OpticalMode?"Direct geodesic reference, 192×108; rebuilds after movement. Purple means unresolved. Single-hole control only; not binary optics."
                :"WASD + Q/E move; right-drag looks. Space plays; comma reverses. rg=GM/c², tg=GM/c³. This assumed field is separate from the SXS event.";
        }
        private GameObject Panel(Transform parent,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject("Panel",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);Place(go,x,y,w,h);go.GetComponent<Image>().color=color;return go;
        }
        private Text Label(Transform parent,string text,int size,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);Place(go,x,y,w,h);
            var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.color=color;t.text=text;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Overflow;return t;
        }
        private void Button(Transform parent,string text,float x,float y,float w,float h,Action action)
        {
            var go=Panel(parent,x,y,w,h,new Color(.08f,.14f,.21f));var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();
            var t=Label(go.transform,text,13,0,0,w,h,accent);t.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(()=>action());
        }
        private Slider MakeSlider(Transform parent,float x,float y,float w,float min,float max,float value,Action<float> action)
        {
            var go=Panel(parent,x,y,w,15,new Color(.16f,.22f,.3f));var s=go.AddComponent<Slider>();
            var fill=Panel(go.transform,0,0,w,15,accent).GetComponent<RectTransform>();s.fillRect=fill;
            var handle=Panel(go.transform,0,-3,13,21,Color.white).GetComponent<RectTransform>();s.handleRect=handle;s.targetGraphic=handle.GetComponent<Image>();
            s.minValue=min;s.maxValue=max;s.SetValueWithoutNotify(value);s.onValueChanged.AddListener(v=>action(v));return s;
        }
        private static void Place(GameObject go,float x,float y,float w,float h)
        { var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
        private void OnDestroy()
        {
            Destroy(lowerTexture);Destroy(upperTexture);Destroy(volumeMaterial);Destroy(tracerMaterial);Destroy(tracerMesh);Destroy(opticalTexture);
            snapshot=default;cache=null;tracers=null;vertices=null;opticalPixels=null;
        }
    }
}
