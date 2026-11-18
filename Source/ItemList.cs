using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class ItemList : Control
    {
        public sealed class Row
        {
            public Node Node;
            public string Name;
            public long Size;
            public double Percent;
            public DateTime Modified;
            public bool IsDir;
        }

        private readonly List<Row> _rows = new List<Row>();
        private int _hover = -1;
        private int _selected = -1;
        private int _scroll;

        public event Action<Row> RowActivated;
        public event Action<Row, Point> MenuRequested;

        public ItemList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            BackColor = Color.White;
        }

        public void SetRows(IList<Row> rows)
        {
            _rows.Clear();
            _rows.AddRange(rows);
            _scroll = 0;
            _hover = -1;
            _selected = -1;
            Invalidate();
        }

        private int RowHeight { get { return (int)Math.Round(31 * DeviceDpi / 96.0); } }
        private int HeaderHeight { get { return (int)Math.Round(30 * DeviceDpi / 96.0); } }

        private int ContentHeight { get { return _rows.Count * RowHeight; } }
        private int ViewHeight { get { return Math.Max(0, Height - HeaderHeight); } }

        private void ClampScroll()
        {
            int max = Math.Max(0, ContentHeight - ViewHeight);
            if (_scroll < 0) _scroll = 0;
            if (_scroll > max) _scroll = max;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            float s = DeviceDpi / 96f;
            int w = Width;
            int headerH = HeaderHeight;
            int rowH = RowHeight;

            int right = w - 12;
            int dateW = (int)(110 * s);
            int pctW = (int)(56 * s);
            int sizeW = (int)(86 * s);
            int dateX = right - dateW;
            int pctX = dateX - pctW - 4;
            int sizeX = pctX - sizeW - 4;
            int nameX = 26;
            int nameW = Math.Max(20, sizeX - nameX - 8);

            // header
            using (var b = new SolidBrush(Color.FromArgb(250, 249, 246)))
                g.FillRectangle(b, 0, 0, w, headerH);
            TextRenderer.DrawText(g, "名称", Theme.Small, new Rectangle(nameX, 0, nameW, headerH), Theme.Faint,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "大小", Theme.Small, new Rectangle(sizeX, 0, sizeW, headerH), Theme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "占比", Theme.Small, new Rectangle(pctX, 0, pctW, headerH), Theme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "修改时间", Theme.Small, new Rectangle(dateX, 0, dateW, headerH), Theme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (var p = new Pen(Theme.Line))
                g.DrawLine(p, 0, headerH - 1, w, headerH - 1);

            ClampScroll();
            var body = new Rectangle(0, headerH, w, Math.Max(0, Height - headerH));
            g.SetClip(body);

            if (_rows.Count == 0)
            {
                TextRenderer.DrawText(g, "暂无数据", Theme.Small, body, Theme.Faint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                g.ResetClip();
                return;
            }

            int first = Math.Max(0, _scroll / rowH);
            int last = Math.Min(_rows.Count - 1, (headerH + body.Height + _scroll) / rowH + 1);

            for (int i = first; i <= last; i++)
            {
                int y = headerH + i * rowH - _scroll;
                var rowRect = new Rectangle(0, y, w, rowH);
                if (rowRect.Bottom < headerH) continue;

                if (i == _selected)
                {
                    using (var b = new SolidBrush(Theme.Selected)) g.FillRectangle(b, rowRect);
                }
                else if (i == _hover)
                {
                    using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, rowRect);
                }

                var row = _rows[i];
                int cy = y + rowH / 2;
                var dot = new Rectangle(8, cy - 3, 7, 7);
                Theme.FillRound(g, dot, 2f, Theme.NodeColor(row.Node));

                TextRenderer.DrawText(g, row.Name, row.IsDir ? Theme.BodyBold : Theme.Body,
                    new Rectangle(nameX, y, nameW, rowH), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(g, Theme.Size(row.Size), Theme.Small,
                    new Rectangle(sizeX, y, sizeW, rowH), Theme.Text,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                TextRenderer.DrawText(g, string.Format("{0:0.#}%", row.Percent), Theme.Small,
                    new Rectangle(pctX, y, pctW, rowH), row.Percent >= 10 ? Theme.Blue : Theme.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                string date = row.IsDir ? "—" : (row.Modified.Year > 1900 ? row.Modified.ToString("yyyy-MM-dd HH:mm") : "—");
                TextRenderer.DrawText(g, date, Theme.Small,
                    new Rectangle(dateX, y, dateW, rowH), Theme.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            g.ResetClip();

            if (ContentHeight > ViewHeight)
            {
                int trackX = w - 5;
                int barH = Math.Max(24, (int)((double)ViewHeight * ViewHeight / ContentHeight));
                int barY = headerH + (int)((double)_scroll / Math.Max(1, ContentHeight - ViewHeight) * (ViewHeight - barH));
                using (var b = new SolidBrush(Color.FromArgb(210, 216, 224)))
                    Theme.FillRound(g, new RectangleF(trackX, barY, 3, barH), 1.5f, Color.FromArgb(210, 216, 224));
            }
        }

        private int RowIndexAt(Point p)
        {
            if (p.Y < HeaderHeight) return -1;
            int idx = (p.Y - HeaderHeight + _scroll) / Math.Max(1, RowHeight);
            return idx >= 0 && idx < _rows.Count ? idx : -1;
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
            _selected = RowIndexAt(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                int idx = RowIndexAt(e.Location);
                if (idx >= 0 && MenuRequested != null) MenuRequested(_rows[idx], PointToScreen(e.Location));
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            int idx = RowIndexAt(PointToClient(Cursor.Position));
            if (idx >= 0 && RowActivated != null) RowActivated(_rows[idx]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scroll -= e.Delta / 120 * Math.Max(1, RowHeight * 3 / 4);
            ClampScroll();
            Invalidate();
        }
    }
}
