using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>One straight dash of a marching-ants outline, in the element's local pixels.</summary>
    public readonly struct OdyDash
    {
        public OdyDash(float x1, float y1, float x2, float y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }
        public float Length => (float)Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    /// <summary>
    /// ODY-S11-216 (polish P1 item 6): a "marching ants" selection outline -- a dashed rectangle whose dash offset keeps
    /// moving. Technique from Owlbear Rodeo 1.0 (a dash offset advanced every frame); drawn anew here with UI Toolkit's
    /// <see cref="Painter2D"/> (USS has no dashed borders). Purely visual: an overlay that ignores picking, added to an
    /// element whose rectangle it outlines. Colors come from the stylesheet (<c>.ody-marching-ants</c>: <c>color</c> =
    /// dashes, <c>border-color</c> = the light line under them). The offset is stepped with the UI Toolkit scheduler
    /// delta; under <see cref="OdyClasses.ReducedMotion"/> the dashes stay still.
    /// </summary>
    public sealed class OdyMarchingAnts
    {
        /// <summary>Dash and gap lengths and the speed are the executor's choice, not values published by Owlbear.</summary>
        public const float DashLength = 6f;
        public const float GapLength = 4f;
        public const float LineWidth = 1.5f;
        public const double SpeedPixelsPerSecond = 24.0;

        private IVisualElementScheduledItem? _ticker;

        /// <param name="outset">How far outside the host's rectangle the outline sits (e.g. outside a token's border).</param>
        public OdyMarchingAnts(string name, float outset = 0f)
        {
            Element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Element.AddToClassList(OdyClasses.MarchingAnts);
            Element.style.position = Position.Absolute;
            Element.style.left = -outset;
            Element.style.top = -outset;
            Element.style.right = -outset;
            Element.style.bottom = -outset;
            Element.generateVisualContent += OnGenerateVisualContent;
        }

        public VisualElement Element { get; }

        /// <summary>Current dash offset along the perimeter, in [0, dash + gap).</summary>
        public float Phase { get; private set; }

        /// <summary>Adds the outline to <paramref name="host"/> and starts the movement (runs only while on a panel).</summary>
        public void AttachTo(VisualElement host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (Element.parent != host) host.Add(Element);
            if (_ticker == null) _ticker = Element.schedule.Execute(timer => Advance(timer.deltaTime)).Every(OdyMotion.FrameIntervalMs);
        }

        public void Detach()
        {
            _ticker?.Pause();
            _ticker = null;
            Element.RemoveFromHierarchy();
        }

        /// <summary>Moves the dashes by <paramref name="elapsedMs"/> worth of travel; public so tests step it. No-op under reduced motion.</summary>
        public void Advance(double elapsedMs)
        {
            if (elapsedMs <= 0.0 || double.IsNaN(elapsedMs) || OdyMotion.IsReducedMotion(Element)) return;
            float period = DashLength + GapLength;
            Phase = (float)((Phase + SpeedPixelsPerSecond * elapsedMs / 1000.0) % period);
            Element.MarkDirtyRepaint();
        }

        /// <summary>
        /// The dashes along the perimeter of a <paramref name="width"/> x <paramref name="height"/> rectangle, walking
        /// clockwise from the top-left corner, shifted by <paramref name="phase"/>. Dashes crossing a corner are split
        /// at the corner. Pure, so the geometry is testable without a renderer.
        /// </summary>
        public static IReadOnlyList<OdyDash> Dashes(float width, float height, float dash, float gap, float phase)
        {
            var result = new List<OdyDash>();
            if (width <= 0f || height <= 0f || dash <= 0f || gap < 0f) return result;
            float period = dash + gap;
            float perimeter = 2f * (width + height);
            float shift = phase % period;
            if (shift < 0f) shift += period;
            for (float start = shift - period; start < perimeter; start += period)
            {
                float from = Math.Max(0f, start);
                float to = Math.Min(perimeter, start + dash);
                if (to > from) AddAlongPerimeter(result, width, height, from, to);
            }

            return result;
        }

        private static void AddAlongPerimeter(List<OdyDash> into, float width, float height, float from, float to)
        {
            // Corner distances along the perimeter: top edge, right edge, bottom edge, left edge.
            float[] corners = { 0f, width, width + height, 2f * width + height, 2f * (width + height) };
            for (int edge = 0; edge < 4; edge++)
            {
                float a = Math.Max(from, corners[edge]);
                float b = Math.Min(to, corners[edge + 1]);
                if (b <= a) continue;
                Vector2 p = PointAt(width, height, a);
                Vector2 q = PointAt(width, height, b);
                into.Add(new OdyDash(p.x, p.y, q.x, q.y));
            }
        }

        private static Vector2 PointAt(float width, float height, float distance)
        {
            if (distance <= width) return new Vector2(distance, 0f);
            distance -= width;
            if (distance <= height) return new Vector2(width, distance);
            distance -= height;
            if (distance <= width) return new Vector2(width - distance, height);
            distance -= width;
            return new Vector2(0f, Math.Max(0f, height - distance));
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = Element.contentRect;
            float inset = LineWidth / 2f;
            float width = rect.width - LineWidth;
            float height = rect.height - LineWidth;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f) return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = LineWidth;

            painter.strokeColor = Element.resolvedStyle.borderTopColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(inset, inset));
            painter.LineTo(new Vector2(inset + width, inset));
            painter.LineTo(new Vector2(inset + width, inset + height));
            painter.LineTo(new Vector2(inset, inset + height));
            painter.ClosePath();
            painter.Stroke();

            painter.strokeColor = Element.resolvedStyle.color;
            painter.BeginPath();
            foreach (OdyDash dash in Dashes(width, height, DashLength, GapLength, Phase))
            {
                painter.MoveTo(new Vector2(inset + dash.X1, inset + dash.Y1));
                painter.LineTo(new Vector2(inset + dash.X2, inset + dash.Y2));
            }

            painter.Stroke();
        }
    }
}
