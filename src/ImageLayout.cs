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

        sealed class Node
        {
            internal double X, Y, Width, Height;
            internal bool Used;
            internal Node Right, Down;
            internal Node(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }
            internal Node Find(double w, double h)
            {
                var pending = new Stack<Node>(); pending.Push(this);
                while (pending.Count > 0)
                {
                    Node node = pending.Pop();
                    if (node.Used)
                    {
                        if (node.Down != null) pending.Push(node.Down);
                        if (node.Right != null) pending.Push(node.Right);
                    }
                    else if (w <= node.Width + 1e-8 && h <= node.Height + 1e-8) return node;
                }
                return null;
            }
            internal void Split(double w, double h)
            {
                Used = true;
                Right = new Node(X + w, Y, Math.Max(0, Width - w), h);
                Down = new Node(X, Y + h, Width, Math.Max(0, Height - h));
            }
        }
        internal static void Pack(IList<ImageItem> items, double gap)
        {
            if (items.Count < 2) return;
            var blocks = items.Select(i => new { Item = i, Bounds = i.Bounds() })
                .OrderByDescending(b => Math.Max(b.Bounds.Width, b.Bounds.Height))
                .ThenByDescending(b => b.Bounds.Width * b.Bounds.Height).ToList();
            double left = blocks.Min(b => b.Bounds.Left), top = blocks.Min(b => b.Bounds.Top);
            Node root = new Node(0, 0, blocks[0].Bounds.Width + gap, blocks[0].Bounds.Height + gap);
            foreach (var block in blocks)
            {
                double w = block.Bounds.Width + gap, h = block.Bounds.Height + gap;
                Node fit = root.Find(w, h);
                if (fit == null)
                {
                    bool right = h <= root.Height + 1e-8, down = w <= root.Width + 1e-8;
                    if (right && (!down || Math.Abs(Math.Log((root.Width + w) / root.Height / 1.4)) <=
                        Math.Abs(Math.Log(root.Width / (root.Height + h) / 1.4))))
                    {
                        root = new Node(0, 0, root.Width + w, root.Height) { Used = true, Down = root,
                            Right = new Node(root.Width, 0, w, root.Height) };
                    }
                    else if (down)
                    {
                        root = new Node(0, 0, root.Width, root.Height + h) { Used = true, Right = root,
                            Down = new Node(0, root.Height, root.Width, h) };
                    }
                    else throw new InvalidOperationException("Unable to pack these image dimensions.");
                    fit = root.Find(w, h);
                }
                fit.Split(w, h);
                block.Item.X = left + fit.X + block.Bounds.Width / 2;
                block.Item.Y = top + fit.Y + block.Bounds.Height / 2;
            }
        }
    }
}
