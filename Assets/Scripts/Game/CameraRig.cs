using MedievalTown.Game.Art;
using MedievalTown.Simulation;
using UnityEngine;

namespace MedievalTown.Game
{
    /// <summary>
    /// Pixel-perfect orthographic camera. Zoom is a whole number of screen pixels per art pixel,
    /// so the pixel art never blurs or shimmers.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public int Zoom { get; private set; }
        public int MinZoom = 1, MaxZoom = 6;

        // Camera centre in prototype pixel space (x right, y down)
        Vector2 center;

        static float WorldW { get { return Town.Cols * 16f; } }
        static float WorldH { get { return Town.Rows * 16f; } }

        void Awake()
        {
            Cam = GetComponent<Camera>();
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color32(28, 20, 14, 255);
            Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 100f;
            Zoom = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 360f), 2, MaxZoom);
            center = new Vector2(WorldW / 2, WorldH / 2);
        }

        /// <summary>Visible size in art pixels.</summary>
        public Vector2 ViewSize { get { return new Vector2(Screen.width / (float)Zoom, Screen.height / (float)Zoom); } }
        /// <summary>Top-left of the view in art pixels.</summary>
        public Vector2 ViewOrigin { get { var v = ViewSize; return new Vector2(center.x - v.x / 2, center.y - v.y / 2); } }

        public void CenterOn(Vector2 px) { center = px; Clamp(); }
        public void PanPixels(Vector2 deltaArtPixels) { center += deltaArtPixels; Clamp(); }
        /// <summary>Pan by a screen-space mouse delta (Unity screen coordinates, y up).</summary>
        public void PanScreen(Vector2 screenDelta) { PanPixels(new Vector2(-screenDelta.x / Zoom, screenDelta.y / Zoom)); }

        public void ZoomAt(int dir, Vector2 screenPos)
        {
            int nz = Mathf.Clamp(Zoom + dir, MinZoom, MaxZoom);
            if (nz == Zoom) return;
            var before = ScreenToPixel(screenPos);
            Zoom = nz;
            var after = ScreenToPixel(screenPos);
            center += before - after;
            Clamp();
        }

        void Clamp()
        {
            var v = ViewSize; const float m = 24f;
            center.x = v.x >= WorldW + 2 * m ? WorldW / 2 : Mathf.Clamp(center.x, v.x / 2 - m, WorldW - v.x / 2 + m);
            center.y = v.y >= WorldH + 2 * m ? WorldH / 2 : Mathf.Clamp(center.y, v.y / 2 - m, WorldH - v.y / 2 + m);
        }

        /// <summary>Screen position (pixels, y up) to prototype pixel space (y down).</summary>
        public Vector2 ScreenToPixel(Vector2 screen)
        {
            var o = ViewOrigin;
            return new Vector2(o.x + screen.x / Zoom, o.y + (Screen.height - screen.y) / Zoom);
        }

        void LateUpdate()
        {
            Clamp();
            Cam.orthographicSize = Screen.height / (2f * Pixels.PerUnit * Zoom);
            // Snap the view's top-left to whole screen pixels so art pixels land on the pixel grid
            float step = 1f / Zoom;
            var o = ViewOrigin;
            float ox = Mathf.Round(o.x / step) * step, oy = Mathf.Round(o.y / step) * step;
            var v = ViewSize;
            transform.position = new Vector3((ox + v.x / 2) / Pixels.PerUnit, -(oy + v.y / 2) / Pixels.PerUnit, -10f);
        }
    }
}
