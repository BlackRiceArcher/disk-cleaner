using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class DriveBarInfo
    {
        public string Root;
        public long Free;
        public long Total;
    }

    internal sealed class DriveBar : Control
    {
        private readonly List<DriveBarInfo> _drives = new List<DriveBarInfo>();
        private readonly List<Rectangle> _rects = new List<Rectangle>();
        private string _active;
        private int _hover = -1;

        public event Action<string> DriveSelected;

        public DriveBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void SetDrives(IList<DriveBarInfo> drives, string active)
        {
            _drives.Clear();
            _drives.AddRange(drives);
            _active = active;
            _hover = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;

            int h = (int)(32 * s);
            int y = (Height - h) / 2;
            int x = (int)(18 * s);
            _rects.Clear();

            for (int i = 0; i < _drives.Count; i++)
            {
                var d = _drives[i];
                string label = d.Root.Length >= 2 ? d.Root.Substring(0, 2) : d.Root;
                string sub = "可用 " + Theme.Size(d.Free);

                int lw = TextRenderer.MeasureText(label, Theme.H3).Width;
                int sw = TextRenderer.MeasureText(sub, Theme.Small).Width;
                int w = (int)(14 * s) + lw + (int)(10 * s) + sw + (int)(14 * s);
                var r = new Rectangle(x, y, w, h);
                if (x + w > Width - (int)(12 * s)) break;
                _rects.Add(r);

                bool active = _active != null && string.Equals(d.Root, _active, StringComparison.OrdinalIgnoreCase);
                bool hover = i == _hover;

                using (var path = Theme.Round(r, h / 2f))
                {
                    if (active)
                    {
                        using (var b = new SolidBrush(Theme.Navy)) g.FillPath(b, path);
                    }
                    else
                    {
                        using (var b = new SolidBrush(hover ? Theme.Hover : Color.White)) g.FillPath(b, path);
                        using (var p = new Pen(Theme.Border, 1f)) g.DrawPath(p, path);
                    }
                }

                var labelRect = new Rectangle(r.X + (int)(14 * s), r.Y, lw, r.Height);
                var subRect = new Rectangle(labelRect.Right + (int)(10 * s), r.Y, sw, r.Height);
                TextRenderer.DrawText(g, label, Theme.H3, labelRect,
                    active ? Color.White : Theme.Navy,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, sub, Theme.Small, subRect,
                    active ? Theme.GoldSoft : Theme.Dim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                x += w + (int)(8 * s);
            }

            if (x < Width - (int)(300 * s))
            {
                TextRenderer.DrawText(g, "点击磁盘切换扫描对象", Theme.Small,
                    new Rectangle(x + (int)(8 * s), 0, Math.Max(60, Width - x - (int)(26 * s)), Height),
                    Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            using (var p = new Pen(Theme.Line))
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

        private int IndexAt(Point p)
        {
            for (int i = 0; i < _rects.Count; i++)
            {
                if (_rects[i].Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = IndexAt(e.Location);
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

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = IndexAt(e.Location);
            if (idx >= 0 && idx < _drives.Count && DriveSelected != null)
                DriveSelected(_drives[idx].Root);
        }
    }
}
