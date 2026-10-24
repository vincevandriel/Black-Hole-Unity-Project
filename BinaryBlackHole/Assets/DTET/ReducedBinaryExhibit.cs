using System;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Coordinate inspection of SXS history with prescribed gas.
    public sealed class ReducedBinaryExhibit : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public ExhibitQualitySettings Profile { get; private set; }
        public bool FallbackActive => Ready && !Profile.GasEnabled;
        public Camera View { get; private set; }
        public Camera CaptureCamera { get; private set; }
        public NumericalEventSession Session { get; private set; }
        public NumericalEventState State => Session == null ? default : Session.State;
        public ReducedBinaryFlowState FieldState { get; private set; }
        public bool EmulatedInputEnabled { get; set; } = true;
        public bool TracersVisible { get; private set; }
        public bool GasVisible { get; private set; }
        public int TracerCount => samplePositions == null ? 0 : samplePositions.Length;
        public double TracerTotalWeight { get; private set; }
        public ulong TracerFingerprint { get; private set; }
        public ulong FieldFingerprint { get; private set; }
        public long CacheBytes => 0; // Analytic requested-epoch field: no 3D history buffers.
        public int CacheFrameCount => 0;
        public int SkyCount => sky == null ? 0 : sky.StarCount;
        public ulong SkyFingerprint => sky == null ? 0 : sky.CatalogFingerprint;
        public RenderTexture SceneTarget { get; private set; }
        public long RenderAllocationBytes => SceneTarget == null ? 0 : (long)SceneTarget.width*SceneTarget.height*(SceneTarget.format==RenderTextureFormat.ARGBHalf?12:8);
        public string StatusText => status == null ? "" : status.text;
        public string RadiationText => radiationLabel == null ? "" : radiationLabel.text;
        public double GravitationalRadiusMeters => flow == null ? 1 : flow.GravitationalRadiusMeters;
        public double LengthMetersPerDisplayUnit => GravitationalRadiusMeters;
        public Transform BodyATransform => markerA;
        public Transform BodyBTransform => markerB;
        public Transform RemnantTransform => markerRemnant;
        public Material GasMaterial => gasMaterial;
        public ExhibitQualityProfile SelectedProfile { get; private set; } = ExhibitQualityProfile.Auto;

        private ReducedBinaryFlowModel flow;
        private ReducedExhibitSky sky;
        private GameObject launcher, sceneRoot, hud;
        private Text launchDetail, status, detail, radiationLabel, phaseLabel, sampleLabel, help;
        private Slider timeline;
        private Image graphCursor;
        private Transform markerA, markerB, markerRemnant;
        private Material gasMaterial, markerMaterial, sampleMaterial, outlineMaterial, displayMaterial;
        private MeshRenderer gasRenderer, sampleRenderer;
        private Mesh sampleMesh;
        private Vector3[] samplePositions, sampleVertices;
        private Color[] sampleColors;
        private Texture2D graphTexture;
        private double lastPresentedTime = double.NaN;
        private double labelTime;
        private bool showHelp;
        private int lastScreenWidth, lastScreenHeight;
        private static readonly Color Gold = new Color(1,.72f,.35f);

        private void Awake()
        {
            CaptureCamera=Camera.main;
            if(CaptureCamera==null) { var camera=new GameObject("M4 interface camera");camera.tag="MainCamera";CaptureCamera=camera.AddComponent<Camera>(); }
            CaptureCamera.cullingMask=1<<5; CaptureCamera.clearFlags=CameraClearFlags.SolidColor;
            CaptureCamera.backgroundColor=new Color(.008f,.015f,.028f); CaptureCamera.nearClipPlane=.01f; CaptureCamera.farClipPlane=10;
            CaptureCamera.depth=10; CaptureCamera.allowHDR=false; CaptureCamera.allowMSAA=false;
            if(EventSystem.current==null)
            {
                var events=new GameObject("M4 desktop input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildLauncher();
        }
        private static ExhibitCapabilities Capabilities(bool forceFallback)
            => new ExhibitCapabilities(forceFallback?0:SystemInfo.graphicsShaderLevel,SystemInfo.graphicsMemorySize,
                SystemInfo.systemMemorySize,SystemInfo.supports3DTextures,SystemInfo.supportsComputeShaders);

        private void BuildLauncher()
        {
            launcher=ReducedExhibitUi.Canvas(transform,CaptureCamera,"M4 quality launcher");
            var p=launcher.transform;
            ReducedExhibitUi.Label(p,"DT / ET     INTERACTIVE COSMOS",18,110,95,1300,40,ReducedExhibitUi.Accent);
            ReducedExhibitUi.Label(p,"GW150914",76,105,160,1320,100,Color.white);
            ReducedExhibitUi.Label(p,"Explore an inspiral. Freeze a merger. Follow the remnant.",26,110,275,1350,50,ReducedExhibitUi.Muted);
            ReducedExhibitUi.Label(p,"SXS numerical binary replay  /  assumed gas  /  synthetic sky",18,110,337,1350,40,Gold);
            var options=new[]{ExhibitQualityProfile.Auto,ExhibitQualityProfile.Laptop,ExhibitQualityProfile.Balanced,ExhibitQualityProfile.High};
            string[] captions={"AUTO","LAPTOP","BALANCED","HIGH"};
            for(int i=0;i<options.Length;i++)
            { var option=options[i];ReducedExhibitUi.Button(p,captions[i],110+i*345,425,320,62,()=>SelectProfile(option)); }
            launchDetail=ReducedExhibitUi.Label(p,"",19,110,520,1370,105,ReducedExhibitUi.Muted);
            ReducedExhibitUi.Button(p,"ENTER EXHIBIT",110,660,350,64,()=>Launch(SelectedProfile));
            ReducedExhibitUi.Button(p,"COMPATIBILITY / GAS OFF",490,660,350,64,()=>Launch(SelectedProfile,true));
            ReducedExhibitUi.Button(p,"EXIT",870,660,180,64,()=>Application.Quit());
            ReducedExhibitUi.Label(p,"Choose before loading. Lower settings change display detail, not event timing or the assumed gas model.\nLaptop is a rendering budget; performance depends on your device. Compatibility keeps the event, sky and waveform.",16,110,765,1370,72,ReducedExhibitUi.Muted);
            SelectProfile(SelectedProfile);
        }
        public void SelectProfile(ExhibitQualityProfile profile)
        {
            SelectedProfile=profile; var settings=ExhibitQuality.Resolve(profile,Capabilities(false));
            launchDetail.text=$"SELECTED  {profile.ToString().ToUpperInvariant()}  →  {settings.EffectiveProfile}\n"
                +$"Scene up to {settings.MaxRenderWidth} × {settings.MaxRenderHeight}  ·  {settings.RaySteps} base ray segments + inner refinement  ·  {settings.StarCount:N0} sky sources\n"
                +settings.Reason;
        }
        public void Launch(ExhibitQualityProfile profile, bool forceFallback=false)
        {
            try { LaunchInternal(profile,forceFallback); }
            catch { ReturnToLauncher();throw; }
        }
        private void LaunchInternal(ExhibitQualityProfile profile, bool forceFallback)
        {
            if(Ready) ReturnToLauncher();
            var gasShader=Resources.Load<Shader>("DTET/ReducedBinaryVolume");
            bool gasUnavailable=gasShader==null||!gasShader.isSupported;
            Profile=ExhibitQuality.Resolve(profile,Capabilities(forceFallback||gasUnavailable)); SelectedProfile=profile;
            var asset=Resources.Load<TextAsset>("Physics/SXS0305");
            if(asset==null) throw new InvalidOperationException("The licensed SXS0305 replay asset is missing.");
            try { using(var stream=new MemoryStream(asset.bytes,false)) Session=new NumericalEventSession(NumericalEventHistory.Load(stream)); }
            finally { Resources.UnloadAsset(asset); }
            flow=new ReducedBinaryFlowModel(Session.Model); Session.SetPlaybackRate(.05);
            sceneRoot=new GameObject("M4 reduced coordinate scene");sceneRoot.transform.SetParent(transform);
            var camera=new GameObject("M4 scene camera");camera.transform.SetParent(sceneRoot.transform);View=camera.AddComponent<Camera>();
            View.cullingMask=~(1<<5);View.clearFlags=CameraClearFlags.SolidColor;View.backgroundColor=new Color(.001f,.002f,.004f);
            View.nearClipPlane=.05f;View.farClipPlane=1000;View.fieldOfView=52;View.depth=-10;View.allowHDR=true;View.allowMSAA=false;
            ResizeSceneTarget();
            sky=sceneRoot.AddComponent<ReducedExhibitSky>();sky.Initialize(View,Profile.StarCount);
            markerMaterial=new Material(Resources.Load<Shader>("DTET/SolidSurface"));markerMaterial.SetColor("_Color",Color.black);
            outlineMaterial=new Material(Resources.Load<Shader>("DTET/GlowLine"));
            markerA=Marker("Primary area proxy",new Color(.3f,.77f,1));markerB=Marker("Secondary area proxy",new Color(.8f,.55f,1));
            markerRemnant=Marker("Common apparent horizon area proxy",Gold);
            if(Profile.GasEnabled) BuildGas();
            BuildSamples(); BuildHud();
            launcher.SetActive(false);Ready=true;GasVisible=Profile.GasEnabled;TracersVisible=false;
            SetViewPreset(0);Seek(0);SetTracers(false);
        }
        private void ResizeSceneTarget()
        {
            int sw=Math.Max(320,Screen.width),sh=Math.Max(180,Screen.height);
            Profile.RenderDimensions(sw,sh,out int width,out int height);
            if(SceneTarget!=null) {View.targetTexture=null;SceneTarget.Release();Destroy(SceneTarget);}
            var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
            SceneTarget=new RenderTexture(width,height,24,format,RenderTextureReadWrite.Linear){name="M4 bounded scene target",filterMode=FilterMode.Bilinear,antiAliasing=1};
            if(!SceneTarget.Create())throw new NotSupportedException("The bounded scene render target could not be allocated. Close other graphics applications or select a lower quality profile.");
            View.targetTexture=SceneTarget;lastScreenWidth=Screen.width;lastScreenHeight=Screen.height;
            if(hud!=null) {var image=hud.GetComponentInChildren<RawImage>();if(image!=null)image.texture=SceneTarget;}
        }
        private Transform Marker(string name, Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(sceneRoot.transform);
            Destroy(go.GetComponent<Collider>());go.GetComponent<MeshRenderer>().sharedMaterial=markerMaterial;
            var ring=new GameObject("Area proxy guide");ring.transform.SetParent(go.transform,false);
            var line=ring.AddComponent<LineRenderer>();line.sharedMaterial=outlineMaterial;line.useWorldSpace=false;line.loop=true;
            line.positionCount=64;line.widthMultiplier=.009f;line.startColor=line.endColor=color*.65f;
            for(int i=0;i<64;i++){float a=2*Mathf.PI*i/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.505f,0,Mathf.Sin(a)*.505f));}
            return go.transform;
        }
        private void BuildGas()
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="Reduced emitting volume";box.transform.SetParent(sceneRoot.transform);
            box.transform.localScale=new Vector3((float)flow.HalfExtentXZ*2,(float)flow.HalfExtentY*2,(float)flow.HalfExtentXZ*2);Destroy(box.GetComponent<Collider>());
            gasMaterial=new Material(Resources.Load<Shader>("DTET/ReducedBinaryVolume"));gasMaterial.SetFloat("_Steps",Profile.RaySteps);
            gasMaterial.SetVector("_HalfExtent",new Vector4((float)flow.HalfExtentXZ,(float)flow.HalfExtentY,(float)flow.HalfExtentXZ,0));
            gasMaterial.SetVector("_FlowBounds",new Vector4((float)flow.HalfExtentXZ,(float)flow.HalfExtentY,0,0));
            gasMaterial.SetFloat("_Exposure",2.5f);gasMaterial.SetFloat("_Opacity",(float)ReducedBinaryFlowModel.GrayExtinctionPerRg);
            gasRenderer=box.GetComponent<MeshRenderer>();gasRenderer.sharedMaterial=gasMaterial;
        }
        private void BuildSamples()
        {
            int count=Profile.TracerCount;samplePositions=new Vector3[count];sampleVertices=new Vector3[count*4];sampleColors=new Color[count*4];
            var uv=new Vector2[count*4];var indices=new int[count*6];
            for(int i=0;i<count;i++)
            {
                samplePositions[i]=new Vector3((float)((Radical(i+1,2)*2-1)*flow.HalfExtentXZ),(float)((Radical(i+1,3)*2-1)*flow.HalfExtentY),(float)((Radical(i+1,5)*2-1)*flow.HalfExtentXZ));
                for(int j=0;j<4;j++)sampleVertices[4*i+j]=samplePositions[i];
                uv[4*i]=new Vector2(-1,-1);uv[4*i+1]=new Vector2(-1,1);uv[4*i+2]=new Vector2(1,1);uv[4*i+3]=new Vector2(1,-1);
                int k=i*4,t=i*6;indices[t]=k;indices[t+1]=k+1;indices[t+2]=k+2;indices[t+3]=k;indices[t+4]=k+2;indices[t+5]=k+3;
            }
            sampleMesh=new Mesh{name="Weighted density samples"};sampleMesh.MarkDynamic();sampleMesh.vertices=sampleVertices;sampleMesh.uv=uv;sampleMesh.colors=sampleColors;sampleMesh.triangles=indices;
            sampleMesh.bounds=new Bounds(Vector3.zero,new Vector3((float)flow.HalfExtentXZ*2, (float)flow.HalfExtentY*2,(float)flow.HalfExtentXZ*2));
            sampleMaterial=new Material(Resources.Load<Shader>("DTET/ReducedDensitySamples"));
            var go=new GameObject("Density sampling overlay",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(sceneRoot.transform);
            go.GetComponent<MeshFilter>().sharedMesh=sampleMesh;sampleRenderer=go.GetComponent<MeshRenderer>();sampleRenderer.sharedMaterial=sampleMaterial;
        }
        private static double Radical(int index,int radix)
        { double value=0,f=1.0/radix;while(index>0){value+=(index%radix)*f;index/=radix;f/=radix;}return value; }
        public void Seek(double seconds)
        { if(!Ready)return;Session.Seek(seconds);Present();RefreshLabels(); }
        public void SetRate(double rate) {if(!Ready)return;Session.SetPlaybackRate(rate);RefreshLabels();}
        public void SetPlaying(bool play) {if(!Ready)return;if(play)Session.Play();else Session.Pause();RefreshLabels();}
        public void SetTracers(bool visible)
        {TracersVisible=visible;if(sampleRenderer!=null)sampleRenderer.enabled=visible;RefreshLabels();}
        public void SetGas(bool visible)
        {GasVisible=visible&&Ready&&Profile.GasEnabled;if(gasRenderer!=null)gasRenderer.enabled=GasVisible;RefreshLabels();}
        public void SetGasVisible(bool visible) => SetGas(visible);
        private void Present()
        {
            double time=Session.PlaybackTimeSeconds;
            if(time==lastPresentedTime)return;lastPresentedTime=time;
            FieldState=flow.EvaluateState(time);var state=State;
            PresentMarker(markerA,state.BodyA);PresentMarker(markerB,state.BodyB);PresentMarker(markerRemnant,state.Remnant);
            MoveCamera(Vector3.zero); // A seek or moving horizon cannot leave this exterior camera inside a proxy.
            if(gasMaterial!=null)BindGas();
            TracerTotalWeight=0;ulong hash=14695981039346656037UL;
            double cellVolume=8*flow.HalfExtentXZ*flow.HalfExtentXZ*flow.HalfExtentY/TracerCount;
            for(int i=0;i<TracerCount;i++)
            {
                var p=samplePositions[i];var value=flow.Sample(FieldState,p.x,p.y,p.z);
                TracerTotalWeight+=value.RelativeDensity*cellVolume;
                float alpha=(float)Math.Min(.95,value.RelativeDensity*1.5);
                var color=new Color(.25f,.95f,1,alpha);for(int j=0;j<4;j++)sampleColors[4*i+j]=color;
                hash=Mix(hash,(float)value.RelativeDensity);
            }
            TracerFingerprint=hash;sampleMesh.colors=sampleColors;
            hash=Mix(14695981039346656037UL,(float)time);
            // A profile-independent sampling fingerprint of the same analytic field.
            for(int i=1;i<=48;i++)
            {var value=flow.Sample(FieldState,(Radical(i,2)*2-1)*flow.HalfExtentXZ,(Radical(i,3)*2-1)*flow.HalfExtentY,(Radical(i,5)*2-1)*flow.HalfExtentXZ);hash=Mix(hash,(float)value.RelativeDensity);}
            FieldFingerprint=hash;timeline.SetValueWithoutNotify((float)(time/Session.Model.DurationSeconds));
            RefreshGraphCursor();
        }
        private static ulong Mix(ulong hash,float value)=>unchecked((hash^(uint)BitConverter.SingleToInt32Bits(value))*1099511628211UL);
        private void PresentMarker(Transform target,NumericalHorizonState? horizon)
        {
            target.gameObject.SetActive(horizon.HasValue);if(!horizon.HasValue)return;
            var h=horizon.Value;target.position=Map(h.PositionMeters);target.localScale=Vector3.one*(float)(2*h.ArealRadiusMeters/GravitationalRadiusMeters);
        }
        private Vector3 Map(NumericalVector3 p)=>new Vector3((float)(p.X/GravitationalRadiusMeters),(float)(p.Z/GravitationalRadiusMeters),(float)(p.Y/GravitationalRadiusMeters));
        private void BindGas()
        {
            ReducedBinaryFlowBinding.Bind(gasMaterial,FieldState);
            var s=State;gasMaterial.SetVector("_HorizonA",Horizon(s.BodyA));gasMaterial.SetVector("_HorizonB",Horizon(s.BodyB));gasMaterial.SetVector("_HorizonR",Horizon(s.Remnant));
        }
        private Vector4 Horizon(NumericalHorizonState? h)
        {if(!h.HasValue)return Vector4.zero;var p=Map(h.Value.PositionMeters);return new Vector4(p.x,p.y,p.z,(float)(h.Value.ArealRadiusMeters/GravitationalRadiusMeters));}
        public void SetViewPreset(int preset)
        {
            if(View==null)return;float r=(float)flow.HalfExtentXZ;
            Vector3[] points={new Vector3(0,r*.95f,-r*1.8f),new Vector3(0,r*2.05f,-.01f),new Vector3(0,r*.035f,-r*2),new Vector3(0,-r*.95f,-r*1.8f),new Vector3(0,10,-23)};
            if(preset<0||preset>=points.Length)throw new ArgumentOutOfRangeException(nameof(preset));
            View.transform.position=points[preset];View.transform.LookAt(Vector3.zero);MoveCamera(Vector3.zero);
        }
        public void MoveCamera(Vector3 delta)
        {
            if(View==null||!float.IsFinite(delta.x)||!float.IsFinite(delta.y)||!float.IsFinite(delta.z))return;
            var p=Vector3.ClampMagnitude(View.transform.position+delta,(float)flow.HalfExtentXZ*4);
            KeepOutside(ref p,State.BodyA);KeepOutside(ref p,State.BodyB);KeepOutside(ref p,State.Remnant);View.transform.position=p;
        }
        private void KeepOutside(ref Vector3 p,NumericalHorizonState? h)
        {if(!h.HasValue)return;var centre=Map(h.Value.PositionMeters);float r=(float)(h.Value.ArealRadiusMeters/GravitationalRadiusMeters)*1.1f;var delta=p-centre;if(delta.sqrMagnitude<r*r)p=centre+(delta.sqrMagnitude<1e-8?Vector3.back:delta.normalized)*r;}

        private void Update()
        {
            if(!Ready)return;
            if(lastScreenWidth!=Screen.width||lastScreenHeight!=Screen.height)ResizeSceneTarget();
            if(EmulatedInputEnabled)HandleInput();
            if(!Ready)return;Session.Tick(Time.unscaledDeltaTime);Present();
            if(Time.unscaledTimeAsDouble>=labelTime){RefreshLabels();labelTime=Time.unscaledTimeAsDouble+.1;}
        }
        private void HandleInput()
        {
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null)
            {
                if(k.spaceKey.wasPressedThisFrame)SetPlaying(!Session.IsPlaying);
                if(k.commaKey.wasPressedThisFrame)SetRate(-Session.PlaybackRate);
                if(k.rKey.wasPressedThisFrame)Seek(0);
                if(k.mKey.wasPressedThisFrame)Seek(Session.Model.CommonHorizonElapsedSeconds);
                if(k.tKey.wasPressedThisFrame)SetTracers(!TracersVisible);
                if(k.gKey.wasPressedThisFrame)SetGas(!GasVisible);
                if(k.hKey.wasPressedThisFrame){showHelp=!showHelp;help.gameObject.SetActive(showHelp);}
                if(k.escapeKey.wasPressedThisFrame)SetPlaying(false);
                if(k.digit1Key.wasPressedThisFrame)SetViewPreset(0);if(k.digit2Key.wasPressedThisFrame)SetViewPreset(1);
                if(k.digit3Key.wasPressedThisFrame)SetViewPreset(2);if(k.digit4Key.wasPressedThisFrame)SetViewPreset(3);if(k.digit5Key.wasPressedThisFrame)SetViewPreset(4);
                var move=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                if(move.sqrMagnitude>0)MoveCamera(View.transform.TransformDirection(move.normalized)*Time.unscaledDeltaTime*(k.leftShiftKey.isPressed?25:9));
            }
            if(m!=null)
            {
                if(m.rightButton.isPressed){var d=m.delta.ReadValue();View.transform.Rotate(Vector3.up,d.x*.12f,Space.World);View.transform.Rotate(Vector3.right,-d.y*.12f,Space.Self);}
                float wheel=m.scroll.ReadValue().y;if(wheel!=0&&!EventSystem.current.IsPointerOverGameObject())MoveCamera(View.transform.forward*wheel*.018f);
            }
        }
        private void BuildHud()
        {
            hud=ReducedExhibitUi.Canvas(transform,CaptureCamera,"M4 exhibit HUD");var p=hud.transform;
            var image=ReducedExhibitUi.Image(p,"Scene image",SceneTarget,0,0,1600,900);
            displayMaterial=new Material(Resources.Load<Shader>("DTET/ExhibitDisplay"));image.material=displayMaterial;
            var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            ReducedExhibitUi.Panel(p,"Header",0,0,1600,93,ReducedExhibitUi.Ink);
            ReducedExhibitUi.Label(p,"GW150914  /  BINARY EXPLORER",28,28,15,1170,40,Color.white);
            phaseLabel=ReducedExhibitUi.Label(p,"",16,30,60,1460,28,Gold);
            ReducedExhibitUi.Panel(p,"Navigation",20,120,245,396,ReducedExhibitUi.Ink);
            ReducedExhibitUi.Label(p,"VIEWPOINT",14,38,139,205,30,ReducedExhibitUi.Muted);
            string[] names={"OVERVIEW / 1","ABOVE / 2","EDGE / 3","BELOW / 4","CLOSE / 5"};
            for(int i=0;i<5;i++){int index=i;ReducedExhibitUi.Button(p,names[i],38,180+i*46,209,37,()=>SetViewPreset(index));}
            ReducedExhibitUi.Button(p,"QUALITY / RETURN",38,437,209,42,ReturnToLauncher);
            ReducedExhibitUi.Panel(p,"Diagnostics",20,535,245,168,ReducedExhibitUi.Ink);
            ReducedExhibitUi.Button(p,"GAS ON / OFF  [G]",38,552,209,36,()=>SetGas(!GasVisible));
            ReducedExhibitUi.Button(p,"DENSITY SAMPLES [T]",38,598,209,36,()=>SetTracers(!TracersVisible));
            sampleLabel=ReducedExhibitUi.Label(p,"",13,38,647,211,44,ReducedExhibitUi.Muted);
            ReducedExhibitUi.Panel(p,"Transport",0,732,1600,168,ReducedExhibitUi.Ink);
            status=ReducedExhibitUi.Label(p,"",18,30,744,1500,30,Color.white);
            ReducedExhibitUi.Button(p,"PLAY / PAUSE",30,785,155,37,()=>SetPlaying(!Session.IsPlaying));
            ReducedExhibitUi.Button(p,"REVERSE",195,785,110,37,()=>SetRate(-Session.PlaybackRate));
            ReducedExhibitUi.Button(p,"TO MERGER",315,785,132,37,()=>Seek(Session.Model.CommonHorizonElapsedSeconds));
            timeline=ReducedExhibitUi.Slider(p,"Timeline",475,797,850,v=>Seek(v*Session.Model.DurationSeconds));
            ReducedExhibitUi.Button(p,"RATE",1360,785,190,37,()=>SetRate(Math.Sign(Session.PlaybackRate)*(Math.Abs(Session.PlaybackRate)<.02?.05:Math.Abs(Session.PlaybackRate)<.1?.2:.01)));
            detail=ReducedExhibitUi.Label(p,"",14,30,842,1500,45,ReducedExhibitUi.Muted);
            ReducedExhibitUi.Panel(p,"Radiation panel",1090,120,490,200,ReducedExhibitUi.Ink);
            ReducedExhibitUi.Label(p,"NUMERICAL RADIATION   Re(H22) / peak |H22|",15,1108,137,455,28,ReducedExhibitUi.Accent);
            graphTexture=BuildGraph();ReducedExhibitUi.Image(p,"Radiation history",graphTexture,1110,173,450,92);
            graphCursor=ReducedExhibitUi.Panel(p,"Radiation cursor",1110,173,2,92,Gold).GetComponent<Image>();
            radiationLabel=ReducedExhibitUi.Label(p,"",13,1110,277,450,38,ReducedExhibitUi.Muted);
            ReducedExhibitUi.Button(p,"HELP / H",1420,25,130,38,()=>{showHelp=!showHelp;help.gameObject.SetActive(showHelp);});
            help=ReducedExhibitUi.Label(p,"WASD move · Q/E rise/fall · right-drag look · wheel approach · Shift faster\nSpace pause/play · comma reverse · R reset · M merger · 1–5 viewpoints\n\nGas is a prescribed educational field, not measured GW150914 gas or a GRMHD solution.\nDensity samples are weighted sampling locations, not material trajectories.\n\nBodies are area-equivalent apparent-horizon proxies, not optical shadows.\nThe waveform is an SXS radiation mode with aligned comparison epochs, not detector strain.\nSky brightness and colours are synthetic; distant sources have no local parallax.\n\nThis coordinate view uses straight light rays. The separate M3 lab provides an isolated-hole optical control.",18,320,390,1140,300,Color.white);
            help.gameObject.SetActive(false);showHelp=false;
        }
        private Texture2D BuildGraph()
        {
            const int w=512,h=100;var pixels=new Color32[w*h];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(7,15,25,255);
            for(int x=0;x<w;x++)pixels[x+w*(h/2)]=new Color32(32,55,70,255);
            int previous=-1;
            for(int x=0;x<w;x++)
            {
                var radiation=Session.Model.Evaluate(Session.Model.DurationSeconds*x/(w-1)).Radiation;
                if(!radiation.HasValue){previous=-1;for(int y=0;y<h;y++)pixels[x+w*y]=new Color32(22,25,31,255);continue;}
                int value=Mathf.Clamp(Mathf.RoundToInt((float)(.5+.43*radiation.Value.RealH22/Session.Model.WaveformPeakAmplitude)*h),0,h-1);
                if(previous<0)previous=value;for(int y=Math.Min(value,previous);y<=Math.Max(value,previous);y++)pixels[x+w*y]=new Color32(85,220,235,255);previous=value;
            }
            var result=new Texture2D(w,h,TextureFormat.RGBA32,false){name="SXS normalized radiation plot",filterMode=FilterMode.Bilinear};result.SetPixels32(pixels);result.Apply(false,true);return result;
        }
        private void RefreshLabels()
        {
            if(!Ready||status==null)return;var s=State;string phase=s.HasCommonHorizon?"COMMON HORIZON / REMNANT":"INSPIRAL";
            phaseLabel.text=$"{phase}   ·   SXS:BBH:0305 representative replay   ·   {(FallbackActive?"COMPATIBILITY: GAS OFF":"ASSUMED GAS / STRAIGHT-RAY VIEW")}";
            status.text=$"{(Session.IsPlaying?"PLAYING":"PAUSED")}   {s.SourceReferenceAlignedSeconds:+0.0000;-0.0000;0.0000} s relative to common horizon   |   rate {Session.PlaybackRate:+0.00;-0.00}×   |   {Profile.EffectiveProfile}";
            detail.text=$"WASD + Q/E move · right-drag look · H explains the model   |   Scene {SceneTarget.width}×{SceneTarget.height} · {SkyCount:N0} synthetic stars\n"
                +(sky!=null&&!sky.Ready?sky.Status+"  ·  ":"")
                +(SceneTarget.format!=RenderTextureFormat.ARGBHalf?"LDR fallback: bright radiance clipped  ·  ":"")
                +(s.SeparationMeters.HasValue?$"Separation {s.SeparationMeters.Value/1000:0.0} km  ·  ":"")+"Horizon spheres show equal-area proxies. Gas and sky are assumed; radiation is a numerical mode.";
            sampleLabel.text=TracersVisible?$"{TracerCount:N0} weighted locations\nDiagnostic samples, not parcels":"Optional field sampling overlay";
            radiationLabel.text=s.Radiation.HasValue?$"Re(H22)/peak = {s.Radiation.Value.RealH22/Session.Model.WaveformPeakAmplitude:+0.000;-0.000;0.000}\nEpoch alignment only; not detector strain":"RADIATION OUTSIDE RECORDED COVERAGE\nGray part of plot: no extrapolation";
            RefreshGraphCursor();
        }
        private void RefreshGraphCursor()
        {
            graphCursor.rectTransform.anchoredPosition=new Vector2(1110+(float)(450*Session.PlaybackTimeSeconds/Session.Model.DurationSeconds),-173);
            graphCursor.color=State.Radiation.HasValue?Gold:ReducedExhibitUi.Muted;
        }
        public void ReturnToLauncher()
        {
            Ready=false;lastPresentedTime=double.NaN;
            if(sceneRoot!=null){sceneRoot.SetActive(false);Destroy(sceneRoot);}if(hud!=null){hud.SetActive(false);Destroy(hud);}
            if(View!=null)View.targetTexture=null;
            if(SceneTarget!=null){SceneTarget.Release();Destroy(SceneTarget);}SceneTarget=null;
            Destroy(gasMaterial);Destroy(markerMaterial);Destroy(sampleMaterial);Destroy(outlineMaterial);Destroy(displayMaterial);Destroy(sampleMesh);Destroy(graphTexture);
            sceneRoot=null;hud=null;View=null;sky=null;Session=null;flow=null;gasMaterial=null;sampleMesh=null;graphTexture=null;
            samplePositions=null;sampleVertices=null;sampleColors=null;TracersVisible=false;GasVisible=false;FieldFingerprint=0;TracerFingerprint=0;
            if(launcher!=null){launcher.SetActive(true);if(launchDetail!=null)SelectProfile(SelectedProfile);}
        }
        private void OnDestroy(){ReturnToLauncher();}
    }
}
