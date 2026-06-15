using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using Gridiron;
using System.Linq;

namespace Gridiron.EditorTools
{
    /// <summary>Builds Main.unity end-to-end (PRD §4.4, §13, M0/M5/M6). Re-runnable: recreates the scene from scratch.</summary>
    public static class GridironSceneBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        static int LGround => LayerMask.NameToLayer("Ground");
        static int LBall => LayerMask.NameToLayer("Ball");
        static int LPlayers => LayerMask.NameToLayer("Players");
        static int LCatch => LayerMask.NameToLayer("CatchZones");

        [MenuItem("Gridiron/3. Build Main Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildStadium();
            var field = BuildFieldSystems();
            var ball = BuildFootball();
            var qb = BuildCharacter("QB", new Color(0.75f, 0.12f, 0.12f), "Assets/_Game/Art/QB.controller");
            var wrGo = BuildCharacter("WR", new Color(0.15f, 0.3f, 0.85f), "Assets/_Game/Art/WR.controller");
            var cbGo = BuildCharacter("CB", new Color(0.92f, 0.92f, 0.95f), "Assets/_Game/Art/CB.controller");
            var director = BuildCameras();
            BuildUI();
            WireEverything(field, ball, qb, wrGo, cbGo, director);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("[Gridiron] Main scene built and saved: " + ScenePath);
        }

        static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildStadium()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stadium/Football_Stadium.fbx");
            if (fbx != null)
            {
                var stadium = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                stadium.name = "Stadium";
                stadium.transform.position = Vector3.zero;
                // §4.4 REQUIRED ADJUSTMENT: FBX long axis runs along X → rotate Y=90° so the field runs along Z.
                stadium.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                // FBX-embedded cameras/lights must never render — only the Cinemachine-driven Main Camera may
                foreach (var cam in stadium.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                foreach (var li in stadium.GetComponentsInChildren<Light>(true)) li.enabled = false;
                foreach (var al in stadium.GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
                foreach (var t in stadium.GetComponentsInChildren<Transform>(true))
                    t.gameObject.isStatic = true;
            }
            else Debug.LogWarning("[Gridiron] stadium.fbx not found");

            // Field meshes are zero-thickness planes — box-collider ground plane instead (§4.4)
            var ground = new GameObject("GroundCollider");
            ground.layer = LGround;
            var box = ground.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.25f, 0f);
            box.size = new Vector3(140f, 0.5f, 160f);
            ground.isStatic = true;
        }

        static FieldBounds BuildFieldSystems()
        {
            var root = new GameObject("FieldSystems");
            var field = root.AddComponent<FieldBounds>();

            new GameObject("LOSMarker").transform.SetParent(root.transform);
            new GameObject("FirstDownMarker").transform.SetParent(root.transform);

            // §4.3: trigger volume covering the offensive end zone (debug/visual aid; logic uses FieldBounds math)
            var ez = new GameObject("EndZoneTrigger");
            ez.transform.SetParent(root.transform);
            ez.transform.position = new Vector3(0f, 1.5f, (45.72f + 54.86f) * 0.5f);
            var ezBox = ez.AddComponent<BoxCollider>();
            ezBox.isTrigger = true;
            ezBox.size = new Vector3(48.76f, 3f, 9.144f);

            // LOS + first-down lines (§11.3)
            var linesGo = new GameObject("FieldLines");
            linesGo.transform.SetParent(root.transform);
            var lines = linesGo.AddComponent<FieldLines>();
            lines.losLine = MakeLine(linesGo.transform, "LOSLine", new Color(0.2f, 0.45f, 1f));
            lines.firstDownLine = MakeLine(linesGo.transform, "FirstDownLine", new Color(1f, 0.9f, 0.1f));
            return field;
        }

        static LineRenderer MakeLine(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = 0.25f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        static Football BuildFootball()
        {
            var root = new GameObject("Football");
            root.layer = LBall;
            var ball = root.AddComponent<Football>(); // RequireComponent adds Rigidbody
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.09f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.name = "Mesh";
            mesh.transform.SetParent(visual.transform, false);
            mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // capsule long axis (Y) → +Z
            mesh.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.45f, 0.24f, 0.1f);
            AssetDatabase.CreateAsset(mat, "Assets/_Game/Art/Mat_Football.mat");
            mesh.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var trail = root.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 1f, 1f, 0.5f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.startWidth = 0.08f;
            trail.endWidth = 0.01f;
            trail.time = 0.3f;
            trail.emitting = false;

            ball.visual = visual.transform;
            ball.trail = trail;

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, "Assets/_Game/Prefabs/Football.prefab", InteractionMode.AutomatedAction);
            return ball;
        }

        static GameObject BuildCharacter(string name, Color tint, string controllerPath)
        {
            var root = new GameObject(name);
            root.layer = LPlayers;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.925f, 0f);
            capsule.radius = 0.35f;
            capsule.height = 1.85f;

