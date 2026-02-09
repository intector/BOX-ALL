using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BOX_ALL.Models;

using Font = Microsoft.Maui.Graphics.Font;

namespace BOX_ALL.Views
{
    /// <summary>
    /// Canvas drawable for BOXALL-96 mixed compartment layout.
    /// 
    /// Physical layout (top to bottom):
    ///   Rows J, I  — 6 MEDIUM compartments each (2× width, 1× height)  = 12
    ///   Rows H, G  — 6 LARGE  compartments each (2× width, 2× height)  = 12
    ///   [hinge gap]
    ///   Rows F–A   — 12 SMALL compartments each (standard 1×1)          = 72
    ///   Total: 96 compartments
    ///
    /// Position naming: sequential (G-01 through G-06, not G-01/G-03/G-05...)
    /// Overall pixel dimensions match GridDrawable (144) for consistent UI.
    /// </summary>
    public class GridDrawable96 : IDrawable
    {
        // Grid geometry — matches the 144 drawable for consistent sizing
        private const float CELL_SIZE = 45f;
        private const float HEADER_SIZE = 25f;
        private const float HINGE_GAP = 22.5f;

        // Layout constants
        private const int SMALL_COLS = 12;   // Columns in small rows (A–F)
        private const int LARGE_COLS = 6;    // Columns in large/medium rows (G–J)

        // Row Y-positions and heights (top to bottom, pre-hinge)
        // Top half: J(1h) + I(1h) + H(2h) + G(2h) = 6 cell heights = 270px
        // Bottom half: F–A = 6 cell heights = 270px
        // Total with header + gap = 587.5px (same as 144)

        private Dictionary<string, Models.Location> _locations = new Dictionary<string, Models.Location>();

        // Pre-computed compartment rectangles for drawing and hit testing
        private List<CompartmentRect> _compartments = new List<CompartmentRect>();

        // Colors (same as GridDrawable for visual consistency)
        private readonly Color EmptyColor = Color.FromArgb("#2A3050");
        private readonly Color OccupiedColor = Color.FromArgb("#4ADE80");
        private readonly Color LowStockColor = Color.FromArgb("#FBBF24");
        private readonly Color OutOfStockColor = Color.FromArgb("#FF6B6B");
        private readonly Color TextColor = Color.FromArgb("#94A3B8");
        private readonly Color DarkTextColor = Color.FromArgb("#0A0E27");
        private readonly Color HeaderTextColor = Color.FromArgb("#64748B");

        public float Width => (SMALL_COLS * CELL_SIZE) + HEADER_SIZE;   // 565px
        public float Height => (12 * CELL_SIZE) + HEADER_SIZE + HINGE_GAP; // 587.5px

        public GridDrawable96()
        {
            BuildCompartmentRects();
        }

