using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BOX_ALL.Models;
using Font = Microsoft.Maui.Graphics.Font;

namespace BOX_ALL.Views
{
    public class GridDrawable : IDrawable
    {
        private const int ROWS = 12;
        private const int COLS = 12;
        private const float CELL_SIZE = 45;
        private const float HEADER_SIZE = 25;
        private const float HINGE_GAP = 22.5f; // Half of cell size for the hinge gap

        private Dictionary<string, Models.Location> _locations = new Dictionary<string, Models.Location>();

        // Colors
        private readonly Color EmptyColor = Color.FromArgb("#2A3050");
        private readonly Color OccupiedColor = Color.FromArgb("#4ADE80");
        private readonly Color LowStockColor = Color.FromArgb("#FBBF24");
        private readonly Color OutOfStockColor = Color.FromArgb("#FF6B6B");
        private readonly Color GridLineColor = Color.FromArgb("#1C2141");
        private readonly Color TextColor = Color.FromArgb("#94A3B8");
        private readonly Color DarkTextColor = Color.FromArgb("#0A0E27");
        private readonly Color HeaderTextColor = Color.FromArgb("#64748B");

        public float Width => (COLS * CELL_SIZE) + HEADER_SIZE;
        public float Height => (ROWS * CELL_SIZE) + HEADER_SIZE + HINGE_GAP; // Added hinge gap to total height

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
            // Account for headers
            float x = point.X - HEADER_SIZE;
            float y = point.Y - HEADER_SIZE;

            if (x < 0 || y < 0) return null;

            int col = (int)(x / CELL_SIZE);
            int row;

            // Account for hinge gap - rows G-L (indices 6-11) are shifted down
            if (y < 6 * CELL_SIZE)
            {
                // Top half (L-G rows, indices 0-5)
                row = (int)(y / CELL_SIZE);
            }
            else if (y < 6 * CELL_SIZE + HINGE_GAP)
            {
                // In the gap - no compartment
                return null;
            }
            else
            {
                // Bottom half (F-A rows, indices 6-11)
                row = (int)((y - HINGE_GAP) / CELL_SIZE);
            }

            if (col >= 0 && col < COLS && row >= 0 && row < ROWS)
            {
                char rowLetter = (char)('L' - row);
                return $"{rowLetter}-{(col + 1):D2}";
            }

            return null;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var sw = Stopwatch.StartNew();

            // Clear background
            canvas.FillColor = Color.FromArgb("#0A0E27");
            canvas.FillRectangle(dirtyRect);

            // Draw column headers (1-12)
            canvas.FontColor = HeaderTextColor;
            canvas.FontSize = 12;  // Increased from 10
            canvas.Font = Font.Default;

            for (int col = 0; col < COLS; col++)
            {
                float x = HEADER_SIZE + (col * CELL_SIZE) + (CELL_SIZE / 2);
                float y = HEADER_SIZE / 2;

                string text = (col + 1).ToString("D2");
                canvas.DrawString(text, x - 10, y - 5, 20, 10,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            // Draw row headers (L to A)
            canvas.FontSize = 13;  // Increased from 10
            for (int row = 0; row < ROWS; row++)
            {
                float x = HEADER_SIZE / 2;
                float y = HEADER_SIZE + (row * CELL_SIZE) + (CELL_SIZE / 2);

                // Add hinge gap offset for bottom half (rows F-A, indices 6-11)
                if (row >= 6)
                {
                    y += HINGE_GAP;
                }

                char rowLetter = (char)('L' - row);
                canvas.DrawString(rowLetter.ToString(), x - 10, y - 5, 20, 10,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            // Draw all compartments
            for (int row = 0; row < ROWS; row++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    DrawCompartment(canvas, row, col);
                }
            }

            // No grid lines needed since we have gaps between rounded rectangles

            Debug.WriteLine($"Grid drawn in {sw.ElapsedMilliseconds}ms");
        }

        private void DrawCompartment(ICanvas canvas, int row, int col)
        {
            float x = HEADER_SIZE + (col * CELL_SIZE);
            float y = HEADER_SIZE + (row * CELL_SIZE);

            // Add hinge gap offset for bottom half (rows F-A, indices 6-11)
            if (row >= 6)
            {
                y += HINGE_GAP;
            }

            // Get position label
            char rowLetter = (char)('L' - row);
            string position = $"{rowLetter}-{(col + 1):D2}";

            // Determine color and content based on location data
            Color bgColor = EmptyColor;
            Color textColor = TextColor;
            string quantityText = "";
            bool hasComponent = false;

            if (_locations.TryGetValue(position, out var location) && location.Component != null)
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
            canvas.FillRoundedRectangle(x + 2, y + 2, CELL_SIZE - 4, CELL_SIZE - 4, 4);  // 4px corner radius

            // Draw position label - increased size
            canvas.FontColor = textColor;
            canvas.FontSize = 13;  // Increased from 11
            canvas.Font = Font.Default;
            canvas.DrawString(position, x, y + 8, CELL_SIZE, 20,
                HorizontalAlignment.Center, VerticalAlignment.Center);

            // Draw quantity if occupied
            if (hasComponent && !string.IsNullOrEmpty(quantityText))
            {
                canvas.FontSize = 11;  // Increased from 10
                canvas.DrawString(quantityText, x, y + 22, CELL_SIZE, 20,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }
}