            // Y Bot model child (only ONE model exists — instantiated per role and tinted, §13)
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Y Bot.fbx");
            GameObject model = null;
            if (fbx != null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (ctrl != null) animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false; // root motion baked into pose (§13)

                // Tint the body material per role
                var bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bodyMat.color = tint;
                AssetDatabase.CreateAsset(bodyMat, "Assets/_Game/Art/Mat_" + name + "_Body.mat");
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("body"))
                            mats[i] = bodyMat;
                    smr.sharedMaterials = mats;
                }

                var driver = model.AddComponent<CharacterAnimatorDriver>();
                driver.animator = animator;
            }
            else
            {
                // Capsule stand-in fallback (M0)
                var standIn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(standIn.GetComponent<Collider>());
                standIn.name = "Model";
                standIn.transform.SetParent(root.transform, false);
                standIn.transform.localPosition = new Vector3(0f, 0.925f, 0f);
                var driver = standIn.AddComponent<CharacterAnimatorDriver>();
                driver.animator = null;
            }

            var socket = root.AddComponent<BallSocket>();
            var fallback = new GameObject("HandSocket");
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localPosition = new Vector3(0.25f, 1.35f, 0.2f);
            socket.overrideSocket = fallback.transform;

            if (name != "QB")
            {
                var zoneGo = new GameObject("CatchZone");
                zoneGo.layer = LCatch;
                zoneGo.transform.SetParent(root.transform, false);
                zoneGo.transform.localPosition = new Vector3(0f, 1.3f, 0f); // chest height
                var sphere = zoneGo.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = name == "WR" ? 1.1f : 1.0f;
                zoneGo.AddComponent<CatchZone>();
            }
            return root;
        }

        static CameraDirector BuildCameras()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; // ≥50° vertical (§11.1)
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0f, 6f, -32f);

            var rig = new GameObject("CameraRig");
            var director = rig.AddComponent<CameraDirector>();
            director.camPreSnap = MakeVcam(rig.transform, "CamPreSnap");
            director.camQB = MakeVcam(rig.transform, "CamQB");
            director.camBall = MakeVcam(rig.transform, "CamBall");
            director.camResult = MakeVcam(rig.transform, "CamResult");
            return director;
        }

        static CinemachineCamera MakeVcam(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = 0;
            var lens = vcam.Lens;
            lens.FieldOfView = 55f;
            vcam.Lens = lens;
            return vcam;
        }

        static void BuildUI()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasGo.AddComponent<HUDController>();
            canvasGo.AddComponent<PlaySelectUI>();
            canvasGo.AddComponent<ResultBanner>();
            canvasGo.AddComponent<MenuController>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        static void WireEverything(FieldBounds field, Football ball, GameObject qbGo, GameObject wrGo, GameObject cbGo, CameraDirector director)
        {
            var mmGo = new GameObject("MatchManager");
            var mm = mmGo.AddComponent<MatchManager>();
            var resolver = mmGo.AddComponent<PlayResolver>();
            var audio = mmGo.AddComponent<AudioManager>();
            audio.source = mmGo.AddComponent<AudioSource>();

            // Components
            var qb = qbGo.AddComponent<QBController>();
            var launcher = qbGo.AddComponent<BallLauncher>();
            var routeRunner = wrGo.AddComponent<RouteRunner>();
            var wr = wrGo.AddComponent<WRController>();
            var cb = cbGo.AddComponent<CBController>();

            var qbDriver = qbGo.GetComponentInChildren<CharacterAnimatorDriver>();
            var wrDriver = wrGo.GetComponentInChildren<CharacterAnimatorDriver>();
            var cbDriver = cbGo.GetComponentInChildren<CharacterAnimatorDriver>();

            // QB
            qb.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            qb.launcher = launcher;
            qb.socket = qbGo.GetComponent<BallSocket>();
            qb.animDriver = qbDriver;
            launcher.ball = ball;
            launcher.qbSocket = qb.socket;
            launcher.wr = wr;
            launcher.animDriver = qbDriver;

            // WR
            wr.routeRunner = routeRunner;
            wr.catchZone = wrGo.GetComponentInChildren<CatchZone>();
            wr.socket = wrGo.GetComponent<BallSocket>();
            wr.animDriver = wrDriver;
            wr.cbTransform = cbGo.transform;
            wr.qbTransform = qbGo.transform;
            wr.ball = ball;
            wr.field = field;

            // CB
            cb.catchZone = cbGo.GetComponentInChildren<CatchZone>();
            cb.socket = cbGo.GetComponent<BallSocket>();
            cb.animDriver = cbDriver;
            cb.wr = wr;
            cb.ball = ball;
            cb.field = field;

            // MatchManager
            mm.rules = AssetDatabase.LoadAssetAtPath<MatchRules>("Assets/_Game/Data/MatchRules.asset");
            mm.difficulties = new[]
            {
                AssetDatabase.LoadAssetAtPath<CBDifficulty>("Assets/_Game/Data/Difficulty/Easy.asset"),
                AssetDatabase.LoadAssetAtPath<CBDifficulty>("Assets/_Game/Data/Difficulty/Normal.asset"),
                AssetDatabase.LoadAssetAtPath<CBDifficulty>("Assets/_Game/Data/Difficulty/Hard.asset")
            };
            mm.difficultyIndex = 1;
            string[] playOrder = { "Slant", "Out", "Dig", "Curl", "Comeback", "Post", "Corner", "Go" };
            mm.playbook = playOrder
                .Select(p => AssetDatabase.LoadAssetAtPath<PlayDefinition>("Assets/_Game/Data/Plays/Play_" + p + ".asset"))
                .Where(p => p != null).ToArray();

            mm.qb = qb;
            mm.wr = wr;
            mm.cb = cb;
            mm.ball = ball;
            mm.field = field;
            mm.resolver = resolver;
            mm.losMarker = GameObject.Find("LOSMarker").transform;
            mm.firstDownMarker = GameObject.Find("FirstDownMarker").transform;

            // Park entities sensibly pre-match
            qbGo.transform.position = new Vector3(0f, 0f, -24.36f);
            wrGo.transform.position = new Vector3(10f, 0f, -22.86f);
            cbGo.transform.position = new Vector3(10f, 0f, -18.29f);
            ball.transform.position = new Vector3(0f, 1f, -24f);
        }
    }
}