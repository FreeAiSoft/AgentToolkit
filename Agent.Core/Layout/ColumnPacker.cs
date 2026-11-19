using System.Collections.Generic;

namespace Agent.Core.Layout
{
    /// <summary>
    /// Pure column-packing helper used by the chat input's top-overflow wrap panel.
    /// Splits a list of item heights into columns that are filled from the last item
    /// upward (bottom-anchored), breaking into a new column when the current column
    /// would exceed either the available height or the maximum item count per column.
    /// </summary>
    public static class ColumnPacker
    {
        /// <summary>
        /// Packs item heights into columns. Each returned column is a list of original
        /// indices, ordered so that the item nearest the bottom of the column comes first.
        /// </summary>
        /// <param name="itemHeights">Heights of the items in their original order.</param>
        /// <param name="spacing">Vertical gap between items within a column.</param>
        /// <param name="availableHeight">
        /// Available height for a single column; pass <see cref="double.PositiveInfinity"/>
        /// for an unlimited height.
        /// </param>
        /// <param name="maxItemsPerColumn">
        /// Maximum number of items allowed in one column once the column has to overflow
        /// to the right; zero or less means unlimited. Ignored when the whole list fits in
        /// a single column within <paramref name="availableHeight"/>.
        /// </param>
        public static List<List<int>> Pack(
            IReadOnlyList<double> itemHeights,
            double spacing,
            double availableHeight,
            int maxItemsPerColumn)
        {
            var columns = new List<List<int>>();
            if (itemHeights == null || itemHeights.Count == 0)
                return columns;

            // Keep everything in a single bottom-anchored column while the available
            // height accommodates the whole list. The per-column item limit only applies
            // once the column actually overflows to the right.
            if (FitsInSingleColumn(itemHeights, spacing, availableHeight))
            {
                var single = new List<int>(itemHeights.Count);
                for (int i = itemHeights.Count - 1; i >= 0; i--)
                    single.Add(i);
                columns.Add(single);
                return columns;
            }

            var current = new List<int>();
            columns.Add(current);

            double usedHeight = 0;
            for (int i = itemHeights.Count - 1; i >= 0; i--)
            {
                double height = itemHeights[i];
                double additional = current.Count == 0 ? height : spacing + height;

                bool limitReached = maxItemsPerColumn > 0 && current.Count >= maxItemsPerColumn;
                if (limitReached || (current.Count > 0 && usedHeight + additional > availableHeight))
                {
                    current = new List<int>();
                    columns.Add(current);
                    usedHeight = 0;
                    additional = height;
                }

                current.Add(i);
                usedHeight += additional;
            }

            return columns;
        }

        private static bool FitsInSingleColumn(
            IReadOnlyList<double> itemHeights,
            double spacing,
            double availableHeight)
        {
            double total = 0;
            for (int i = 0; i < itemHeights.Count; i++)
            {
                if (i > 0)
                    total += spacing;
                total += itemHeights[i];
                if (total > availableHeight)
                    return false;
            }

            return true;
        }
    }
}
