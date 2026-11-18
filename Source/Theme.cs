using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(245, 243, 238);
        public static readonly Color Card = Color.White;
        public static readonly Color Navy = Color.FromArgb(15, 27, 45);
        public static readonly Color Navy2 = Color.FromArgb(31, 52, 80);
        public static readonly Color Blue = Color.FromArgb(40, 86, 154);
        public static readonly Color BlueHover = Color.FromArgb(53, 105, 182);
        public static readonly Color Gold = Color.FromArgb(198, 160, 48);
        public static readonly Color GoldSoft = Color.FromArgb(219, 187, 96);
        public static readonly Color Text = Color.FromArgb(25, 36, 51);
        public static readonly Color Dim = Color.FromArgb(112, 122, 136);
        public static readonly Color Faint = Color.FromArgb(154, 162, 174);
        public static readonly Color Border = Color.FromArgb(226, 221, 210);
        public static readonly Color Line = Color.FromArgb(240, 237, 231);
        public static readonly Color Safe = Color.FromArgb(34, 112, 80);
        public static readonly Color SafeSoft = Color.FromArgb(236, 245, 240);
        public static readonly Color Warn = Color.FromArgb(170, 116, 22);
        public static readonly Color WarnSoft = Color.FromArgb(251, 245, 232);
        public static readonly Color Hover = Color.FromArgb(246, 248, 252);
        public static readonly Color Selected = Color.FromArgb(232, 239, 249);

        public static readonly Font Title = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        public static readonly Font H2 = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
        public static readonly Font H3 = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Bold);
        public static readonly Font Body = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular);
        public static readonly Font BodyBold = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold);
        public static readonly Font Small = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Regular);
        public static readonly Font SmallBold = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Bold);
        public static readonly Font Tiny = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Regular);
        public static readonly Font Num = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
        public static readonly Font HeaderSub = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);

        private static readonly Color[] DirPalette =
        {
            Color.FromArgb(31, 53, 82),
            Color.FromArgb(43, 70, 104),
            Color.FromArgb(54, 86, 122),
            Color.FromArgb(64, 78, 98),
            Color.FromArgb(74, 90, 112),
            Color.FromArgb(92, 100, 116),
            Color.FromArgb(110, 96, 68),
            Color.FromArgb(118, 110, 98)
        };

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        public static string Size(long bytes)
        {
            if (bytes <= 0) return "0 B";
            double b = bytes;
            if (b >= 1024d * 1024d * 1024d) return (b / (1024d * 1024d * 1024d)).ToString("0.##") + " GB";
            if (b >= 1024d * 1024d) return (b / (1024d * 1024d)).ToString("0.#") + " MB";
            if (b >= 1024d) return (b / 1024d).ToString("0.#") + " KB";
            return ((long)b).ToString() + " B";
        }

        public static Color Lighten(Color c, int delta)
        {
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, c.R + delta)),
                Math.Max(0, Math.Min(255, c.G + delta)),
                Math.Max(0, Math.Min(255, c.B + delta)));
        }

        public static Color NodeColor(Node n)
        {
            Color baseColor;
            string name = n.Name ?? "";
            int seed = 0;
            for (int i = 0; i < name.Length; i++) seed = (seed * 31 + name[i]) & 0x7fffffff;

            if (n.IsDir)
            {
                baseColor = DirPalette[seed % DirPalette.Length];
            }
            else
            {
                string e = n.Extension;
                switch (e)
                {
                    case "mp4": case "mkv": case "avi": case "mov": case "wmv": case "flv": case "ts": case "m4v":
                        baseColor = Color.FromArgb(165, 116, 62); break;
                    case "jpg": case "jpeg": case "png": case "gif": case "bmp": case "psd": case "webp": case "tif": case "raw": case "cr2":
                        baseColor = Color.FromArgb(58, 105, 168); break;
                    case "mp3": case "wav": case "flac": case "aac": case "m4a": case "wma":
                        baseColor = Color.FromArgb(110, 96, 152); break;
                    case "doc": case "docx": case "xls": case "xlsx": case "ppt": case "pptx": case "pdf": case "txt": case "md":
                    case "ttf": case "otf": case "woff": case "woff2":
                        baseColor = Color.FromArgb(46, 120, 92); break;
                    case "zip": case "rar": case "7z": case "iso": case "gz": case "tar": case "cab":
                        baseColor = Color.FromArgb(146, 128, 62); break;
                    case "exe": case "dll": case "msi": case "sys": case "mui":
                        baseColor = Color.FromArgb(94, 108, 126); break;
                    case "js": case "json": case "cs": case "py": case "java": case "xml": case "html": case "css": case "tsx":
                        baseColor = Color.FromArgb(68, 116, 112); break;
                    default:
                        baseColor = Color.FromArgb(128, 138, 152); break;
                }
            }
            int delta = (seed % 13) - 6;
            return Lighten(baseColor, delta);
        }
    }

    internal class CardPanel : Panel
    {
        public int Radius = 12;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            float s = DeviceDpi / 96f;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 2.5f);
            using (var path = Theme.Round(r, Radius * s))
            {
                using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border, 1f)) g.DrawPath(p, path);
            }
        }
    }

    internal sealed class FlatButton : Control
    {
        public enum BtnStyle { Primary, Ghost, Danger, Soft }

        public BtnStyle Kind = BtnStyle.Ghost;
        public bool Active;

        private bool _hover;
        private bool _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            Font = Theme.BodyBold;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            Color fill, text, border;

            switch (Kind)
            {
                case BtnStyle.Primary:
                    fill = !Enabled ? Color.FromArgb(198, 203, 212) : (_hover ? Theme.BlueHover : Theme.Navy);
                    text = Color.White; border = fill; break;
                case BtnStyle.Danger:
                    fill = _hover ? Color.FromArgb(168, 58, 52) : Color.FromArgb(145, 48, 43);
                    text = Color.White; border = fill; break;
                case BtnStyle.Soft:
                    fill = _hover ? Theme.Selected : Theme.Hover;
                    text = Theme.Blue; border = Theme.Border; break;
                default:
                    if (Active) { fill = Theme.Navy; text = Color.White; border = Theme.Navy; }
                    else { fill = _hover ? Theme.Hover : Color.White; text = Enabled ? Theme.Text : Theme.Faint; border = Theme.Border; }
                    break;
            }
            if (_down && Enabled) fill = Theme.Lighten(fill, -14);

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, 5f * s))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if (!Enabled)
            {
                using (var b = new SolidBrush(Color.FromArgb(170, Color.White))) g.FillRectangle(b, ClientRectangle);
            }
        }
    }
}