        /// <summary>
        /// Pre-compute all 96 compartment rectangles with their positions, pixel bounds, and font sizes.
        /// </summary>
        private void BuildCompartmentRects()
        {
            _compartments.Clear();
            float topY = HEADER_SIZE; // Starting Y after header

            // === TOP HALF (above hinge) ===

            // Row J (index 0): medium — 6 compartments, 2× width, 1× height
            float rowY = topY;
            for (int i = 0; i < 6; i++)
            {
                _compartments.Add(new CompartmentRect
                {
                    Position = $"J-{(i + 1):D2}",
                    X = HEADER_SIZE + (i * 2 * CELL_SIZE),
                    Y = rowY,
                    W = 2 * CELL_SIZE,
                    H = CELL_SIZE,
                    RowLetter = 'J',
                    FontSizeLabel = 14f,
                    FontSizeQty = 12f
                });
            }

            // Row I (index 1): medium — 6 compartments, 2× width, 1× height
            rowY = topY + CELL_SIZE;
            for (int i = 0; i < 6; i++)
            {
                _compartments.Add(new CompartmentRect
                {
                    Position = $"I-{(i + 1):D2}",
                    X = HEADER_SIZE + (i * 2 * CELL_SIZE),
                    Y = rowY,
                    W = 2 * CELL_SIZE,
                    H = CELL_SIZE,
                    RowLetter = 'I',
                    FontSizeLabel = 14f,
                    FontSizeQty = 12f
                });
            }

            // Row H (index 2): large — 6 compartments, 2× width, 2× height
            rowY = topY + (2 * CELL_SIZE);
            for (int i = 0; i < 6; i++)
            {
                _compartments.Add(new CompartmentRect
                {
                    Position = $"H-{(i + 1):D2}",
                    X = HEADER_SIZE + (i * 2 * CELL_SIZE),
                    Y = rowY,
                    W = 2 * CELL_SIZE,
                    H = 2 * CELL_SIZE,
                    RowLetter = 'H',
                    FontSizeLabel = 16f,
                    FontSizeQty = 13f
                });
            }

            // Row G (index 3): large — 6 compartments, 2× width, 2× height
            rowY = topY + (4 * CELL_SIZE);
            for (int i = 0; i < 6; i++)
            {
                _compartments.Add(new CompartmentRect
                {
                    Position = $"G-{(i + 1):D2}",
                    X = HEADER_SIZE + (i * 2 * CELL_SIZE),
                    Y = rowY,
                    W = 2 * CELL_SIZE,
                    H = 2 * CELL_SIZE,
                    RowLetter = 'G',
                    FontSizeLabel = 16f,
                    FontSizeQty = 13f
                });
            }

            // === BOTTOM HALF (below hinge) — Rows F through A, standard small cells ===
            float bottomStartY = topY + (6 * CELL_SIZE) + HINGE_GAP;

            for (int rowIdx = 0; rowIdx < 6; rowIdx++)
            {
                char rowLetter = (char)('F' - rowIdx); // F, E, D, C, B, A
                rowY = bottomStartY + (rowIdx * CELL_SIZE);

                for (int col = 0; col < SMALL_COLS; col++)
                {
                    _compartments.Add(new CompartmentRect
                    {
                        Position = $"{rowLetter}-{(col + 1):D2}",
                        X = HEADER_SIZE + (col * CELL_SIZE),
                        Y = rowY,
                        W = CELL_SIZE,
                        H = CELL_SIZE,
                        RowLetter = rowLetter,
                        FontSizeLabel = 13f,
                        FontSizeQty = 11f
                    });
                }
            }
        }

        public void UpdateLocations(List<Models.Location>? locations)
        {
            _locations.Clear();
            if (locations != null)
            {
                foreach (var loc in locations)
                {
                    if (!string.IsNullOrEmpty(loc.Position))
                    {
                        _locations[loc.Position] = loc;
                    }
                }
            }
        }

        public string? GetPositionFromPoint(PointF point)
        {
            // Check each compartment rectangle for a hit
            foreach (var rect in _compartments)
            {
                if (point.X >= rect.X && point.X < rect.X + rect.W &&
                    point.Y >= rect.Y && point.Y < rect.Y + rect.H)
                {
                    return rect.Position;
                }
            }
            return null;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var sw = Stopwatch.StartNew();

            // Clear background
            canvas.FillColor = Color.FromArgb("#0A0E27");
            canvas.FillRectangle(dirtyRect);

            // Draw headers
            DrawColumnHeaders(canvas);
            DrawRowHeaders(canvas);

            // Draw all compartments
            foreach (var rect in _compartments)
            {
                DrawCompartment(canvas, rect);
            }

            Debug.WriteLine($"Grid96 drawn in {sw.ElapsedMilliseconds}ms");
        }

