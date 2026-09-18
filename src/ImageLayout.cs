using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ArkBoard
{
    internal static class ImageLayout
    {
        internal static void Normalize(IList<ImageItem> items)
        {
            if (items.Count < 2) return;
            double target = items.Average(i => Math.Max(i.Width, i.Height));
            foreach (ImageItem i in items)
            {
                double scale = target / Math.Max(i.Width, i.Height);
                i.Width *= scale; i.Height *= scale;
            }
        }

        sealed class Block
        {
            internal ImageItem Item;
            internal Rect Bounds;
            internal double Width, Height;
        }
        sealed class Packing
        {
            internal Rect[] Places;
            internal double Score;
        }
        static bool Overlaps(Rect a, Rect b)
        { return a.Left < b.Right - 1e-8 && a.Right > b.Left + 1e-8 && a.Top < b.Bottom - 1e-8 && a.Bottom > b.Top + 1e-8; }
        static bool Contains(Rect outer, Rect inner)
        { return inner.Left >= outer.Left - 1e-8 && inner.Top >= outer.Top - 1e-8 && inner.Right <= outer.Right + 1e-8 && inner.Bottom <= outer.Bottom + 1e-8; }
        static void SplitFreeRectangles(List<Rect> free, Rect used)
        {
            for (int index = free.Count - 1; index >= 0; index--)
            {
                Rect space = free[index];
                if (!Overlaps(space, used)) continue;
                free.RemoveAt(index);
                if (used.Left > space.Left + 1e-8) free.Add(new Rect(space.Left, space.Top, used.Left - space.Left, space.Height));
                if (used.Right < space.Right - 1e-8) free.Add(new Rect(used.Right, space.Top, space.Right - used.Right, space.Height));
                if (used.Top > space.Top + 1e-8) free.Add(new Rect(space.Left, space.Top, space.Width, used.Top - space.Top));
                if (used.Bottom < space.Bottom - 1e-8) free.Add(new Rect(space.Left, used.Bottom, space.Width, space.Bottom - used.Bottom));
            }
            for (int a = free.Count - 1; a >= 0; a--)
            {
                if (free[a].Width <= 1e-8 || free[a].Height <= 1e-8) { free.RemoveAt(a); continue; }
                for (int b = 0; b < free.Count; b++) if (a != b && Contains(free[b], free[a]))
                { free.RemoveAt(a); break; }
            }
        }

        internal static void AlignWidth(IList<ImageItem> items)
        {
            if (items.Count < 2) return;
            double target = items.Average(i => i.Width);
            foreach (ImageItem item in items)
            {
                double scale = target / item.Width;
                item.Width = target; item.Height *= scale;
            }
        }

        internal static void AlignHeight(IList<ImageItem> items)
        {
            if (items.Count < 2) return;
            double target = items.Average(i => i.Height);
            foreach (ImageItem item in items)
            {
                double scale = target / item.Height;
                item.Height = target; item.Width *= scale;
            }
        }
        static Packing TryPack(IList<Block> blocks, double width)
        {
            double heightLimit = blocks.Sum(b => b.Height);
            var free = new List<Rect> { new Rect(0, 0, width, heightLimit) };
            var places = new Rect[blocks.Count];
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                Block block = blocks[blockIndex]; int best = -1;
                double bestBottom = double.MaxValue, bestX = double.MaxValue, bestWaste = double.MaxValue;
                for (int index = 0; index < free.Count; index++)
                {
                    Rect space = free[index];
                    if (block.Width > space.Width + 1e-8 || block.Height > space.Height + 1e-8) continue;
                    double bottom = space.Top + block.Height;
                    double waste = space.Width * space.Height - block.Width * block.Height;
                    if (bottom < bestBottom - 1e-8 || (Math.Abs(bottom - bestBottom) < 1e-8 &&
                        (space.Left < bestX - 1e-8 || (Math.Abs(space.Left - bestX) < 1e-8 && waste < bestWaste))))
                    { best = index; bestBottom = bottom; bestX = space.Left; bestWaste = waste; }
                }
                if (best < 0) return null;
                Rect place = new Rect(free[best].Left, free[best].Top, block.Width, block.Height);
                places[blockIndex] = place; SplitFreeRectangles(free, place);
            }
            double usedWidth = places.Max(r => r.Right), usedHeight = places.Max(r => r.Bottom);
            double aspectPenalty = Math.Abs(Math.Log(Math.Max(1e-8, usedWidth / usedHeight) / 1.4));
            return new Packing { Places = places, Score = usedWidth * usedHeight * (1 + .12 * aspectPenalty) };
        }
        internal static void Pack(IList<ImageItem> items, double gap)
        {
            if (items.Count < 2) return;
            var blocks = items.Select(i => new Block { Item = i, Bounds = i.Bounds(), Width = i.Bounds().Width + gap, Height = i.Bounds().Height + gap })
                .OrderByDescending(b => b.Width * b.Height).ThenByDescending(b => Math.Max(b.Width, b.Height)).ToList();
            double left = blocks.Min(b => b.Bounds.Left), top = blocks.Min(b => b.Bounds.Top);
            double totalArea = blocks.Sum(b => b.Width * b.Height), widest = blocks.Max(b => b.Width);
            double idealWidth = Math.Max(widest, Math.Sqrt(totalArea * 1.4));
            var widths = new List<double> { widest };
            for (int step = -8; step <= 16; step++) widths.Add(Math.Max(widest, idealWidth * (1 + step * .05)));
            Packing best = null;
            foreach (double width in widths.Distinct().OrderBy(w => w))
            {
                Packing candidate = TryPack(blocks, width);
                if (candidate != null && (best == null || candidate.Score < best.Score)) best = candidate;
            }
            if (best == null) throw new InvalidOperationException("Unable to pack these image dimensions.");
            for (int index = 0; index < blocks.Count; index++)
            {
                Block block = blocks[index]; Rect place = best.Places[index];
                block.Item.X = left + place.X + block.Bounds.Width / 2;
                block.Item.Y = top + place.Y + block.Bounds.Height / 2;
            }
        }
    }
}
