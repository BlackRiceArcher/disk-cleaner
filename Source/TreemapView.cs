using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class TreemapView : Control
    {
        private Node _root;
        private readonly List<Node> _items = new List<Node>();
        private RectangleF[] _rects = new RectangleF[0];
        private int _hover = -1;
        private Point _mouse;
        private const int MaxItems = 3000;

        public event Action<Node> NodeActivated;
        public event Action<Node, Point> MenuRequested;

        public TreemapView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            BackColor = Color.White;
        }

        public Node Root { get { return _root; } }

        public void SetRoot(Node root)
        {
            _root = root;
            _items.Clear();
            if (root != null && root.Children != null)
            {
                foreach (var c in root.Children)
                {
                    if (c.Size <= 0) continue;
                    _items.Add(c);
                    if (_items.Count >= MaxItems) break;
                }
            }
            _hover = -1;
            Recompute();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Recompute();
            Invalidate();
        }

        private void Recompute()
        {
            if (_items.Count == 0 || Width <= 4 || Height <= 4)
            {
                _rects = new RectangleF[0];
                return;
            }
            var values = new double[_items.Count];
            for (int i = 0; i < _items.Count; i++) values[i] = _items[i].Size;
            _rects = TreemapLayout.Compute(values, new RectangleF(1f, 1f, Width - 2f, Height - 2f));
        }

        private Node NodeAt(Point p)
        {
            for (int i = 0; i < _rects.Length; i++)
            {
                if (_rects[i].Width > 0 && _rects[i].Contains(p)) return _items[i];
            }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.None;

            if (_root == null)
            {
                DrawCenter(g, "正在读取磁盘信息…");
                return;
            }
            if (_items.Count == 0)
            {
                DrawCenter(g, "该目录没有可分析的数据");
                return;
            }

            float s = DeviceDpi / 96f;
            for (int i = 0; i < _rects.Length && i < _items.Count; i++)
            {
                var r = _rects[i];
                if (r.Width < 1.6f || r.Height < 1.6f) continue;
                var rr = new RectangleF(r.X + 0.6f, r.Y + 0.6f, r.Width - 1.2f, r.Height - 1.2f);
                using (var b = new SolidBrush(Theme.NodeColor(_items[i])))
                    g.FillRectangle(b, rr);

                if (rr.Width >= 70 * s && rr.Height >= 34 * s)
                {
                    var nameRect = new Rectangle((int)rr.X + 6, (int)rr.Y + 4, (int)rr.Width - 12, 18);
                    TextRenderer.DrawText(g, _items[i].Name, Theme.SmallBold, nameRect, Color.White,
                        TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    var sizeRect = new Rectangle((int)rr.X + 6, (int)rr.Y + 22, (int)rr.Width - 12, 14);
                    TextRenderer.DrawText(g, Theme.Size(_items[i].Size), Theme.Tiny, sizeRect,
                        Color.FromArgb(218, 229, 242),
                        TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                    if (rr.Width >= 110 * s && rr.Height >= 110 * s && _root != null && _root.Size > 0)
                    {
                        string pct = string.Format("{0:0.#}%", _items[i].Size * 100.0 / _root.Size);
                        var pctRect = new Rectangle((int)rr.X, (int)(rr.Y + rr.Height / 2), (int)rr.Width, 26);
                        TextRenderer.DrawText(g, pct, Theme.Num, pctRect, Color.FromArgb(200, 214, 232),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    }
                }
                else if (rr.Width >= 44 * s && rr.Height >= 17 * s)
                {
                    var nameRect = new Rectangle((int)rr.X + 4, (int)rr.Y + 2, (int)rr.Width - 8, (int)rr.Height - 4);
                    TextRenderer.DrawText(g, _items[i].Name, Theme.Tiny, nameRect, Color.White,
                        TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }

                if (i == _hover)
                {
                    using (var p = new Pen(Theme.Gold, 2f))
                        g.DrawRectangle(p, rr.X + 1, rr.Y + 1, Math.Max(1f, rr.Width - 2), Math.Max(1f, rr.Height - 2));
                }
            }

            if (_hover >= 0 && _hover < _items.Count) DrawTooltip(g, _items[_hover]);
        }

        private void DrawCenter(Graphics g, string text)
        {
            TextRenderer.DrawText(g, text, Theme.Body, ClientRectangle, Theme.Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void DrawTooltip(Graphics g, Node node)
        {
            float s = DeviceDpi / 96f;
            string title = node.Name;
            if (title.Length > 52) title = title.Substring(0, 50) + "…";
            string path = node.FullPath;
            if (path.Length > 68) path = path.Substring(0, 30) + " … " + path.Substring(path.Length - 34);

            double pct = _root != null && _root.Size > 0 ? node.Size * 100.0 / _root.Size : 0;
            string line2 = Theme.Size(node.Size) + "   ·   " + string.Format("{0:0.#}%", pct) +
                           (node.IsDir ? "   ·   文件夹" : "   ·   文件");
            if (node.Modified.Year > 1900 && !node.IsDir)
                line2 += "   ·   " + node.Modified.ToString("yyyy-MM-dd");

            int w = Math.Max(TextRenderer.MeasureText(title, Theme.SmallBold).Width,
                     Math.Max(TextRenderer.MeasureText(line2, Theme.Small).Width,
                              TextRenderer.MeasureText(path, Theme.Tiny).Width)) + 24;
            w = Math.Min(w, Width - 16);
            int h = (int)(64 * s);

            int x = _mouse.X + 16;
            int y = _mouse.Y + 18;
            if (x + w > Width - 6) x = _mouse.X - w - 12;
            if (y + h > Height - 6) y = _mouse.Y - h - 12;
            if (x < 4) x = 4;
            if (y < 4) y = 4;

            var box = new Rectangle(x, y, w, h);
            using (var path2 = Theme.Round(box, 6f * s))
            {
                using (var b = new SolidBrush(Color.FromArgb(15, 27, 45))) g.FillPath(b, path2);
                using (var p = new Pen(Theme.Gold, 1f)) g.DrawPath(p, path2);
            }
            TextRenderer.DrawText(g, title, Theme.SmallBold,
                new Rectangle(x + 12, y + 7, w - 24, 18), Color.White,
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, line2, Theme.Small,
                new Rectangle(x + 12, y + 26, w - 24, 17), Theme.GoldSoft,
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, path, Theme.Tiny,
                new Rectangle(x + 12, y + 43, w - 24, 15), Color.FromArgb(176, 190, 208),
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = e.Location;
            int idx = -1;
            for (int i = 0; i < _rects.Length; i++)
            {
                if (_rects[i].Width > 0 && _rects[i].Contains(e.Location)) { idx = i; break; }
            }
            Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            if (idx != _hover || idx >= 0)
            {
                _hover = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            var n = NodeAt(PointToClient(Cursor.Position));
            if (n != null && NodeActivated != null) NodeActivated(n);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                var n = NodeAt(e.Location);
                if (n != null && MenuRequested != null) MenuRequested(n, PointToScreen(e.Location));
            }
        }
    }
}