        private void DrawColumnHeaders(ICanvas canvas)
        {
            canvas.FontColor = HeaderTextColor;
            canvas.Font = Font.Default;

            // Top half column headers: 01–06 (centered in 2-cell-wide spaces)
            canvas.FontSize = 12;
            for (int i = 0; i < LARGE_COLS; i++)
            {
                float x = HEADER_SIZE + (i * 2 * CELL_SIZE) + CELL_SIZE; // center of 2-cell span
                float y = HEADER_SIZE / 2;
                string text = (i + 1).ToString("D2");
                canvas.DrawString(text, x - 10, y - 5, 20, 10,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            // Bottom half column headers: 01–12
            float bottomHeaderY = HEADER_SIZE + (6 * CELL_SIZE) + HINGE_GAP - 14;
            canvas.FontSize = 10;
            for (int col = 0; col < SMALL_COLS; col++)
            {
                float x = HEADER_SIZE + (col * CELL_SIZE) + (CELL_SIZE / 2);
                string text = (col + 1).ToString("D2");
                canvas.DrawString(text, x - 10, bottomHeaderY - 3, 20, 10,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawRowHeaders(ICanvas canvas)
        {
            canvas.FontColor = HeaderTextColor;
            canvas.FontSize = 13;
            canvas.Font = Font.Default;

            float topY = HEADER_SIZE;

            // J (medium, 1h)
            DrawRowLabel(canvas, 'J', topY, CELL_SIZE);
            // I (medium, 1h)
            DrawRowLabel(canvas, 'I', topY + CELL_SIZE, CELL_SIZE);
            // H (large, 2h)
            DrawRowLabel(canvas, 'H', topY + (2 * CELL_SIZE), 2 * CELL_SIZE);
            // G (large, 2h)
            DrawRowLabel(canvas, 'G', topY + (4 * CELL_SIZE), 2 * CELL_SIZE);

            // Bottom half: F through A
            float bottomStartY = topY + (6 * CELL_SIZE) + HINGE_GAP;
            for (int i = 0; i < 6; i++)
            {
                char letter = (char)('F' - i);
                DrawRowLabel(canvas, letter, bottomStartY + (i * CELL_SIZE), CELL_SIZE);
            }
        }

        private void DrawRowLabel(ICanvas canvas, char letter, float y, float height)
        {
            float x = HEADER_SIZE / 2;
            float centerY = y + (height / 2);
            canvas.DrawString(letter.ToString(), x - 10, centerY - 5, 20, 10,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        private void DrawCompartment(ICanvas canvas, CompartmentRect rect)
        {
            // Determine color and content based on location data
            Color bgColor = EmptyColor;
            Color textColor = TextColor;
            string quantityText = "";
            bool hasComponent = false;

            if (_locations.TryGetValue(rect.Position, out var location) && location.Component != null)
            {
                hasComponent = true;
                if (location.IsEmpty)
                {
                    bgColor = OutOfStockColor;
                    textColor = DarkTextColor;
                }
                else if (location.IsLowStock)
                {
                    bgColor = LowStockColor;
                    textColor = DarkTextColor;
                }
                else
                {
                    bgColor = OccupiedColor;
                    textColor = DarkTextColor;
                }
                quantityText = $"Q:{location.Quantity}";
            }

            // Fill compartment background with rounded rectangle
            canvas.FillColor = bgColor;
            canvas.FillRoundedRectangle(rect.X + 2, rect.Y + 2, rect.W - 4, rect.H - 4, 4);

            // Draw position label (centered in compartment)
            canvas.FontColor = textColor;
            canvas.FontSize = rect.FontSizeLabel;
            canvas.Font = Font.Default;

            float labelY = rect.Y + (rect.H / 2) - (hasComponent ? 8 : 0);
            canvas.DrawString(rect.Position, rect.X, labelY, rect.W, 20,
                HorizontalAlignment.Center, VerticalAlignment.Center);

            // Draw quantity if occupied
            if (hasComponent && !string.IsNullOrEmpty(quantityText))
            {
                canvas.FontSize = rect.FontSizeQty;
                float qtyY = rect.Y + (rect.H / 2) + 8;
                canvas.DrawString(quantityText, rect.X, qtyY, rect.W, 20,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        /// <summary>
        /// Internal struct for pre-computed compartment geometry.
        /// </summary>
        private struct CompartmentRect
        {
            public string Position;
            public float X, Y, W, H;
            public char RowLetter;
            public float FontSizeLabel;
            public float FontSizeQty;
        }
    }
}
