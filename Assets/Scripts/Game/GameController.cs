using System;
using System.IO;
using MedievalTown.Game.Art;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game
{
    public enum ToolKind { Inspect, Build, Clear }

    /// <summary>
    /// Owns the Town, runs the clock, handles mouse and keyboard, and saves/loads.
    /// Boots itself when you press Play, so the scene only needs to exist.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        public Town Town { get; private set; }
        public CameraRig Rig { get; private set; }
        public WorldView View { get; private set; }
        public Hud Hud { get; private set; }

        public int Speed { get; private set; }
        int lastSpeed = 1;
        public const float SecondsPerDay = 180f;   // three minutes a day at 1x
        public static readonly int[] Speeds = { 0, 1, 3, 10, 30 };

        public ToolKind Tool { get; private set; }
        public BuildingType BuildType { get; private set; }
        public Selection Selected { get; set; }

        /// <summary>Tile under the mouse.</summary>
        public Vector2Int HoverTile { get; private set; }
        public Vector2 HoverPixel { get; private set; }
        public bool PointerOnMap { get; private set; }

        string SavePath { get { return Path.Combine(Application.persistentDataPath, "medieval-town-save-v1.json"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Medieval Town").AddComponent<GameController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            Selected = Selection.None;

            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            Rig = cam.GetComponent<CameraRig>();
            if (Rig == null) Rig = cam.gameObject.AddComponent<CameraRig>();

            View = new GameObject("World").AddComponent<WorldView>();
            Hud = gameObject.AddComponent<Hud>();

            Town = Load() ?? Town.CreateNew(Environment.TickCount & 0x7fffffff);
            Hook(Town);
            View.Bind(Town, Rig);
            CenterOnTown();
            SetSpeed(1);
            SetTool(ToolKind.Inspect);
        }

#if UNITY_EDITOR
        // When scripts change while the game is running, Unity reloads them and the running game loses its town.
        // Save first, so pressing Play again carries on from here.
        void OnEnable() { UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += SaveBeforeReload; }
        void OnDisable() { UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= SaveBeforeReload; }
        void SaveBeforeReload() { if (Town != null) Save(); }
#endif

        void Hook(Town t)
        {
            t.DayEnded = day => { if (day % 7 == 1) Save(); };
        }

        public void NewTown()
        {
            Town = Town.CreateNew(Environment.TickCount & 0x7fffffff);
            Hook(Town);
            Selected = Selection.None;
            View.Bind(Town, Rig);
            CenterOnTown();
            SetSpeed(1);
            Save();
        }

        // ================================================================== controls
        public void SetSpeed(int s) { if (s > 0) lastSpeed = s; Speed = s; }
        public void TogglePause() { SetSpeed(Speed > 0 ? 0 : lastSpeed); }
        public void SetTool(ToolKind k, BuildingType t = BuildingType.Road) { Tool = k; BuildType = t; if (View != null) View.HideGhost(); }
        public void Select(Selection s) { Selected = s; }

        public void CenterOnTown()
        {
            float x = 0, y = 0; int n = 0;
            foreach (var b in Town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                x += (b.X + b.Def.W / 2f) * 16; y += (b.Y + b.Def.H / 2f) * 16; n++;
            }
            Rig.CenterOn(n > 0 ? new Vector2(x / n, y / n) : new Vector2(Town.Cols * 8, Town.Rows * 8));
        }

        public void CenterOn(Vector2 px) { Rig.CenterOn(px); }

        // ================================================================== loop
        bool dragging, painting, panning;
        Vector2 dragStart, lastMouse;

        void Update()
        {
            if (Town == null) return;
            if (Speed > 0) Town.Advance(Time.deltaTime / SecondsPerDay * Speed);
            HandleKeys();
            HandleMouse();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (Tool != ToolKind.Inspect) SetTool(ToolKind.Inspect); else Selected = Selection.None; Hud.CloseAll(); }
            if (Input.GetKeyDown(KeyCode.Q)) SetTool(ToolKind.Inspect);
            if (Input.GetKeyDown(KeyCode.R)) SetTool(ToolKind.Build, BuildingType.Road);
            if (Input.GetKeyDown(KeyCode.X)) SetTool(ToolKind.Clear);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(Speeds[1]);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(Speeds[2]);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(Speeds[3]);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeed(Speeds[4]);
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.Home)) CenterOnTown();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) Rig.ZoomAt(1, new Vector2(Screen.width / 2f, Screen.height / 2f));
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Rig.ZoomAt(-1, new Vector2(Screen.width / 2f, Screen.height / 2f));

            var pan = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan.x += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pan.y -= 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) pan.y += 1;
            float boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 3f : 1f;   // hold Shift to cross a big map quickly
            if (pan != Vector2.zero) Rig.PanPixels(pan * (520f / Rig.Zoom) * boost * Time.unscaledDeltaTime);
        }

        void HandleMouse()
        {
            Vector2 mouse = Input.mousePosition;
            bool overUi = Hud.Blocks(mouse);
            PointerOnMap = !overUi && mouse.x >= 0 && mouse.y >= 0 && mouse.x < Screen.width && mouse.y < Screen.height;
            var px = Rig.ScreenToPixel(mouse);
            HoverPixel = px;
            HoverTile = new Vector2Int(Mathf.FloorToInt(px.x / 16), Mathf.FloorToInt(px.y / 16));

            if (PointerOnMap && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) Rig.ZoomAt(Input.mouseScrollDelta.y > 0 ? 1 : -1, mouse);

            if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && PointerOnMap)
            {
                panning = true; lastMouse = mouse; dragStart = mouse;
            }
            if (panning && !dragging)
            {
                Rig.PanScreen(mouse - lastMouse); lastMouse = mouse;
                if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                {
                    panning = false;
                    // a right-click without dragging cancels the current tool
                    if (Vector2.Distance(mouse, dragStart) < 4 && Tool != ToolKind.Inspect) SetTool(ToolKind.Inspect);
                }
            }

            bool paintTool = Tool == ToolKind.Clear || (Tool == ToolKind.Build && BuildType == BuildingType.Road);
            if (Input.GetMouseButtonDown(0) && PointerOnMap)
            {
                dragStart = mouse; lastMouse = mouse;
                if (paintTool) { painting = true; Act(true); }
                else dragging = true;
            }
            if (painting)
            {
                if (Input.GetMouseButton(0)) { if (PointerOnMap) Act(false); }
                else painting = false;
            }
            if (dragging)
            {
                if (Input.GetMouseButton(0))
                {
                    if (!panning && Vector2.Distance(mouse, dragStart) > 6) { panning = true; lastMouse = mouse; }
                    if (panning) { Rig.PanScreen(mouse - lastMouse); lastMouse = mouse; }
                }
                else
                {
                    if (!panning && PointerOnMap) Act(true);
                    dragging = false; panning = false;
                }
            }
            UpdateGhost();
        }

        Vector2Int Anchor(BuildingType t)
        {
            var d = Defs.Get(t);
            if (t == BuildingType.Road) return HoverTile;
            return new Vector2Int(HoverTile.x - (d.W - 1) / 2, HoverTile.y - (d.H - 1) / 2);
        }

        public Vector2Int PlacementAnchor { get { return Anchor(BuildType); } }

        void UpdateGhost()
        {
            if (!PointerOnMap || (panning && !painting)) { View.HideGhost(); return; }
            if (Tool == ToolKind.Build)
            {
                var a = Anchor(BuildType);
                View.ShowGhost(BuildType, a.x, a.y, Town.CanPlace(BuildType, a.x, a.y) == null);
            }
            else if (Tool == ToolKind.Clear)
            {
                var b = Town.OccupantAt(HoverTile.x, HoverTile.y);
                byte t = Town.TerrainAt(HoverTile.x, HoverTile.y);
                if (b != null && b.Type != BuildingType.Cart && b.Type != BuildingType.Pavilion) View.ShowClearMarker(b.X, b.Y, b.Def.W, b.Def.H);
                else if (t == Tile.Tree || t == Tile.Rock || t == Tile.Sapling || t == Tile.Stump) View.ShowClearMarker(HoverTile.x, HoverTile.y, 1, 1);
                else View.HideGhost();
            }
            else View.HideGhost();
        }

        void Act(bool first)
        {
            int tx = HoverTile.x, ty = HoverTile.y;
            if (Tool == ToolKind.Inspect) { Selected = View.PickAt(HoverPixel); Hud.ResetConfirm(); return; }
            if (Tool == ToolKind.Clear)
            {
                var b = Town.OccupantAt(tx, ty);
                if (b != null)
                {
                    if (!first) return;   // tearing down buildings needs a deliberate click
                    if (b.Type == BuildingType.Cart || b.Type == BuildingType.Pavilion) { Hud.Toast(b.Type == BuildingType.Cart ? "The cart is unloaded once a Granary stands." : "The pavilion comes down when the Keep is finished."); return; }
                    Selected = Selection.Building(b.Id);
                    SetTool(ToolKind.Inspect);
                    Hud.Toast("Use Pull down in the panel to remove the " + b.Def.Name + ".");
                    return;
                }
                var err = Town.ClearTile(tx, ty);
                if (err != null && first) Hud.Toast(err);
                return;
            }
            var a = Anchor(BuildType);
            string error;
            var built = Town.Place(BuildType, a.x, a.y, out error);
            if (built == null)
            {
                if (first && !(BuildType == BuildingType.Road && error.Contains("taken"))) Hud.Toast(error);
            }
            else if (BuildType != BuildingType.Road) Selected = Selection.Building(built.Id);
        }

        // ================================================================== save / load
        public void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(Town)); }
            catch (Exception e) { Debug.LogWarning("Could not save the town: " + e.Message); }
        }

        Town Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                var t = JsonUtility.FromJson<Town>(File.ReadAllText(SavePath));
                if (t == null || t.Version < 1 || t.Version > Town.CurrentVersion || t.Terrain == null || t.Terrain.Length != Town.Cols * Town.Rows) return null;
                t.AfterLoad();
                return t;
            }
            catch (Exception e) { Debug.LogWarning("Could not load the saved town, starting fresh: " + e.Message); return null; }
        }

        void OnApplicationQuit() { if (Town != null) Save(); }
    }
}
