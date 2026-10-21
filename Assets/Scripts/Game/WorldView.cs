using System.Collections.Generic;
using System.Linq;
using MedievalTown.Game.Art;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game
{
    public enum SelectionKind { None, Building, Person }
    public struct Selection
    {
        public SelectionKind Kind; public int Id;
        public static readonly Selection None = new Selection { Kind = SelectionKind.None, Id = -1 };
        public static Selection Building(int id) { return new Selection { Kind = SelectionKind.Building, Id = id }; }
        public static Selection Person(int id) { return new Selection { Kind = SelectionKind.Person, Id = id }; }
    }

    /// <summary>
    /// Draws the manor with SpriteRenderers: ground, buildings and building sites, crops in the strips, the mill's sails,
    /// the lord's banners, trees, tents, villagers going about their chores, smoke, water sparkle, clouds, birds, dusk and night.
    /// Positions are worked out in map pixels (y down) and converted to world units.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        const int OrderGround = -32000, OrderBase = -31000, OrderCrops = -30990, OrderSmoke = 29000, OrderClouds = 29500,
                  OrderNight = 30000, OrderLights = 30010, OrderSelect = 31000, OrderGhost = 31010;

        Town town;
        CameraRig rig;
        Nav nav;
        GroundPainter painter;
        const int ChunkTiles = 32;
        // The ground is painted a chunk at a time, only near the camera: the map is far too big to keep painted whole.
        sealed class GroundChunk { public Texture2D Tex; public SpriteRenderer Sr; public int Tx0, Ty0, Tw, Th; public PixelCanvas Canvas; public int Season = -1; public int Seen; public bool Dirty; }
        readonly Dictionary<int, GroundChunk> chunks = new Dictionary<int, GroundChunk>();
        int chunkCols, chunkRows, frameNo;
        const int MaxChunks = 64;
        List<int> groundWatch, treeWatch;
        float time;

        sealed class BuildingView
        {
            public Building B; public GameObject Go; public SpriteRenderer Base, Top, Lights, Overlay, Pile, Door, Sails, Banner, SiteTimber, SiteStone;
            public int Season = -1, Stage = -2; public BuildingSprites Art; public BuildingArt.SiteSprites Site;
            public float DoorUntil, SailAngle;
        }
        sealed class PersonView
        {
            public Citizen P; public SpriteRenderer Sr, Held;
            public Vector2 Pos = new Vector2(float.NaN, 0), Goal = new Vector2(float.NaN, 0);
            public List<Vector2> Path = new List<Vector2>(); public int PathIdx;
            public int Dir; public float Step; public bool Moving, Inside;
            public int DoorState, DoorB = -1; public float DoorT; public int LastHammer, LastPhase = -1;
        }
        const int Out = 0, Entering = 1, In = 2, Exiting = 3;
        const float DoorTime = 0.8f;
        const int FxSmoke = 0, FxSmokeDark = 1, FxDust = 2, FxSpark = 3, FxChip = 4, FxGrit = 5, FxSoil = 6, FxHay = 7, FxSeed = 8, FxLeaf = 9, FxFlour = 10;
        static readonly Color32[] BitColors =
        {
            new Color32(216, 176, 122, 255), new Color32(168, 160, 148, 255), new Color32(94, 62, 36, 255), new Color32(224, 192, 112, 255),
            new Color32(232, 216, 144, 255), new Color32(106, 150, 62, 255)
        };
        sealed class Particle { public SpriteRenderer Sr; public float X, Y, Life, Max, Vx, Vy; public int Kind; }
        struct Job { public PeopleArt.Act Act; public int Dir; public Vector2 Spot; public bool Front; public Building B; public bool Inside; public bool Hammer; public Sprite Carry; }
        sealed class Cloud { public SpriteRenderer Sr; public float X, Y; public int Rx; }

        readonly Dictionary<int, BuildingView> buildings = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, SpriteRenderer> trees = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, byte> treeKinds = new Dictionary<int, byte>();
        readonly Dictionary<int, PersonView> people = new Dictionary<int, PersonView>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();
        readonly Dictionary<int, float> smokeTimers = new Dictionary<int, float>();
        readonly List<SpriteRenderer> sparkles = new List<SpriteRenderer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<SpriteRenderer> birds = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> tents = new List<SpriteRenderer>();
        readonly Dictionary<int, Vector2> tentSpot = new Dictionary<int, Vector2>();
        SpriteRenderer campfire;
        float birdX, birdY, birdVx; bool birdsActive;
        SpriteRenderer night, sunset;
        readonly SpriteRenderer[] selEdges = new SpriteRenderer[4];
        SpriteRenderer selMarker, ghostBase, ghostTop, ghostFoot;
        Transform layerBuildings, layerTrees, layerPeople, layerFx;
        int treeSeason = -1, treeMapVersion = -1;

        /// <summary>The lord's house colours, from the dynasty's name: field and charge.</summary>
        public static string[] HouseColours(Town t)
        {
            string[][] pal = { new[] { "#b5432f", "#e0b04a" }, new[] { "#2e4a8a", "#efe6d2" }, new[] { "#3e6a3a", "#e0b04a" }, new[] { "#6a2a6a", "#efe6d2" }, new[] { "#1e1e2a", "#c84a3a" }, new[] { "#e0b04a", "#7a1e1e" } };
            int h = 0; foreach (char c in t.Dynasty ?? "") h = h * 31 + c;
            return pal[(h & 0x7fffffff) % pal.Length];
        }

        // ================================================================== setup
        public void Bind(Town t, CameraRig cameraRig)
        {
            town = t; rig = cameraRig; nav = new Nav(t);
            PeopleArt.WorkplaceOf = p => { var w = town.GetBuilding(p.WorkId); return w != null ? w.Type : (BuildingType?)null; };
            PeopleArt.Rules = p => p.Id == town.KingId || p.Id == town.RegentId;
            foreach (Transform c in transform) Destroy(c.gameObject);
            buildings.Clear(); trees.Clear(); treeKinds.Clear(); people.Clear(); particles.Clear(); particlePool.Clear(); smokeTimers.Clear();
            sparkles.Clear(); clouds.Clear(); birds.Clear(); tents.Clear(); tentSpot.Clear();
            treeSeason = -1; treeMapVersion = -1;
            if (town != null) { if (groundWatch != null) town.UnwatchTiles(groundWatch); if (treeWatch != null) town.UnwatchTiles(treeWatch); }

            layerBuildings = Layer("Buildings"); layerTrees = Layer("Trees"); layerPeople = Layer("Folk"); layerFx = Layer("Effects");
            painter = new GroundPainter(town);
            foreach (var c in chunks.Values) if (c.Tex != null) Destroy(c.Tex);
            chunks.Clear();
            chunkCols = (Town.Cols + ChunkTiles - 1) / ChunkTiles; chunkRows = (Town.Rows + ChunkTiles - 1) / ChunkTiles;
            groundWatch = town.WatchTiles(); treeWatch = town.WatchTiles();

            night = MakeSr("Night", layerFx, NatureArt.White, OrderNight);
            sunset = MakeSr("Sunset", layerFx, NatureArt.White, OrderNight - 1);
            for (int i = 0; i < 4; i++) selEdges[i] = MakeSr("Selection", layerFx, NatureArt.White, OrderSelect);
            selMarker = MakeSr("Marker", layerFx, NatureArt.White, OrderSelect);
            ghostBase = MakeSr("GhostBase", layerFx, null, OrderGhost);
            ghostTop = MakeSr("GhostTop", layerFx, null, OrderGhost + 1);
            ghostFoot = MakeSr("GhostFoot", layerFx, NatureArt.White, OrderGhost + 2);
            campfire = MakeSr("Campfire", layerBuildings, PropArt.Campfire(0), 0); campfire.enabled = false;
            for (int i = 0; i < 160; i++) { var s = MakeSr("Sparkle", layerFx, NatureArt.White, OrderBase + 1); s.color = new Color32(214, 238, 250, 255); s.enabled = false; sparkles.Add(s); }
            for (int i = 0; i < Mathf.Clamp(Town.Cols * Town.Rows / 560, 5, 70); i++)
            {
                int rx = Random.Range(40, 90), ry = Random.Range(18, 34);
                clouds.Add(new Cloud { Sr = MakeSr("Cloud", layerFx, CloudSprite(rx, ry), OrderClouds), X = Random.Range(0f, Town.Cols * 16f), Y = Random.Range(0f, Town.Rows * 16f), Rx = rx });
            }
            for (int i = 0; i < 5; i++) { var b = MakeSr("Bird", layerFx, NatureArt.White, OrderClouds + 1); b.enabled = false; birds.Add(b); }
            HideGhost();
        }

        Transform Layer(string n) { var go = new GameObject(n); go.transform.SetParent(transform, false); return go.transform; }

        static SpriteRenderer MakeSr(string name, Transform parent, Sprite s, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order;
            return sr;
        }

        static void Place(Component c, float px, float py, float w = 1f, float h = 1f)
        {
            c.transform.position = Pixels.ToWorld(px, py);
            c.transform.localScale = new Vector3(w, h, 1f);
        }

        static int SortOrder(float footPy, int bias = 0) { return Mathf.RoundToInt(footPy) * 4 + bias; }

        static Sprite CloudSprite(int rx, int ry)
        {
            var g = new PixelCanvas(rx * 2 + 2, ry * 2 + 2);
            var c = new Color32(18, 28, 48, 18);
            for (int dy = -ry; dy <= ry; dy += 2)
            {
                int hw = Mathf.RoundToInt(rx * Mathf.Sqrt(Mathf.Max(0, 1 - (dy / (float)ry) * (dy / (float)ry))) / 2) * 2;
                if (hw > 0) g.R(rx - hw + 1, dy + ry + 1, hw * 2, 2, c);
            }
            return g.ToSprite("cloud");
        }

        // ================================================================== frame
        void LateUpdate()
        {
            if (town == null) return;
            time += Time.deltaTime;
            int si = town.Season;
            SyncGround(si);
            SyncBuildings(si);
            SyncTrees(si);
            SyncTents(si);
            SyncPeople();
            UpdateEffects(si);
            UpdateNight();
            UpdateSelection();
            UpdateZones();
        }

        /// <summary>Tiles in view, with a margin, as a rect of tile coordinates.</summary>
        RectInt ViewTiles(int margin)
        {
            var o = rig.ViewOrigin; var v = rig.ViewSize;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(o.x / 16) - margin), y0 = Mathf.Max(0, Mathf.FloorToInt(o.y / 16) - margin);
            int x1 = Mathf.Min(Town.Cols - 1, Mathf.CeilToInt((o.x + v.x) / 16) + margin), y1 = Mathf.Min(Town.Rows - 1, Mathf.CeilToInt((o.y + v.y) / 16) + margin);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0 + 1), Mathf.Max(0, y1 - y0 + 1));
        }

        void SyncGround(int si)
        {
            frameNo++;
            // tiles that changed: repaint them in the chunks that are loaded (the rest are painted fresh when they come into view)
            if (groundWatch.Count >= 40000) { foreach (var c in chunks.Values) c.Season = -1; groundWatch.Clear(); }
            if (groundWatch.Count > 0)
            {
                var done = new HashSet<int>();
                foreach (int k in groundWatch)
                {
                    if (!done.Add(k)) continue;
                    int tx = k % Town.Cols, ty = k / Town.Cols;
                    for (int cy = (ty - 1) / ChunkTiles; cy <= (ty + 1) / ChunkTiles; cy++)
                        for (int cx = (tx - 1) / ChunkTiles; cx <= (tx + 1) / ChunkTiles; cx++)
                        {
                            GroundChunk c;
                            if (cx < 0 || cy < 0 || !chunks.TryGetValue(cy * chunkCols + cx, out c) || c.Season != si) continue;
                            painter.PaintAround(c.Canvas, c.Tx0, c.Ty0, tx, ty, si); c.Dirty = true;
                        }
                }
                groundWatch.Clear();
                foreach (var c in chunks.Values) if (c.Dirty) { Upload(c); c.Dirty = false; }
            }
            // the chunks in view: a new one is painted at once; one from another season is repainted a few a frame
            var vt = ViewTiles(4);
            int budget = 3;
            for (int cy = vt.yMin / ChunkTiles; cy <= (vt.yMax - 1) / ChunkTiles; cy++)
                for (int cx = vt.xMin / ChunkTiles; cx <= (vt.xMax - 1) / ChunkTiles; cx++)
                {
                    int key = cy * chunkCols + cx;
                    GroundChunk c;
                    if (!chunks.TryGetValue(key, out c)) { c = NewChunk(cx, cy); chunks[key] = c; }
                    c.Seen = frameNo;
                    if (c.Season == si) continue;
                    if (c.Season >= 0 && budget-- <= 0) continue;
                    painter.PaintChunk(c.Canvas, c.Tx0, c.Ty0, c.Tw, c.Th, si);
                    c.Season = si; Upload(c); c.Sr.enabled = true;
                }
            // forget the chunks longest out of view
            int inView = ((vt.xMax - 1) / ChunkTiles - vt.xMin / ChunkTiles + 1) * ((vt.yMax - 1) / ChunkTiles - vt.yMin / ChunkTiles + 1);
            int keep = Mathf.Max(MaxChunks, inView + 24);
            if (chunks.Count > keep)
            {
                foreach (var kv in chunks.OrderBy(kv => kv.Value.Seen).Take(chunks.Count - keep).ToList())
                {
                    if (kv.Value.Seen == frameNo) break;
                    Destroy(kv.Value.Sr.gameObject); Destroy(kv.Value.Tex); chunks.Remove(kv.Key);
                }
            }
        }

        GroundChunk NewChunk(int cx, int cy)
        {
            var c = new GroundChunk { Tx0 = cx * ChunkTiles, Ty0 = cy * ChunkTiles };
            c.Tw = Mathf.Min(ChunkTiles, Town.Cols - c.Tx0); c.Th = Mathf.Min(ChunkTiles, Town.Rows - c.Ty0);
            int w = c.Tw * 16, h = c.Th * 16;
            c.Canvas = new PixelCanvas(w, h);
            c.Tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Ground" };
            var gs = Sprite.Create(c.Tex, new Rect(0, 0, w, h), new Vector2(0, 1), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            c.Sr = MakeSr("Ground", transform, gs, OrderGround);
            c.Sr.transform.position = Pixels.ToWorld(c.Tx0 * 16, c.Ty0 * 16);
            c.Sr.enabled = false;
            return c;
        }

        void Upload(GroundChunk c) { c.Tex.SetPixels32(c.Canvas.Px); c.Tex.Apply(false); }

        // ---------------------------------------------------------------- buildings
        public BuildingSprites ArtFor(Building b) { return BuildingArt.Get(b.Type, BuildingArt.VariantOf(b), town.Season); }

        bool Lit(Building b)
        {
            if (!b.Built) return false;
            float t = town.TimeOfDay;
            switch (b.Type)
            {
                case BuildingType.Cottage: case BuildingType.FreeHouse: return b.Residents.Count > 0;
                case BuildingType.Keep: case BuildingType.Pavilion: case BuildingType.StewardHall: case BuildingType.Manor: return b.Residents.Count > 0;
                case BuildingType.Church: return town.Priest != null && (t > 0.78f || town.IsSunday);
                case BuildingType.Alehouse: return b.Employees.Count > 0 && !town.IsSunday && (t > 0.68f || t < 0.1f);
                case BuildingType.Mill: return b.Employees.Count > 0 && t > 0.75f && t < 0.86f;
                default: return false;
            }
        }

        static Good? PileGood(Building b)
        {
            if (b.Type == BuildingType.Granary)
            {
                int best = -1; float bv = 0.5f;
                for (int g = 0; g < Balance.GoodCount; g++) if (b.Stock[g] > bv) { bv = b.Stock[g]; best = g; }
                return best < 0 ? (Good?)null : (Good)best;
            }
            switch (b.Type)
            {
                case BuildingType.Woodcutter: return Good.Timber;
                case BuildingType.Quarry: return Good.Stone;
                case BuildingType.Strip: case BuildingType.Demesne: return Good.Grain;
            }
            return null;
        }

        float PileAmount(Building b, Good g)
        {
            switch (b.Type)
            {
                case BuildingType.Granary: return b.Stock[(int)g] / 120f;
                case BuildingType.Woodcutter: return town.StoreStock(Good.Timber) / 100f;
                case BuildingType.Quarry: return town.StoreStock(Good.Stone) / 100f;
                case BuildingType.Strip: case BuildingType.Demesne:
                    return town.Season == 2 && b.Harvested > 0 && b.CropLeft > 0.5f ? 0.6f : 0f;   // sheaves waiting to be carted
            }
            return 0;
        }

        /// <summary>Whether the mill's sails are turning: a miller at work, by day.</summary>
        bool Grinding(Building b) { return b.Built && b.Employees.Count > 0 && town.TimeOfDay > 0.26f && town.TimeOfDay < 0.74f && !town.IsSunday; }

        void SyncBuildings(int si)
        {
            var alive = new HashSet<int>();
            float n = NightAlpha();
            int frame = Mathf.FloorToInt(time * 1.1f) % 2;
            var colours = HouseColours(town);
            PeopleArt.HouseColour = colours[0];
            foreach (var b in town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                alive.Add(b.Id);
                BuildingView v;
                if (!buildings.TryGetValue(b.Id, out v)) { v = CreateBuildingView(b); buildings[b.Id] = v; }
                var d = b.Def;
                int stage = b.Built ? -1 : BuildingArt.SiteStep(b.BuildFraction);
                if (v.Season != si || v.Stage != stage)
                {
                    if (v.Stage >= 0 && stage < 0) Burst(b);
                    v.Art = ArtFor(b); v.Season = si; v.Stage = stage;
                    if (stage >= 0)
                    {
                        v.Site = BuildingArt.Site(b.Type, BuildingArt.VariantOf(b), si, stage);
                        v.Base.sprite = v.Site.Base; v.Top.sprite = v.Site.Top;
                    }
                    else { v.Site = null; v.Top.sprite = v.Art.Top; }
                    v.Lights.sprite = v.Art.Lights;
                }
                if (v.SiteTimber != null)
                {
                    SitePile(v.SiteTimber, b, Good.Timber, d.Timber, si);
                    SitePile(v.SiteStone, b, Good.Stone, d.Stone, si);
                }
                if (v.Door != null)
                {
                    bool open = b.Built && v.Art.HasDoor && time < v.DoorUntil;
                    v.Door.enabled = open;
                    if (open) v.Door.sprite = Lit(b) && n > 0.25f ? v.Art.DoorOpenLit : v.Art.DoorOpen;
                }
                if (stage < 0) v.Base.sprite = v.Art.Base[frame];
                bool lit = Lit(b) && n > 0.02f;
                v.Lights.enabled = lit;
                if (lit) v.Lights.color = new Color(1, 1, 1, Mathf.Clamp01(n * 1.6f));
                // crops in the fields
                if (v.Overlay != null)
                {
                    v.Overlay.enabled = b.Built;
                    if (b.Built)
                    {
                        int st = PropArt.FieldStage(b, si);
                        float tot = b.CropLeft + b.Harvested;
                        v.Overlay.sprite = PropArt.FieldCrops(st, frame, tot > 0 ? b.Harvested / tot : 0, d.W * 16, d.H * 16, b.Type == BuildingType.Strip);
                    }
                }
                if (v.Pile != null)
                {
                    var g = b.Built ? PileGood(b) : null;
                    int lvl = g.HasValue ? PropArt.PileLevel(PileAmount(b, g.Value), 1f) : 0;
                    v.Pile.enabled = lvl > 0;
                    if (lvl > 0) v.Pile.sprite = PropArt.Pile(g.Value, lvl, si);
                }
                if (v.Sails != null)
                {
                    v.Sails.enabled = b.Built;
                    if (Grinding(b)) v.SailAngle += Time.deltaTime * 2.4f * Mathf.Min(3, Mathf.Max(1, GameController.Instance != null ? GameController.Instance.Speed : 1));
                    int f = Mathf.FloorToInt(v.SailAngle) % 4;
                    v.Sails.sprite = v.Art.Sails[(f + 4) % 4];
                }
                if (v.Banner != null)
                {
                    v.Banner.enabled = b.Built;
                    v.Banner.sprite = PropArt.Pennant(Mathf.FloorToInt(time * 3 + b.Id) % 4, colours[0], colours[1]);
                }
            }
            if (alive.Count != buildings.Count)
            {
                var gone = new List<int>();
                foreach (var kv in buildings) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { Destroy(buildings[id].Go); buildings.Remove(id); smokeTimers.Remove(id); }
            }
        }

        static void SitePile(SpriteRenderer sr, Building b, Good g, float total, int si)
        {
            float left = b.Built || total <= 0 ? 0 : b.Delivered[(int)g] - b.BuildFraction * total;
            int lvl = PropArt.PileLevel(left, Mathf.Max(8f, total * 0.6f));
            sr.enabled = lvl > 0;
            if (lvl > 0) sr.sprite = PropArt.Pile(g, lvl, si);
        }

        void Burst(Building b)
        {
            var d = b.Def; float foot = (b.Y + d.H) * 16, top = b.Y * 16 - BuildingArt.LiftOf(b.Type);
            for (int i = 0; i < 26; i++)
                SpawnFx(b.X * 16 + Random.value * d.W * 16, foot - Random.value * 8, FxDust, (Random.value - 0.5f) * 30, 0.9f + Random.value * 0.9f);
            for (int i = 0; i < 10; i++)
                SpawnFx(b.X * 16 + 2 + Random.value * (d.W * 16 - 4), top + Random.value * 14, FxSpark, (Random.value - 0.5f) * 10, 0.8f + Random.value * 0.6f);
        }

        void OpenDoor(Building b)
        {
            BuildingView v;
            if (b != null && buildings.TryGetValue(b.Id, out v)) v.DoorUntil = Mathf.Max(v.DoorUntil, time + 0.35f);
        }

        BuildingView CreateBuildingView(Building b)
        {
            var d = b.Def;
            var v = new BuildingView { B = b, Art = ArtFor(b) };
            v.Go = new GameObject(d.Name + " #" + b.Id);
            v.Go.transform.SetParent(layerBuildings, false);
            v.Go.transform.position = Pixels.ToWorld(b.X * 16, b.Y * 16 - v.Art.Lift);
            int foot = (b.Y + d.H) * 16;
            v.Base = MakeSr("Base", v.Go.transform, v.Art.Base[0], OrderBase);
            v.Top = MakeSr("Top", v.Go.transform, v.Art.Top, SortOrder(foot));
            v.Lights = MakeSr("Lights", v.Go.transform, v.Art.Lights, OrderLights);
            if (d.IsField)
            {
                v.Overlay = MakeSr("Crops", v.Go.transform, null, OrderCrops);
                v.Overlay.transform.position = Pixels.ToWorld(b.X * 16, b.Y * 16);
            }
            if (v.Art.PileAt.x >= 0)
            {
                v.Pile = MakeSr("Pile", v.Go.transform, null, SortOrder(foot, 1));
                v.Pile.transform.position = Pixels.ToWorld(b.X * 16 + v.Art.PileAt.x, b.Y * 16 - v.Art.Lift + v.Art.PileAt.y);
            }
            if (v.Art.Sails != null)
            {
                v.Sails = MakeSr("Sails", v.Go.transform, v.Art.Sails[0], SortOrder(foot, 2));
                v.Sails.transform.position = Pixels.ToWorld(b.X * 16 + v.Art.SailHub.x, b.Y * 16 - v.Art.Lift + v.Art.SailHub.y);
            }
            if (v.Art.BannerAt.x >= 0)
            {
                v.Banner = MakeSr("Banner", v.Go.transform, null, SortOrder(foot, 2));
                v.Banner.transform.position = Pixels.ToWorld(b.X * 16 + v.Art.BannerAt.x, b.Y * 16 - v.Art.Lift + v.Art.BannerAt.y);
            }
            if (b.Type != BuildingType.Cart && b.Type != BuildingType.Pavilion)
            {
                v.Door = MakeSr("Door", v.Go.transform, null, SortOrder(foot, 1)); v.Door.enabled = false;
                v.SiteTimber = MakeSr("SiteTimber", v.Go.transform, null, SortOrder(foot, 1));
                v.SiteTimber.transform.position = Pixels.ToWorld(b.X * 16 + 1, foot);
                v.SiteStone = MakeSr("SiteStone", v.Go.transform, null, SortOrder(foot, 1));
                v.SiteStone.transform.position = Pixels.ToWorld(b.X * 16 + Mathf.Max(d.W * 16 - 27, 8), foot);
            }
            else if (b.Type == BuildingType.Pavilion) { v.Door = MakeSr("Door", v.Go.transform, null, SortOrder(foot, 1)); v.Door.enabled = false; }
            return v;
        }

        // ---------------------------------------------------------------- trees
        static bool IsGrowth(byte t) { return t == Tile.Tree || t == Tile.Rock || t == Tile.Sapling || t == Tile.Stump; }

        RectInt treeRect;
        /// <summary>Trees, saplings, stumps and boulders are sprites only near the camera.</summary>
        void SyncTrees(int si)
        {
            var vt = ViewTiles(6);
            bool moved = !treeRect.Contains(new Vector2Int(vt.xMin, vt.yMin)) || !treeRect.Contains(new Vector2Int(vt.xMax - 1, vt.yMax - 1));
            bool reseason = treeSeason != si;
            if (!reseason && !moved && treeWatch.Count == 0) return;
            if (moved || reseason || treeWatch.Count >= 40000)
            {
                // a fresh window, a little bigger than the view so small pans don't rebuild it
                treeRect = ViewTiles(18);
                treeWatch.Clear();
                var want = new HashSet<int>();
                for (int ty = treeRect.yMin; ty < treeRect.yMax; ty++)
                    for (int tx = treeRect.xMin; tx < treeRect.xMax; tx++)
                        if (IsGrowth(town.TerrainAt(tx, ty))) { int k = ty * Town.Cols + tx; want.Add(k); TreeAt(tx, ty, si, reseason, false); }
                var gone = new List<int>();
                foreach (var kv in trees) if (!want.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int k in gone) { Destroy(trees[k].gameObject); trees.Remove(k); treeKinds.Remove(k); }
            }
            else
            {
                foreach (int k in treeWatch)
                {
                    int tx = k % Town.Cols, ty = k / Town.Cols;
                    if (!treeRect.Contains(new Vector2Int(tx, ty))) continue;
                    if (IsGrowth(town.TerrainAt(tx, ty))) TreeAt(tx, ty, si, false, true);
                    else if (trees.ContainsKey(k))
                    {
                        byte was;
                        if (treeKinds.TryGetValue(k, out was) && was == Tile.Rock) Broken(tx, ty);
                        Destroy(trees[k].gameObject); trees.Remove(k); treeKinds.Remove(k);
                    }
                }
                treeWatch.Clear();
            }
            treeSeason = si; treeMapVersion = town.MapVersion;
        }

        void TreeAt(int tx, int ty, int si, bool reseason, bool live)
        {
            byte t = town.TerrainAt(tx, ty);
            int k = ty * Town.Cols + tx;
            SpriteRenderer sr; byte was;
            bool exists = trees.TryGetValue(k, out sr);
            if (exists && !reseason && treeKinds.TryGetValue(k, out was) && was == t) return;
            var pl = NatureArt.At(tx, ty, t, si);
            if (live && exists && treeKinds.TryGetValue(k, out was) && was == Tile.Tree && t == Tile.Stump) Felled(tx, ty, si);
            if (!exists) { sr = MakeSr("Growth", layerTrees, pl.Sprite, 0); trees[k] = sr; }
            sr.sprite = pl.Sprite;
            sr.transform.position = Pixels.ToWorld(pl.Px, pl.Py);
            sr.sortingOrder = SortOrder(pl.Py);
            treeKinds[k] = t;
        }

        void Felled(int tx, int ty, int si)
        {
            float x = tx * 16 + 8, y = ty * 16 + 14;
            for (int i = 0; i < 8; i++) SpawnFx(x + (Random.value - 0.5f) * 14, y - Random.value * 4, FxDust, (Random.value - 0.5f) * 30, 0.8f + Random.value * 0.6f);
            Bits(x, y - 4, FxChip, 6, 0, 40, 30);
            if (si != 3) for (int i = 0; i < 6; i++) SpawnFx(x + (Random.value - 0.5f) * 16, y - 10 - Random.value * 10, FxLeaf, (Random.value - 0.5f) * 10, 1.4f + Random.value, 6);
        }

        void Broken(int tx, int ty)
        {
            float x = tx * 16 + 8, y = ty * 16 + 12;
            for (int i = 0; i < 6; i++) SpawnFx(x + (Random.value - 0.5f) * 12, y - Random.value * 4, FxDust, (Random.value - 0.5f) * 24, 0.8f + Random.value * 0.5f);
            Bits(x, y - 2, FxGrit, 8, 0, 40, 34);
        }

        // ---------------------------------------------------------------- tents for those without a roof
        Vector2 CampCenter()
        {
            var seat = town.Seat ?? town.Buildings.FirstOrDefault(b => b.Type == BuildingType.Cart);
            if (seat == null) return new Vector2(Town.WagonX * 16, (Town.TrailRow - 6) * 16);
            return new Vector2(seat.X * 16 + seat.Def.W * 8 + 40, (seat.Y + seat.Def.H) * 16 + 12);
        }

        void SyncTents(int si)
        {
            var heads = town.People.Where(p => p.HomeId < 0).GroupBy(p => town.HouseholdOf(p)).Select(g => g.Key).ToList();
            tentSpot.Clear();
            var center = CampCenter();
            int ctx = Mathf.FloorToInt(center.x / 16), cty = Mathf.FloorToInt(center.y / 16);
            var spots = new List<Vector2Int>();
            for (int r = 1; r < 8 && spots.Count < heads.Count; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = ctx + dx, y = cty + dy;
                        byte t = town.TerrainAt(x, y);
                        if (t != Tile.Grass || town.OccupantAt(x, y) != null || town.OccupantAt(x, y + 1) != null) continue;
                        if (spots.Count < heads.Count) spots.Add(new Vector2Int(x, y));
                    }
            for (int i = 0; i < heads.Count; i++)
            {
                if (i >= tents.Count) tents.Add(MakeSr("Tent", layerBuildings, null, 0));
                var sr = tents[i];
                if (i >= spots.Count) { sr.enabled = false; continue; }
                var s = spots[i];
                float px = s.x * 16 + 8, py = s.y * 16 + 14;
                sr.enabled = true; sr.sprite = PropArt.Tent(heads[i], si);
                sr.transform.position = Pixels.ToWorld(px, py); sr.sortingOrder = SortOrder(py);
                tentSpot[heads[i]] = new Vector2(px, py + 3);
            }
            for (int i = heads.Count; i < tents.Count; i++) tents[i].enabled = false;
            campfire.enabled = heads.Count > 0;
            if (campfire.enabled)
            {
                campfire.sprite = PropArt.Campfire(Mathf.FloorToInt(time * 6) % 3);
                campfire.transform.position = Pixels.ToWorld(center.x, center.y + 4);
                campfire.sortingOrder = SortOrder(center.y + 4);
            }
        }

        // ---------------------------------------------------------------- folk
        Vector2 Front(Building b, Citizen p, float spread = 1f)
        {
            var d = b.Def; float a = p.Seed, c = (p.Seed * 7.31f) % 1f;
            return new Vector2(b.X * 16 + 4 + a * (d.W * 16 - 8) * spread + (1 - spread) * (d.W * 8 - 4), (b.Y + d.H) * 16 + 3 + c * 4);
        }

        /// <summary>A walkable tile beside water near a building, and which way to face it (x, y, direction); x is -1 if none.</summary>
        Vector3Int NearestWater(Building b, int salt)
        {
            var d = b.Def; var found = new List<Vector3Int>();
            for (int y = b.Y - 4; y < b.Y + d.H + 4; y++)
                for (int x = b.X - 4; x < b.X + d.W + 4; x++)
                {
                    byte tt = town.TerrainAt(x, y);
                    if ((tt != Tile.Grass && tt != Tile.Stump) || town.OccupantAt(x, y) != null) continue;
                    if (town.TerrainAt(x + 1, y) == Tile.Water) found.Add(new Vector3Int(x, y, PeopleArt.Right));
                    else if (town.TerrainAt(x - 1, y) == Tile.Water) found.Add(new Vector3Int(x, y, PeopleArt.Left));
                    else if (town.TerrainAt(x, y + 1) == Tile.Water) found.Add(new Vector3Int(x, y, PeopleArt.Down));
                    else if (town.TerrainAt(x, y - 1) == Tile.Water) found.Add(new Vector3Int(x, y, PeopleArt.Up));
                }
            if (found.Count == 0) return new Vector3Int(-1, -1, 0);
            return found[Mathf.Abs(salt) % found.Count];
        }

        bool HasDoor(Building b) { return b != null && b.Built && ArtFor(b).HasDoor; }

        Vector2 DoorStep(Building b, Citizen p)
        {
            var art = ArtFor(b); var r = art.Door; float foot = (b.Y + b.Def.H) * 16;
            return new Vector2(b.X * 16 + r.x + r.width / 2f + (p.Seed - 0.5f) * 2, Mathf.Max(foot + 1, b.Y * 16 - art.Lift + r.y + r.height + 2));
        }
        float DoorInsideY(Building b) { var art = ArtFor(b); var r = art.Door; return b.Y * 16 - art.Lift + r.y + r.height - 1; }

        /// <summary>The nearest tree to a point (map tiles), within a radius.</summary>
        int NearestTree(int cx, int cy, int r, int salt)
        {
            int best = -1; float bd = float.MaxValue;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    byte t = town.TerrainAt(x, y);
                    if (t != Tile.Tree) continue;
                    float dd = (x - cx) * (x - cx) + (y - cy) * (y - cy) + Noise.Hash(x, y, salt) * 6;
                    if (dd < bd) { bd = dd; best = y * Town.Cols + x; }
                }
            return best;
        }

        /// <summary>Where someone does their chore and what it looks like.</summary>
        Job JobFor(Citizen p, Building t, float ph)
        {
            var j = new Job { Act = PeopleArt.Act.None, Dir = PeopleArt.Down, B = t };
            float wob = Mathf.Floor(time / 6f + p.Seed * 10);
            int si = town.Season;
            switch (p.Chore)
            {
                case Chore.OwnStrip: case Chore.WeekWork: case Chore.Labour:
                    if (t != null && t.Def.IsField)
                    {
                        // working along a furrow a step at a time; the season says what the work is
                        int w = t.Def.W * 16, h = t.Def.H * 16;
                        float prog = time * 0.9f + p.Seed * (w - 14) + p.Id * 7f;
                        int lap = Mathf.FloorToInt(prog / (w - 14));
                        int rows = (h - 8) / 4;
                        int row = Mathf.FloorToInt(Noise.Hash(p.Id, lap, 2) * rows);
                        j.Spot = new Vector2(t.X * 16 + 7 + (prog - lap * (w - 14)), t.Y * 16 + 8 + row * 4 + 3);
                        j.Dir = PeopleArt.Right;
                        int stage = PropArt.FieldStage(t, si);
                        if (si == 2) j.Act = stage == 4 ? PeopleArt.Act.Scythe : PeopleArt.Act.Fork;
                        else if (si == 0 && t.CropGrowth < 0.25f && p.Id % 2 == 0) j.Act = PeopleArt.Act.Sow;
                        else j.Act = PeopleArt.Act.Hoe;
                        return j;
                    }
                    if (t != null && t.Type == BuildingType.Quarry) goto case Chore.Trade;
                    if (t != null && t.Type == BuildingType.Woodcutter) goto case Chore.Trade;
                    // week-work with nothing in the fields: gathering wood for the lord near the granary
                    {
                        var g = town.Stores.FirstOrDefault() ?? town.Seat;
                        int cx = g != null ? g.X : Town.WagonX, cy = g != null ? g.Y : Town.WagonY;
                        int tree = NearestTree(cx, cy, 14, p.Id % 5);
                        if (tree >= 0) { j.Spot = new Vector2(tree % Town.Cols * 16 + 8 - 9, tree / Town.Cols * 16 + 15); j.Dir = PeopleArt.Right; j.Act = PeopleArt.Act.Chop; return j; }
                    }
                    break;
                case Chore.Gather:
                {
                    var home = town.HomeOf(p);
                    int cx = home != null ? home.X : Town.WagonX, cy = home != null ? home.Y : Town.WagonY;
                    int tree = NearestTree(cx, cy, 12, p.Id % 7 + (int)wob % 3);
                    if (tree >= 0) { j.Spot = new Vector2(tree % Town.Cols * 16 + 11, tree / Town.Cols * 16 + 18); j.Dir = PeopleArt.Up; j.Act = PeopleArt.Act.Pluck; return j; }
                    break;
                }
                case Chore.Build:
                    if (t != null) { j.Spot = Front(t, p); j.Dir = PeopleArt.Up; j.Hammer = true; return j; }
                    break;
                case Chore.Trade:
                    if (t == null) break;
                    switch (t.Type)
                    {
                        case BuildingType.Woodcutter:
                        {
                            int tree = t.TargetTile >= 0 && town.TerrainAt(t.TargetTile % Town.Cols, t.TargetTile / Town.Cols) == Tile.Tree ? t.TargetTile : NearestTree(t.X, t.Y, t.Def.Radius, 1);
                            int idx = t.Employees.IndexOf(p.Id);
                            if (tree >= 0 && idx != 1)
                            {
                                int side = (idx < 0 ? p.Id : idx) % 2 == 0 ? -1 : 1;
                                j.Spot = new Vector2(tree % Town.Cols * 16 + 8 + side * 9, tree / Town.Cols * 16 + 15); j.Dir = side < 0 ? PeopleArt.Right : PeopleArt.Left; j.Act = PeopleArt.Act.Chop;
                                return j;
                            }
                            j.Spot = new Vector2(t.X * 16 + 13, t.Y * 16 + 27); j.Dir = PeopleArt.Right; j.Act = PeopleArt.Act.Maul; j.Front = true;   // splitting at the block
                            return j;
                        }
                        case BuildingType.Quarry:
                            j.Spot = new Vector2(t.X * 16 + 10 + (p.Id % 3) * 6, t.Y * 16 + 16 + (p.Id % 2) * 2); j.Dir = PeopleArt.Up; j.Act = PeopleArt.Act.Pick;
                            return j;
                        case BuildingType.Mill: case BuildingType.Alehouse: case BuildingType.MerchantHouse:
                            j.Inside = true; return j;
                        case BuildingType.Pasture: case BuildingType.Sheepfold:
                        {
                            // wandering among the beasts, a different spot every little while
                            float w = t.Def.W * 16, h = t.Def.H * 16;
                            int beat = Mathf.FloorToInt(time / 9f + p.Seed * 5);
                            float fx = Mathf.Repeat(beat * 0.37f + p.Seed, 1f), fy = Mathf.Repeat(beat * 0.61f + p.Seed * 3, 1f);
                            j.Spot = new Vector2(t.X * 16 + 8 + fx * (w - 16), t.Y * 16 + 12 + fy * (h - 16));
                            j.Dir = beat % 2 == 0 ? PeopleArt.Left : PeopleArt.Right;
                            j.Act = t.Type == BuildingType.Sheepfold && town.Season <= 1 ? PeopleArt.Act.Shears : PeopleArt.Act.Tend;
                            return j;
                        }
                        case BuildingType.Fishpond:
                        {
                            // at the water's edge with a rod
                            var wtile = NearestWater(t, p.Id);
                            if (wtile.x >= 0) { j.Spot = new Vector2(wtile.x * 16 + 8, wtile.y * 16 + 8); j.Dir = wtile.z; j.Act = PeopleArt.Act.Fish; return j; }
                            j.Inside = true; return j;
                        }
                        case BuildingType.Bakehouse: case BuildingType.Butcher: case BuildingType.Weaver:
                        {
                            // mornings at the trestle out front, the rest of the day inside
                            if (ph > 0.55f) { j.Inside = true; return j; }
                            j.Spot = new Vector2(t.X * 16 + 8 + t.Employees.IndexOf(p.Id) * 12, (t.Y + t.Def.H) * 16 + 4); j.Dir = PeopleArt.Down; j.Front = true;
                            j.Act = t.Type == BuildingType.Bakehouse ? PeopleArt.Act.Knead : t.Type == BuildingType.Butcher ? PeopleArt.Act.Cleaver : PeopleArt.Act.Sew;
                            return j;
                        }
                    }
                    break;
                case Chore.Travel:
                {
                    // the merchant on the road with his cart: out to the capital on Tuesday, home on Thursday
                    var capX = town.CrossOf(town.Capital); if (t == null || capX == null) break;
                    var home = new Vector2(t.X * 16 + t.Def.W * 8, (t.Y + t.Def.H) * 16 + 6);
                    var market = new Vector2(capX.X * 16 + 16 + (p.Seed - 0.5f) * 12, (capX.Y + 2) * 16 + 8);
                    float k = Mathf.Clamp01((ph - 0.25f) / 0.5f);
                    bool outward = town.WeekdayIndex == 1;
                    if (outward ? k >= 1f : k <= 0f) { j.Spot = outward ? market : home; return j; }
                    j.Spot = Vector2.Lerp(outward ? home : market, outward ? market : home, k);
                    j.Carry = PropArt.CarriedSack();
                    return j;
                }
                case Chore.Market:
                    // trading at the capital's market, by the cross
                    if (t != null) { j.Spot = new Vector2(t.X * 16 + 16 + (p.Seed - 0.5f) * 14, (t.Y + 2) * 16 + 8); j.Dir = PeopleArt.Down; j.Act = PeopleArt.Act.Point; return j; }
                    break;
                case Chore.Mass: case Chore.Pray: case Chore.Court: case Chore.Accounts:
                    if (t != null) { j.Inside = true; return j; }
                    break;
                case Chore.Collect:
                {
                    float k = Mathf.Clamp01((ph - 0.28f) / 0.38f);
                    if (p.Rank == Rank.Collector)
                    {
                        // riding round the village crosses (and, for the steward's man, the manors), then home with the chest
                        List<Building> stops;
                        var bar = town.BaronyOfBaron(p);
                        if (bar != null) stops = town.VillagesOf(bar).Select(town.CrossOf).Where(c => c != null).ToList();
                        else
                        {
                            stops = town.Villages.Where(v => v.BaronyId < 0).Select(town.CrossOf).Where(c => c != null).ToList();
                            stops.AddRange(town.Baronies.Select(x => town.GetBuilding(x.ManorId)).Where(m => m != null));
                        }
                        stops = stops.OrderBy(b => b.X).ToList();
                        if (stops.Count == 0 || k >= 1f) { if (t != null) { j.Inside = true; j.B = t; } return j; }
                        var st = stops[Mathf.Min(stops.Count - 1, Mathf.FloorToInt(k * stops.Count))];
                        j.B = st; j.Spot = HasDoor(st) ? DoorStep(st, p) + new Vector2(4, 2) : Front(st, p); j.Dir = PeopleArt.Up;
                        j.Carry = PropArt.Chest();
                        return j;
                    }
                    // the reeve: door to door round his village, then the takings into the chest at the cross
                    int vid = t != null ? t.VillageId : -1;
                    var houses = town.Buildings.Where(b => b.Built && Town.IsPeasantHome(b.Type) && b.Residents.Count > 0 && b.VillageId == vid).OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
                    j.Carry = PropArt.CarriedSack();
                    if (houses.Count == 0 || k >= 1f) { if (t != null) { j.B = t; j.Spot = Front(t, p); j.Dir = PeopleArt.Up; } return j; }
                    var h = houses[Mathf.Min(houses.Count - 1, Mathf.FloorToInt(k * houses.Count))];
                    j.B = h; j.Spot = HasDoor(h) ? DoorStep(h, p) + new Vector2(4, 2) : Front(h, p); j.Dir = PeopleArt.Up;
                    return j;
                }
                case Chore.Deliver:
                {
                    // the morning at the hall, the chest carried to the castle at midday, then back to the books
                    var seat = town.Seat;
                    if (ph < 0.42f || ph > 0.6f || seat == null) { if (t != null) { j.Inside = true; j.B = t; } return j; }
                    j.Inside = true; j.B = seat; j.Carry = PropArt.Chest();
                    return j;
                }
            }
            j.Spot = new Vector2(float.NaN, 0);
            return j;
        }

        /// <summary>A knight's beat: round the village by day.</summary>
        Vector2 PatrolSpot(Citizen p)
        {
            var homes = town.Buildings.Where(b => b.Built && Town.IsPeasantHome(b.Type)).ToList();
            if (homes.Count == 0) return new Vector2(Town.WagonX * 16, Town.TrailRow * 16 + 12);
            int k = Mathf.FloorToInt(time / 14f + p.Seed * 7) % homes.Count;
            var h = homes[k];
            return new Vector2(h.X * 16 + h.Def.W * 8 + (p.Seed - 0.5f) * 10, (h.Y + h.Def.H) * 16 + 8);
        }

        /// <summary>Children playing round their home, or near the village green.</summary>
        Vector2 PlaySpot(Citizen p, Building home)
        {
            float t = time * 0.12f + p.Id;
            var c = home != null ? new Vector2(home.X * 16 + home.Def.W * 8, (home.Y + home.Def.H) * 16 + 12) : CampCenter();
            return c + new Vector2(Mathf.Round(Mathf.Sin(t * 1.3f) * 18), Mathf.Round(Mathf.Sin(t * 0.9f + 2) * 6) + 4);
        }

        void Repath(PersonView v, Vector2 goal, Building visit)
        {
            v.Goal = goal; v.Path.Clear(); v.PathIdx = 0;
            var s = new Vector2Int(Mathf.FloorToInt(v.Pos.x / 16), Mathf.FloorToInt((v.Pos.y - 1) / 16));
            var t = new Vector2Int(Mathf.FloorToInt(goal.x / 16), Mathf.FloorToInt((goal.y - 1) / 16));
            if (s != t)
            {
                var tiles = nav.Find(s, t, visit);
                if (tiles != null)
                    for (int i = 1; i < tiles.Count - 1; i++) v.Path.Add(new Vector2(tiles[i].x * 16 + 8, tiles[i].y * 16 + 12));
            }
            v.Path.Add(goal);
        }

        void SyncPeople()
        {
            int gameSpeed = GameController.Instance != null ? Mathf.Max(1, GameController.Instance.Speed) : 1;
            float dt = Time.deltaTime, anim = dt * Mathf.Min(gameSpeed, 3);
            float baseSpeed = 34f * Mathf.Min(gameSpeed, 12) * dt;
            float ph = town.TimeOfDay;
            bool nightTime = ph < 0.22f || ph > 0.87f;
            bool workHours = ph > 0.27f && ph < 0.72f;
            bool massHours = town.MassToday && ph > 0.3f && ph < 0.46f;
            int repaths = 24;
            var alive = new HashSet<int>();
            foreach (var p in town.People)
            {
                alive.Add(p.Id);
                PersonView v;
                if (!people.TryGetValue(p.Id, out v))
                {
                    v = new PersonView { P = p };
                    v.Sr = MakeSr("Person", layerPeople, null, 0);
                    v.Held = MakeSr("Held", v.Sr.transform, null, 0); v.Held.enabled = false;
                    people[p.Id] = v;
                }
                v.P = p;
                var home = town.HomeOf(p);
                var task = town.GetBuilding(p.TaskId);

                // where should they be right now?
                Vector2 goal; Building visit = null; bool inside = false;
                Job job = default(Job); job.Act = PeopleArt.Act.None;
                if (nightTime || p.Chore == Chore.Rest && (ph < 0.3f || ph > 0.8f))
                {
                    if (home != null && home.Built) { visit = home; inside = true; goal = HasDoor(home) ? DoorStep(home, p) : Front(home, p); }
                    else { Vector2 ts; int head = town.HouseholdOf(p); goal = tentSpot.TryGetValue(head, out ts) ? ts + new Vector2((p.Seed - 0.5f) * 8, 0) : CampCenter(); }
                }
                else if (massHours && p.Chore == Chore.Mass && task != null)
                {
                    visit = task; inside = true; goal = HasDoor(task) ? DoorStep(task, p) : Front(task, p);
                }
                else if (workHours && p.Chore != Chore.None && p.Chore != Chore.Rest && p.Chore != Chore.Play && p.Chore != Chore.Mass)
                {
                    if (p.Chore == Chore.Patrol) goal = PatrolSpot(p);
                    else
                    {
                        job = JobFor(p, task, ph);
                        var target = job.B ?? task;
                        if (job.Inside && target != null) { visit = target; inside = true; goal = HasDoor(target) ? DoorStep(target, p) : Front(target, p); }
                        else if (!float.IsNaN(job.Spot.x)) goal = job.Spot;
                        else goal = home != null ? Front(home, p) : CampCenter();
                    }
                }
                else if (!nightTime && ph > 0.72f && Day0(p) && town.OfType(BuildingType.Alehouse).Any())
                {
                    // a cup at the alehouse of an evening
                    var ale = town.OfType(BuildingType.Alehouse).OrderBy(a => Mathf.Abs(a.X - (home != null ? home.X : 0))).First();
                    visit = ale; inside = true; goal = HasDoor(ale) ? DoorStep(ale, p) : Front(ale, p);
                }
                else if (p.IsChild && !nightTime) goal = PlaySpot(p, home);
                else if (home != null && home.Built && (p.Chore == Chore.Court || p.Chore == Chore.Pray) && task != null) { visit = task; inside = true; goal = HasDoor(task) ? DoorStep(task, p) : Front(task, p); }
                else if (home != null && home.Built)
                {
                    // pottering about the toft, or indoors by the fire in winter
                    if (town.Season == 3 || ph > 0.8f) { visit = home; inside = true; goal = HasDoor(home) ? DoorStep(home, p) : Front(home, p); }
                    else goal = Front(home, p);
                }
                else { Vector2 ts; int head = town.HouseholdOf(p); goal = tentSpot.TryGetValue(head, out ts) ? ts + new Vector2(6, 2) : CampCenter(); }

                if (float.IsNaN(v.Pos.x))
                {
                    v.Pos = goal; v.Goal = goal; v.Path.Clear(); v.PathIdx = 0;
                    if (inside) { v.DoorState = In; v.DoorB = visit != null ? visit.Id : -1; }
                }

                // time to come out?
                if (v.DoorState == In && !(inside && (visit != null ? visit.Id : -1) == v.DoorB))
                {
                    var db = town.GetBuilding(v.DoorB);
                    v.DoorState = Exiting; v.DoorT = 0;
                    if (HasDoor(db)) { v.Pos = DoorStep(db, p); OpenDoor(db); }
                    else if (db != null) v.Pos = Front(db, p);
                    v.Goal = new Vector2(float.NaN, 0);
                }

                v.Moving = false;
                if (v.DoorState == Out)
                {
                    if ((float.IsNaN(v.Goal.x) || (goal - v.Goal).sqrMagnitude > 9f) && repaths > 0) { Repath(v, goal, visit); repaths--; }
                    if (v.PathIdx < v.Path.Count)
                    {
                        int tx = Mathf.FloorToInt(v.Pos.x / 16), ty = Mathf.FloorToInt((v.Pos.y - 1) / 16);
                        float speed = baseSpeed * (nav.IsRoad(tx, ty) ? 1.5f : 1f) * (p.IsChild ? 1.1f : 1f) * (p.Age > 60 ? 0.8f : 1f);
                        var target = v.Path[v.PathIdx];
                        var delta = target - v.Pos;
                        float dist = delta.magnitude;
                        if (dist <= speed) { v.Pos = target; v.PathIdx++; }
                        else
                        {
                            v.Pos += delta / dist * speed;
                            v.Moving = true; v.Step += speed;
                            v.Dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x < 0 ? PeopleArt.Left : PeopleArt.Right) : (delta.y < 0 ? PeopleArt.Up : PeopleArt.Down);
                        }
                    }
                    bool arrived = v.PathIdx >= v.Path.Count && !v.Moving;
                    if (arrived && inside && visit != null)
                    {
                        v.DoorState = Entering; v.DoorT = 0; v.DoorB = visit.Id;
                        if (HasDoor(visit)) OpenDoor(visit);
                    }
                    else if (arrived) v.Dir = job.Hammer && workHours ? PeopleArt.Up : job.Act != PeopleArt.Act.None && workHours ? job.Dir : PeopleArt.Down;
                }
                else if (v.DoorState == Entering || v.DoorState == Exiting)
                {
                    v.DoorT += anim;
                    var db = town.GetBuilding(v.DoorB);
                    if (HasDoor(db)) OpenDoor(db);
                    if (v.DoorT >= DoorTime) { v.DoorState = v.DoorState == Entering ? In : Out; v.DoorT = 0; }
                }

                v.Inside = v.DoorState == In;
                v.Sr.enabled = !v.Inside;
                v.Held.enabled = false;
                if (v.Inside) continue;

                float x = Mathf.Round(v.Pos.x), y = Mathf.Round(v.Pos.y);
                int frame = v.Moving ? ((Mathf.FloorToInt(v.Step / 5) % 4) == 1 ? 1 : (Mathf.FloorToInt(v.Step / 5) % 4) == 3 ? 2 : 0) : 0;
                int order = SortOrder(y, 2);
                var act = PeopleArt.Act.None; int phase = 0;
                var tint = Color.white;
                if (v.DoorState == Entering || v.DoorState == Exiting)
                {
                    var db = town.GetBuilding(v.DoorB);
                    float t = Mathf.Clamp01(v.DoorT / DoorTime);
                    float k = v.DoorState == Entering ? Mathf.Clamp01((t - 0.3f) / 0.7f) : 1 - Mathf.Clamp01(t / 0.6f);
                    v.Dir = v.DoorState == Entering ? PeopleArt.Up : PeopleArt.Down;
                    if (HasDoor(db))
                    {
                        float step = DoorStep(db, p).y;
                        y = Mathf.Round(Mathf.Lerp(step, DoorInsideY(db), k));
                        order = SortOrder((db.Y + db.Def.H) * 16, 2);
                        if (k > 0 && k < 1) frame = (Mathf.FloorToInt(k * 6) % 2) + 1;
                    }
                    float l = 1 - 0.75f * k;
                    tint = new Color(l, l * 0.92f, l * 0.85f, 1 - k * k);
                }
                else if (workHours && v.PathIdx >= v.Path.Count && !v.Moving)
                {
                    if (job.Hammer)
                    {
                        int hf = Mathf.FloorToInt(time * 3.2f * Mathf.Min(gameSpeed, 2) + p.Seed * 5) % 2;
                        v.Held.enabled = true; v.Held.sprite = PropArt.Hammer(hf);
                        v.Held.transform.localPosition = Pixels.ToWorld(1, hf == 0 ? -18 : -14);
                        if (hf == 1 && v.LastHammer == 0 && Random.value < 0.5f) SpawnFx(x + 3 + Random.value * 4, y - 12 - Random.value * 4, FxDust, (Random.value - 0.5f) * 12, 0.5f);
                        v.LastHammer = hf;
                        var site = task;
                        if (site != null && !site.Built) order = Mathf.Max(order, SortOrder((site.Y + site.Def.H) * 16, 3));
                    }
                    else if (job.Act != PeopleArt.Act.None && (v.Pos - job.Spot).sqrMagnitude < 36f)
                    {
                        act = job.Act; v.Dir = job.Dir;
                        bool landing;
                        phase = PeopleArt.PhaseAt(act, time * Mathf.Min(gameSpeed, 2) + p.Seed * 5, out landing);
                        if (landing && phase != v.LastPhase && particles.Count < 520) WorkFx(act, v.Dir, new Vector2(x, y));
                        v.LastPhase = phase;
                        if (job.Front && job.B != null) order = Mathf.Max(order, SortOrder((job.B.Y + job.B.Def.H) * 16, 3));
                    }
                }
                if (job.Carry != null && v.DoorState == Out && !v.Held.enabled)
                {
                    v.Held.enabled = true; v.Held.sprite = job.Carry;
                    v.Held.transform.localPosition = Pixels.ToWorld(-4, -13);
                }
                v.Sr.sprite = act != PeopleArt.Act.None ? PeopleArt.Working(p, v.Dir, act, phase) : PeopleArt.Get(p, v.Dir, frame);
                v.Sr.color = tint; v.Held.color = tint;
                v.Sr.transform.position = Pixels.ToWorld(x, y);
                v.Sr.sortingOrder = order; v.Held.sortingOrder = order + 1;
            }
            if (alive.Count != people.Count)
            {
                var gone = new List<int>();
                foreach (var kv in people) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { Destroy(people[id].Sr.gameObject); people.Remove(id); }
            }
        }

        /// <summary>Whether someone had a cup at the alehouse today (so they're there of an evening).</summary>
        bool Day0(Citizen p) { return p.AleDay == town.Day - 1 || p.AleDay == town.Day; }

        void Bits(float x, float y, int kind, int n, float vx0, float spread, float up)
        {
            for (int i = 0; i < n; i++)
                SpawnFx(x + (Random.value - 0.5f) * 3, y - Random.value * 2, kind, vx0 + (Random.value - 0.5f) * spread, 0.35f + Random.value * 0.35f, -up * (0.5f + Random.value * 0.7f));
        }

        /// <summary>Chips, grit, soil, hay and seed as the tool lands.</summary>
        void WorkFx(PeopleArt.Act a, int dir, Vector2 at)
        {
            float away = dir == PeopleArt.Right ? 1 : dir == PeopleArt.Left ? -1 : 0;
            float x = at.x + away * 9, y = at.y - 2;
            switch (a)
            {
                case PeopleArt.Act.Chop: case PeopleArt.Act.Maul: Bits(x, y - 4, FxChip, 4, away * 16, 30, 26); break;
                case PeopleArt.Act.Pick: Bits(at.x, at.y - 10, FxGrit, 5, 0, 34, 26); break;
                case PeopleArt.Act.Hoe: Bits(x, y, FxSoil, 4, away * 12, 20, 18); break;
                case PeopleArt.Act.Scythe: Bits(x, y, FxHay, 5, away * 14, 24, 14); break;
                case PeopleArt.Act.Sow: Bits(at.x + away * 6, at.y - 9, FxSeed, 6, away * 26, 18, 10); break;
                case PeopleArt.Act.Fork: Bits(x, at.y - 19, FxHay, 5, away * 10, 24, 16); break;
                case PeopleArt.Act.Pluck: if (town.Season != 3 && Random.value < 0.6f) SpawnFx(at.x, at.y - 14, FxLeaf, (Random.value - 0.5f) * 8, 1.4f, 5); break;
            }
        }

        public bool TryGetPersonPos(int id, out Vector2 pos)
        {
            PersonView v;
            if (people.TryGetValue(id, out v) && !float.IsNaN(v.Pos.x)) { pos = v.Pos; return true; }
            pos = Vector2.zero; return false;
        }
        public bool IsInside(int id) { PersonView v; return people.TryGetValue(id, out v) && v.Inside; }
        public int InsideOf(int id) { PersonView v; return people.TryGetValue(id, out v) && v.Inside ? v.DoorB : -1; }

        // ================================================================== effects
        Rect VisiblePx()
        {
            var o = rig.ViewOrigin; var s = rig.ViewSize;
            return new Rect(o.x - 48, o.y - 48, s.x + 96, s.y + 112);
        }

        float NightAlpha()
        {
            int sp = GameController.Instance != null ? GameController.Instance.Speed : 1;
            float k = sp >= 30 ? 0.3f : sp >= 10 ? 0.6f : 1f;
            return town.Nightness * k;
        }

        void UpdateEffects(int si)
        {
            float dt = Time.deltaTime, nightness = town.Nightness;
            var vr = VisiblePx();
            foreach (var kv in buildings)
            {
                var b = kv.Value.B; var d = b.Def;
                if (!b.Built) continue;
                float X = b.X * 16, Y = b.Y * 16;
                if (X > vr.xMax || X + d.W * 16 < vr.xMin || Y > vr.yMax || Y + d.H * 16 < vr.yMin) continue;
                float rate = 0;
                if (Town.IsHome(b.Type) && b.Residents.Count > 0) rate = (nightness > 0 || town.TimeOfDay < 0.3f || si >= 2) ? 0.9f : 0.3f;
                else if (b.Type == BuildingType.Alehouse && b.Employees.Count > 0) rate = 0.8f;
                else if (b.Type == BuildingType.Church && town.Priest != null && si >= 2) rate = 0.3f;
                if (rate <= 0) continue;
                float tmr; if (!smokeTimers.TryGetValue(b.Id, out tmr)) tmr = Random.value;
                tmr -= dt * rate;
                if (tmr <= 0)
                {
                    tmr = 1 + Random.value * 0.4f;
                    var art = kv.Value.Art;
                    foreach (var s in art.Smoke) if (particles.Count < 500) SpawnFx(X + s.x, Y - art.Lift + s.y, FxSmoke, (Random.value - 0.3f) * 1.5f, 3.2f);
                }
                smokeTimers[b.Id] = tmr;
            }
            if (campfire.enabled && Random.value < dt * 1.5f) SpawnFx(Pixels.ToPixel(campfire.transform.position).x, Pixels.ToPixel(campfire.transform.position).y - 6, FxSmoke, (Random.value - 0.3f) * 1.5f, 3.2f);
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Life -= dt;
                if (p.Life <= 0) { p.Sr.enabled = false; particlePool.Push(p.Sr); particles.RemoveAt(i); continue; }
                float k = 1 - p.Life / p.Max; int sz;
                switch (p.Kind)
                {
                    case FxDust:
                        p.X += p.Vx * dt; p.Vx *= Mathf.Max(0, 1 - 3f * dt); p.Y -= 5 * dt;
                        sz = 2 + Mathf.FloorToInt(k * 2);
                        p.Sr.color = new Color(216 / 255f, 196 / 255f, 154 / 255f, 0.8f * (1 - k));
                        break;
                    case FxSpark:
                        p.X += p.Vx * dt; p.Y -= 14 * dt; sz = 1;
                        p.Sr.color = new Color(1f, 242 / 255f, 160 / 255f, (Mathf.FloorToInt(time * 12 + i) % 3 == 0 ? 0.4f : 1f) * (1 - k));
                        break;
                    case FxLeaf:
                        p.X += (p.Vx + Mathf.Sin(time * 5 + i) * 10) * dt; p.Y += p.Vy * dt; sz = 1;
                        { var c = BitColors[5]; p.Sr.color = new Color(c.r / 255f, c.g / 255f, c.b / 255f, Mathf.Min(1, 3 * (1 - k))); }
                        break;
                    case FxChip: case FxGrit: case FxSoil: case FxHay: case FxSeed:
                    {
                        p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 110 * dt; sz = 1;
                        var c = BitColors[p.Kind - FxChip];
                        p.Sr.color = new Color(c.r / 255f, c.g / 255f, c.b / 255f, Mathf.Min(1, 4 * (1 - k)));
                        break;
                    }
                    default:
                        p.X += (3 + p.Vx) * dt; p.Y -= 7 * dt;
                        sz = 1 + Mathf.FloorToInt(k * 3);
                        p.Sr.color = p.Kind == FxSmokeDark ? new Color(110 / 255f, 104 / 255f, 98 / 255f, 0.6f * (1 - k)) : new Color(236 / 255f, 232 / 255f, 224 / 255f, 0.6f * (1 - k));
                        break;
                }
                Place(p.Sr, Mathf.Round(p.X - sz / 2f), Mathf.Round(p.Y - sz / 2f), sz, sz);
            }

            int used = 0;
            if (si != 3)
            {
                int tick = Mathf.FloorToInt(time * 1.6f);
                int tx0 = Mathf.Max(0, Mathf.FloorToInt(vr.xMin / 16)), tx1 = Mathf.Min(Town.Cols - 1, Mathf.CeilToInt(vr.xMax / 16));
                int ty0 = Mathf.Max(0, Mathf.FloorToInt(vr.yMin / 16)), ty1 = Mathf.Min(Town.Rows - 1, Mathf.CeilToInt(vr.yMax / 16));
                for (int ty = ty0; ty <= ty1 && used < sparkles.Count; ty++)
                    for (int tx = tx0; tx <= tx1 && used < sparkles.Count; tx++)
                    {
                        if (town.TerrainAt(tx, ty) != Tile.Water || town.IsRoad(tx, ty)) continue;
                        for (int k = 0; k < 2 && used < sparkles.Count; k++)
                        {
                            int q = tick + ((tx * 3 + ty * 5 + k) % 7);
                            if (Noise.Hash(tx * 7 + k, ty * 13, q) >= 0.16f) continue;
                            var s = sparkles[used++]; s.enabled = true;
                            Place(s, tx * 16 + 2 + (int)(Noise.Hash(tx, ty, k + q) * 11), ty * 16 + 3 + (int)(Noise.Hash(ty, tx, k + q) * 10), 2, 1);
                        }
                    }
            }
            for (int i = used; i < sparkles.Count; i++) sparkles[i].enabled = false;

            foreach (var c in clouds)
            {
                c.X += 4 * dt;
                if (c.X - c.Rx > Town.Cols * 16) { c.X = -c.Rx; c.Y = Random.Range(0f, Town.Rows * 16f); }
                c.Sr.transform.position = Pixels.ToWorld(Mathf.Round(c.X - c.Rx), Mathf.Round(c.Y - c.Rx / 2f));
            }

            var view = rig.ViewSize; var org = rig.ViewOrigin;
            if (birdsActive)
            {
                birdX += birdVx * dt;
                if (birdX > org.x + view.x + 60) birdsActive = false;
            }
            else if (nightness < 0.2f && si != 3 && Random.value < dt / 18f)
            {
                birdsActive = true; birdX = org.x - 30; birdY = org.y + Random.value * view.y * 0.6f + 10; birdVx = 26 + Random.value * 14;
            }
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i];
                b.enabled = birdsActive && i < 3 + (int)(birdVx % 3);
                if (!b.enabled) continue;
                float bx = Mathf.Round(birdX - i * 7), by = Mathf.Round(birdY + (i % 2 == 1 ? 1 : -1) * Mathf.Ceil(i / 2f) * 4);
                bool flap = Mathf.FloorToInt(time * 6 + i) % 2 == 1;
                b.color = new Color32(42, 36, 32, 255);
                if (flap) Place(b, bx, by, 3, 1); else Place(b, bx, by + 1, 3, 1);
            }
        }

        void SpawnFx(float x, float y, int kind, float vx, float life, float vy = 0)
        {
            if (particles.Count >= 600) return;
            var sr = particlePool.Count > 0 ? particlePool.Pop() : MakeSr("Fx", layerFx, NatureArt.White, OrderSmoke);
            sr.enabled = true;
            particles.Add(new Particle { Sr = sr, X = x, Y = y, Life = life, Max = life, Vx = vx, Vy = vy, Kind = kind });
        }

        void UpdateNight()
        {
            var o = rig.ViewOrigin; var s = rig.ViewSize;
            float n = NightAlpha();
            night.enabled = n > 0;
            if (n > 0) { night.color = new Color(16 / 255f, 20 / 255f, 56 / 255f, 0.5f * n); Place(night, o.x - 4, o.y - 4, s.x + 8, s.y + 8); }
            float t = town.TimeOfDay;
            bool dusk = t > 0.72f && t < 0.88f;
            sunset.enabled = dusk;
            if (dusk)
            {
                float k = 1 - Mathf.Abs(t - 0.8f) / 0.08f;
                sunset.color = new Color(1f, 140 / 255f, 60 / 255f, 0.09f * k);
                Place(sunset, o.x - 4, o.y - 4, s.x + 8, s.y + 8);
            }
        }

        // ================================================================== selection, ghost & picking
        void UpdateSelection()
        {
            foreach (var e in selEdges) e.enabled = false;
            selMarker.enabled = false;
            if (GameController.Instance == null) return;
            var sel = GameController.Instance.Selected;
            if (sel.Kind == SelectionKind.Building)
            {
                var b = town.GetBuilding(sel.Id); if (b == null) return;
                var d = b.Def; int lift = BuildingArt.LiftOf(b.Type);
                float x = b.X * 16, y = b.Y * 16 - lift, w = d.W * 16, h = d.H * 16 + lift;
                var gold = new Color32(255, 215, 122, 255);
                Place(selEdges[0], x, y, w, 1); Place(selEdges[1], x, y + h - 1, w, 1);
                Place(selEdges[2], x, y, 1, h); Place(selEdges[3], x + w - 1, y, 1, h);
                foreach (var e in selEdges) { e.enabled = true; e.color = gold; }
            }
            else if (sel.Kind == SelectionKind.Person)
            {
                Vector2 pos;
                if (!TryGetPersonPos(sel.Id, out pos) || IsInside(sel.Id)) return;
                float bob = Mathf.FloorToInt(time * 3) % 2;
                selMarker.enabled = true;
                selMarker.color = new Color32(255, 215, 122, 255);
                Place(selMarker, Mathf.Round(pos.x) - 1, Mathf.Round(pos.y) - 22 - bob, 3, 2);
            }
        }

        // ================================================================== zones of control
        // Each tile belongs to the village whose cross is nearest, within reach. The overlay tints those tiles in the colour of the
        // village's lord (the crown, or a baron) and rules a line where one village's land meets another's or the wild.
        SpriteRenderer zoneFill, zoneEdge;
        Texture2D zoneFillTex, zoneEdgeTex;
        string zoneKey;
        /// <summary>The village each tile belongs to (-1: none), recomputed when the realm changes.</summary>
        public int[] ZoneOwner { get; private set; }
        string ownerKey;
        /// <summary>The villages whose zones are on show right now, or empty.</summary>
        public readonly List<int> ZoneShown = new List<int>();
        const int ZoneScale = 4;   // overlay texels per tile

        static readonly string[] BaronyColours = { "#b5432f", "#3e6a9a", "#5a9a4a", "#8a4a9a", "#c07a2a", "#2a8a8a", "#9a3a6a", "#6a7a2a" };
        public static Color32 LiegeColour(Town t, Village v)
        {
            if (v == null || v.BaronyId < 0) return PixelCanvas.C("#e0b04a");
            int i = t.Baronies.FindIndex(b => b.Id == v.BaronyId);
            return PixelCanvas.C(BaronyColours[Mathf.Max(0, i) % BaronyColours.Length]);
        }

        void EnsureOwners()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var v in town.Villages) { var c = town.CrossOf(v); sb.Append(v.Id).Append(':').Append(c != null ? c.X * 1000 + c.Y : -1).Append(v.Capital ? 'c' : '.').Append(';'); }
            foreach (var b in town.Buildings) if (b.Type == BuildingType.BoundaryStone && b.Built) sb.Append('s').Append(b.Id).Append(':').Append(b.VillageId).Append(';');
            string k = sb.ToString();
            if (ZoneOwner != null && k == ownerKey) return;
            ownerKey = k;
            ZoneOwner = town.LandMap();
        }

        void UpdateZones()
        {
            ZoneShown.Clear();
            var gc = GameController.Instance;
            if (gc == null) return;
            var sel = gc.Selected;
            var sb = sel.Kind == SelectionKind.Building ? town.GetBuilding(sel.Id) : null;
            bool building = gc.Tool == ToolKind.Build && gc.BuildType != BuildingType.Road && !Defs.Get(gc.BuildType).IsWall;
            if (gc.ZoneView || building) ZoneShown.AddRange(town.Villages.Select(v => v.Id));
            else if (sb != null && sb.Type == BuildingType.VillageCross && sb.VillageId >= 0) ZoneShown.Add(sb.VillageId);
            else if (sb != null && sb.Type == BuildingType.Manor)
            {
                var bar = town.Baronies.FirstOrDefault(x => x.ManorId == sb.Id);
                if (bar != null) ZoneShown.AddRange(town.VillagesOf(bar).Select(v => v.Id));
            }
            if (zoneFill == null)
            {
                zoneFillTex = new Texture2D(Town.Cols * ZoneScale, Town.Rows * ZoneScale, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ZoneFill" };
                zoneEdgeTex = new Texture2D(Town.Cols * ZoneScale, Town.Rows * ZoneScale, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ZoneEdge" };
                var pivot = new Vector2(0, 1); float ppu = Pixels.PerUnit / (16f / ZoneScale);
                zoneFill = MakeSr("ZoneFill", layerFx, Sprite.Create(zoneFillTex, new Rect(0, 0, zoneFillTex.width, zoneFillTex.height), pivot, ppu, 0, SpriteMeshType.FullRect), OrderBase - 1);
                zoneEdge = MakeSr("ZoneEdge", layerFx, Sprite.Create(zoneEdgeTex, new Rect(0, 0, zoneEdgeTex.width, zoneEdgeTex.height), pivot, ppu, 0, SpriteMeshType.FullRect), OrderSmoke - 1);
                zoneFill.transform.position = zoneEdge.transform.position = Pixels.ToWorld(0, 0);
                zoneKey = null; ownerKey = null;
            }
            bool on = ZoneShown.Count > 0;
            zoneFill.enabled = zoneEdge.enabled = on;
            if (!on) return;
            EnsureOwners();
            var kb = new System.Text.StringBuilder(ownerKey);
            foreach (var id in ZoneShown) { var v = town.GetVillage(id); kb.Append('|').Append(id).Append(',').Append(v != null ? v.BaronyId : -9); }
            foreach (var b in town.Baronies) kb.Append('#').Append(b.Id);
            string key = kb.ToString();
            if (key == zoneKey) return;
            zoneKey = key;
            DrawZones();
        }

        void DrawZones()
        {
            int W = Town.Cols * ZoneScale, H = Town.Rows * ZoneScale;
            var fill = new Color32[W * H]; var edge = new Color32[W * H];
            var shown = new HashSet<int>(ZoneShown);
            var col = new Dictionary<int, Color32>();
            foreach (var v in town.Villages) col[v.Id] = LiegeColour(town, v);
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    int o = ZoneOwner[ty * Town.Cols + tx];
                    if (o < 0 || !shown.Contains(o)) continue;
                    var c = col[o];
                    var f = new Color32(c.r, c.g, c.b, 46);
                    var e = new Color32((byte)(c.r * 0.75f), (byte)(c.g * 0.75f), (byte)(c.b * 0.75f), 230);
                    bool n = ty == 0 || ZoneOwner[(ty - 1) * Town.Cols + tx] != o, s2 = ty == Town.Rows - 1 || ZoneOwner[(ty + 1) * Town.Cols + tx] != o;
                    bool w = tx == 0 || ZoneOwner[ty * Town.Cols + tx - 1] != o, ea = tx == Town.Cols - 1 || ZoneOwner[ty * Town.Cols + tx + 1] != o;
                    for (int sy = 0; sy < ZoneScale; sy++)
                        for (int sx = 0; sx < ZoneScale; sx++)
                        {
                            int px = tx * ZoneScale + sx, py = ty * ZoneScale + sy;
                            int i = (H - 1 - py) * W + px;   // textures are bottom-up
                            fill[i] = f;
                            bool line = (n && sy == 0) || (s2 && sy == ZoneScale - 1) || (w && sx == 0) || (ea && sx == ZoneScale - 1);
                            // a dashed line, like a map's boundary
                            if (line && ((px + py) / 2) % 3 != 2) edge[i] = e;
                        }
                }
            zoneFillTex.SetPixels32(fill); zoneFillTex.Apply(false);
            zoneEdgeTex.SetPixels32(edge); zoneEdgeTex.Apply(false);
        }

        public void ShowGhost(BuildingType type, int tx, int ty, bool ok)
        {
            var d = Defs.Get(type);
            ghostFoot.enabled = true;
            ghostFoot.color = ok ? new Color(156 / 255f, 214 / 255f, 110 / 255f, 0.38f) : new Color(232 / 255f, 96 / 255f, 70 / 255f, 0.45f);
            Place(ghostFoot, tx * 16, ty * 16, d.W * 16, d.H * 16);
            if (type == BuildingType.Road) { ghostBase.enabled = ghostTop.enabled = false; return; }
            var art = BuildingArt.Get(type, 0, town.Season);
            ghostBase.enabled = ghostTop.enabled = true;
            ghostBase.sprite = art.Base[0]; ghostTop.sprite = art.Top;
            ghostBase.color = ghostTop.color = new Color(1, 1, 1, 0.7f);
            Place(ghostBase, tx * 16, ty * 16 - art.Lift); Place(ghostTop, tx * 16, ty * 16 - art.Lift);
        }

        public void ShowClearMarker(int tx, int ty, int w, int h)
        {
            ghostBase.enabled = ghostTop.enabled = false;
            ghostFoot.enabled = true;
            ghostFoot.color = new Color(232 / 255f, 96 / 255f, 70 / 255f, 0.45f);
            Place(ghostFoot, tx * 16, ty * 16, w * 16, h * 16);
        }

        public void HideGhost() { if (ghostBase == null) return; ghostBase.enabled = ghostTop.enabled = ghostFoot.enabled = false; }

        /// <summary>Finds the person or building under a point (map pixels).</summary>
        public Selection PickAt(Vector2 w)
        {
            int best = -1; float bd = 7;
            foreach (var kv in people)
            {
                var v = kv.Value; if (float.IsNaN(v.Pos.x) || v.Inside) continue;
                float dd = Vector2.Distance(new Vector2(v.Pos.x, v.Pos.y - 8), w);
                if (dd < bd) { bd = dd; best = kv.Key; }
            }
            if (best >= 0) return Selection.Person(best);
            Building hit = null; float hitFoot = -1;
            foreach (var b in town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                var d = b.Def; int lift = BuildingArt.LiftOf(b.Type);
                float x0 = b.X * 16, y0 = b.Y * 16 - lift, x1 = x0 + d.W * 16, y1 = (b.Y + d.H) * 16;
                if (w.x < x0 || w.x >= x1 || w.y < y0 || w.y >= y1) continue;
                if (w.y < b.Y * 16 && b.Built && !ArtFor(b).TopCanvas.IsOpaque((int)(w.x - x0), (int)(w.y - y0))) continue;
                if (y1 > hitFoot) { hitFoot = y1; hit = b; }
            }
            return hit != null ? Selection.Building(hit.Id) : Selection.None;
        }
    }
}
