using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class HeaderBar : Control
    {
        public long Used;
        public long Total;
        public long Free;
        public bool IsAdmin;
        public string DriveLabel = "C:";

        public HeaderBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Navy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new LinearGradientBrush(ClientRectangle, Theme.Navy, Theme.Navy2, LinearGradientMode.Horizontal))
                g.FillRectangle(brush, ClientRectangle);

            float s = DeviceDpi / 96f;
            int pad = (int)Math.Round(30 * s);

            using (var b = new SolidBrush(Theme.Gold))
                g.FillRectangle(b, pad, (int)(44 * s), (int)(4 * s), (int)(24 * s));

            TextRenderer.DrawText(g, "磁盘清理工具", Theme.Title,
                new Rectangle(pad + (int)(16 * s), (int)(39 * s), (int)(420 * s), (int)(34 * s)),
                Color.White, TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int rightW = (int)(360 * s);
            int rx = Width - pad - rightW;

            string usage = string.Format("{0} 已用 {1} / {2}", DriveLabel, Theme.Size(Used), Theme.Size(Total));
            TextRenderer.DrawText(g, usage, Theme.BodyBold,
                new Rectangle(rx, (int)(26 * s), rightW, (int)(22 * s)),
                Color.White, TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            int barY = (int)(55 * s);
            int barH = (int)(8 * s);
            var track = new RectangleF(rx, barY, rightW, barH);
            Theme.FillRound(g, track, barH / 2f, Color.FromArgb(58, 255, 255, 255));

            double ratio = Total > 0 ? (double)Used / Total : 0;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            float fillW = Math.Max(barH, (float)(rightW * ratio));
            var fillRect = new RectangleF(rx, barY, fillW, barH);
            using (var brush = new LinearGradientBrush(fillRect, Theme.Gold, Theme.GoldSoft, LinearGradientMode.Horizontal))
            using (var path = Theme.Round(fillRect, barH / 2f))
                g.FillPath(brush, path);

            string free = string.Format("可用 {0}", Theme.Size(Free));
            TextRenderer.DrawText(g, free, Theme.Small,
                new Rectangle(rx, (int)(68 * s), rightW, (int)(18 * s)),
                Theme.GoldSoft, TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            if (IsAdmin)
            {
                string badge = "管理员模式";
                var bf = Theme.Tiny;
                int bw = TextRenderer.MeasureText(badge, bf).Width + (int)(18 * s);
                int bh = (int)(19 * s);
                var br = new Rectangle(pad + (int)(236 * s), (int)(46 * s), bw, bh);
                using (var path = Theme.Round(br, bh / 2f))
                {
                    using (var pen = new Pen(Color.FromArgb(120, 201, 162, 39), 1f)) g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, badge, bf, br, Theme.GoldSoft,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            using (var pen = new Pen(Color.FromArgb(46, 255, 255, 255)))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
