using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class Breadcrumb : Control
    {
        private sealed class Segment
        {
            public string Text;
            public Node Node;
            public Rectangle Bounds;
            public bool IsLast;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private int _hover = -1;

        public event Action<Node> Navigate;

        public Breadcrumb()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        public void SetCurrent(Node node)
        {
            _segments.Clear();
            _hover = -1;
            if (node != null)
            {
                var chain = new List<Node>();
                for (var n = node; n != null; n = n.Parent) chain.Add(n);
                chain.Reverse();

                int x = 0;
                for (int i = 0; i < chain.Count; i++)
                {
                    string text = i == 0 ? chain[i].Name.TrimEnd('\\') : chain[i].Name;
                    if (string.IsNullOrEmpty(text)) text = "C:";
                    int w = TextRenderer.MeasureText(text, i == chain.Count - 1 ? Theme.SmallBold : Theme.Small).Width + 6;
                    _segments.Add(new Segment
                    {
                        Text = text,
                        Node = chain[i],
                        Bounds = new Rectangle(x, 0, w, Height),
                        IsLast = i == chain.Count - 1
                    });
                    x += w + 18;
                }

                int total = x;
                int start = 0;
                while (total > Width && start < _segments.Count - 2)
                {
                    total -= _segments[start].Bounds.Width + 18;
                    start++;
                }
                if (start > 0)
                {
                    int shift = _segments[start].Bounds.X - 18;
                    for (int i = start; i < _segments.Count; i++)
                    {
                        var b = _segments[i].Bounds;
                        _segments[i].Bounds = new Rectangle(b.X - shift, b.Y, b.Width, b.Height);
                    }
                    // remove leading hidden segments
                    _segments.RemoveRange(0, start);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var r = seg.Bounds;
                r.Offset(0, (Height - 20) / 2);
                r.Height = 20;

                if (i > 0)
                {
                    TextRenderer.DrawText(g, "›", Theme.Small,
                        new Rectangle(r.X - 14, r.Y, 12, r.Height), Theme.Faint,
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                }

                Color color = seg.IsLast ? Theme.Text : (i == _hover ? Theme.Blue : Theme.Dim);
                TextRenderer.DrawText(g, seg.Text, seg.IsLast ? Theme.SmallBold : Theme.Small, r, color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                if (i == _hover)
                {
                    using (var p = new Pen(Theme.Blue))
                        g.DrawLine(p, r.X, r.Bottom - 2, r.Right, r.Bottom - 2);
                }
            }
        }

        private int IndexAt(Point p)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var r = _segments[i].Bounds;
                r.Offset(0, (Height - 20) / 2);
                r.Height = 20;
                r.Inflate(4, 2);
                if (r.Contains(p)) return i;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int idx = IndexAt(e.Location);
            if (idx >= 0 && Navigate != null) Navigate(_segments[idx].Node);
        }
    }
}
