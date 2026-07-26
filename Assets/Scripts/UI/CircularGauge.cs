using UnityEngine;
using UnityEngine.UIElements;

namespace PostWarUI
{
    [UxmlElement]
    public partial class CircularGauge : VisualElement
    {
        private float _progress = 1.0f;
        private Color _trackColor = new Color(0.1f, 0.12f, 0.11f, 0.8f);
        private Color _fillColor = new Color(0.29f, 0.42f, 0.31f, 1f);
        private float _lineThickness = 6f;

        [UxmlAttribute]
        public float progress
        {
            get => _progress;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (!Mathf.Approximately(_progress, clamped))
                {
                    _progress = clamped;
                    MarkDirtyRepaint();
                }
            }
        }

        [UxmlAttribute]
        public Color trackColor
        {
            get => _trackColor;
            set { _trackColor = value; MarkDirtyRepaint(); }
        }

        [UxmlAttribute]
        public Color fillColor
        {
            get => _fillColor;
            set { _fillColor = value; MarkDirtyRepaint(); }
        }

        [UxmlAttribute]
        public float lineThickness
        {
            get => _lineThickness;
            set { _lineThickness = value; MarkDirtyRepaint(); }
        }

        public CircularGauge()
        {
            // Ensure element takes space and centers content
            style.flexGrow = 0;
            style.flexShrink = 0;

            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;

            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0)
                return;

            var painter = mgc.painter2D;

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float radius = (Mathf.Min(width, height) * 0.5f) - (_lineThickness * 0.5f) - 1.5f; // Extra margin for shadow offset
            if (radius <= 0) return;

            painter.lineCap = LineCap.Round;

            // 0. Sleek Directional Drop Shadow
            // Match line thickness EXACTLY, but offset slightly down and right (+1.5px, +2.0px)
            Vector2 shadowCenter = center + new Vector2(1.5f, 2.0f);
            painter.lineWidth = _lineThickness;
            painter.strokeColor = new Color(0.0f, 0.0f, 0.0f, 0.65f); // Soft black shadow
            painter.BeginPath();
            painter.Arc(shadowCenter, radius, 0f, 360f);
            painter.Stroke();

            // 1. Background Track
            painter.lineWidth = _lineThickness;
            painter.strokeColor = _trackColor;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            // 2. Progress Arc
            if (_progress > 0.001f)
            {
                float startAngle = -90f;
                float sweepAngle = 360f * _progress;

                painter.strokeColor = _fillColor;
                painter.BeginPath();
                painter.Arc(center, radius, startAngle, startAngle + sweepAngle);
                painter.Stroke();
            }
        }
    }
}
