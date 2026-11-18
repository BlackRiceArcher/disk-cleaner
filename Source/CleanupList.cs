using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class CleanupList : Control
    {
        private readonly List<CleanupItem> _items = new List<CleanupItem>();
        private int _hover = -1;
        private int _scroll;

        public event Action Changed;

        public string FooterText;

        public CleanupList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        public IList<CleanupItem> Items { get { return _items; } }

        public void SetItems(IList<CleanupItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _scroll = 0;
            _hover = -1;
            Invalidate();
        }

        private int RowHeight { get { return (int)Math.Round(50 * DeviceDpi / 96.0); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            int rowH = RowHeight;
            int total = _items.Count * rowH;
            int maxScroll = Math.Max(0, total - Height);
            if (_scroll > maxScroll) _scroll = maxScroll;

            g.SetClip(ClientRectangle);
            for (int i = 0; i < _items.Count; i++)
            {
                int y = i * rowH - _scroll;
                if (y + rowH < 0 || y > Height) continue;
                var row = new Rectangle(4, y + 1, Width - 8, rowH - 2);
                var item = _items[i];

                if (i == _hover)
                {
                    using (var b = new SolidBrush(Theme.Hover))
                        Theme.FillRound(g, row, 6f * s, Theme.Hover);
                }

                int cx = 14;
                int cy = y + rowH / 2;
                var box = new Rectangle(cx, cy - 9, 18, 18);
                if (item.Checked)
                {
                    Theme.FillRound(g, box, 4f, Theme.Navy);
                    using (var p = new Pen(Color.White, 2f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawLines(p, new[]
                        {
                            new PointF(box.X + 4.5f, box.Y + 9f),
                            new PointF(box.X + 7.8f, box.Y + 12.4f),
                            new PointF(box.X + 13.6f, box.Y + 5.6f)
                        });
                    }
                }
                else
                {
                    using (var path = Theme.Round(box, 4f))
                    {
                        using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                        using (var p = new Pen(Color.FromArgb(178, 186, 198), 1.4f)) g.DrawPath(p, path);
                    }
                }

                int textX = 40;
                int rightLimit = Width - 12;
                int pillW = (int)(38 * s);
                var pill = new Rectangle(rightLimit - pillW, cy - 10, pillW, 20);
                if (item.Caution)
                {
                    Theme.FillRound(g, pill, 10f, Theme.WarnSoft);
                    TextRenderer.DrawText(g, "谨慎", Theme.Tiny, pill, Theme.Warn,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    Theme.FillRound(g, pill, 10f, Theme.SafeSoft);
                    TextRenderer.DrawText(g, "安全", Theme.Tiny, pill, Theme.Safe,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }

                int sizeW = (int)(92 * s);
                var sizeRect = new Rectangle(pill.X - sizeW - 8, cy - 12, sizeW, 24);
                TextRenderer.DrawText(g, item.Size > 0 ? Theme.Size(item.Size) : "—", Theme.BodyBold, sizeRect,
                    item.Size > 0 ? Theme.Text : Theme.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                int nameW = Math.Max(30, sizeRect.X - textX - 8);
                TextRenderer.DrawText(g, item.Name, Theme.BodyBold,
                    new Rectangle(textX, y + 7, nameW, 20), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, item.Desc, Theme.Small,
                    new Rectangle(textX, y + 27, nameW, 16), Theme.Dim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            g.ResetClip();

            if (!string.IsNullOrEmpty(FooterText))
            {
                int fy = _items.Count * rowH - _scroll + (int)(12 * s);
                if (fy < Height - (int)(20 * s))
                {
                    TextRenderer.DrawText(g, FooterText, Theme.Small,
                        new Rectangle((int)(16 * s), fy, Width - (int)(36 * s), Height - fy - (int)(6 * s)),
                        Theme.Faint, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                }
            }

            if (total > Height)
            {
                int barH = Math.Max(24, (int)((double)Height * Height / total));
                int barY = (int)((double)_scroll / Math.Max(1, total - Height) * (Height - barH));
                Theme.FillRound(g, new RectangleF(Width - 4, barY, 3, barH), 1.5f, Color.FromArgb(214, 220, 228));
            }
        }

        private int RowIndexAt(Point p)
        {
            int idx = (p.Y + _scroll) / Math.Max(1, RowHeight);
            return idx >= 0 && idx < _items.Count ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = RowIndexAt(e.Location);
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int idx = RowIndexAt(e.Location);
            if (idx < 0) return;
            _items[idx].Checked = !_items[idx].Checked;
            Invalidate();
            if (Changed != null) Changed();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int total = _items.Count * RowHeight;
            int max = Math.Max(0, total - Height);
            _scroll -= e.Delta / 120 * Math.Max(1, RowHeight * 2 / 3);
            if (_scroll < 0) _scroll = 0;
            if (_scroll > max) _scroll = max;
            Invalidate();
        }
    }
}
