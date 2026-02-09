using System.Collections.Generic;

namespace BOX_ALL.Helpers
{
    /// <summary>
    /// Generates valid compartment position labels for each box type.
    /// BOXALL-144: Uniform 12×12 grid, positions L-01 through A-12 (144 total)
    /// BOXALL-96:  Mixed layout, positions A-01 through F-12 (small) + G-01 through J-06 (large/medium) (96 total)
    /// </summary>
    public static class PositionHelper
    {
        /// <summary>
        /// Returns all valid positions for the given box type, ordered top-to-bottom, left-to-right.
        /// </summary>
        public static List<string> GetAllPositions(string boxType)
        {
            if (Is96Type(boxType))
                return GetPositions96();
            else
                return GetPositions144();
        }

        /// <summary>
        /// Returns all valid positions for the given box type, excluding the specified position.
        /// Useful for relocation pickers.
        /// </summary>
        public static List<string> GetAllPositionsExcept(string boxType, string excludePosition)
        {
            var positions = GetAllPositions(boxType);
            positions.Remove(excludePosition);
            return positions;
        }

        /// <summary>
        /// Validates whether a position string is valid for the given box type.
        /// </summary>
        public static bool IsValidPosition(string boxType, string position)
        {
            if (string.IsNullOrEmpty(position)) return false;

            if (Is96Type(boxType))
                return IsValidPosition96(position);
            else
                return IsValidPosition144(position);
        }

        /// <summary>
        /// Gets the regex pattern for valid positions of the given box type.
        /// </summary>
        public static string GetValidationPattern(string boxType)
        {
            if (Is96Type(boxType))
                return @"^[A-J]-\d{2}$";
            else
                return @"^[A-L]-\d{2}$";
        }

        public static bool Is96Type(string boxType)
        {
            return boxType != null && (boxType.Contains("96") || boxType == "BOXALL96" || boxType == "BOXALL96AS");
        }

        // --- BOXALL-144: 12 rows (L→A) × 12 columns ---
        private static List<string> GetPositions144()
        {
            var positions = new List<string>(144);
            for (char row = 'A'; row <= 'L'; row++)
            {
                for (int col = 1; col <= 12; col++)
                {
                    positions.Add($"{row}-{col:D2}");
                }
            }
            return positions;
        }

        private static bool IsValidPosition144(string position)
        {
            if (position.Length < 4) return false;
            var parts = position.Split('-');
            if (parts.Length != 2) return false;
            char row = parts[0][0];
            if (row < 'A' || row > 'L') return false;
            if (!int.TryParse(parts[1], out int col)) return false;
            return col >= 1 && col <= 12;
        }

        // --- BOXALL-96: Mixed layout ---
        // Rows A–F: 12 small compartments each (01–12)
        // Rows G–H: 6 large compartments each (01–06), 2× width, 2× height
        // Rows I–J: 6 medium compartments each (01–06), 2× width, 1× height
        private static List<string> GetPositions96()
        {
            var positions = new List<string>(96);

            // Top rows first (J→I: medium, 6 each)
            for (char row = 'I'; row <= 'J'; row++)
            {
                for (int col = 1; col <= 6; col++)
                {
                    positions.Add($"{row}-{col:D2}");
                }
            }

            // Large rows (H→G: 6 each)
            for (char row = 'G'; row <= 'H'; row++)
            {
                for (int col = 1; col <= 6; col++)
                {
                    positions.Add($"{row}-{col:D2}");
                }
            }

            // Small rows (F→A: 12 each)
            for (char row = 'A'; row <= 'F'; row++)
            {
                for (int col = 1; col <= 12; col++)
                {
                    positions.Add($"{row}-{col:D2}");
                }
            }

            return positions;
        }

        private static bool IsValidPosition96(string position)
        {
            if (position.Length < 4) return false;
            var parts = position.Split('-');
            if (parts.Length != 2) return false;
            char row = parts[0][0];
            if (row < 'A' || row > 'J') return false;
            if (!int.TryParse(parts[1], out int col)) return false;

            // Rows A-F: columns 1-12, Rows G-J: columns 1-6
            if (row >= 'G')
                return col >= 1 && col <= 6;
            else
                return col >= 1 && col <= 12;
        }
    }
}
