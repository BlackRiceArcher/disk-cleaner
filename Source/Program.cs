using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--scan")
            {
                string outFile = args.Length >= 3 ? args[2] : Path.Combine(Path.GetTempPath(), "cdc_scan.txt");
                RunScanTest(args[1], outFile);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && args[0] == "--shot")
            {
                string scanPath = args.Length >= 3 ? args[2] : "C:\\Users";
                Application.Run(new MainForm(args[1], scanPath));
                return;
            }

            Application.Run(new MainForm());
        }

        private static void RunScanTest(string path, string outFile)
        {
            var sw = Stopwatch.StartNew();
            var scanner = new DiskScanner();
            Node root;
            try
            {
                root = scanner.Scan(path, CancellationToken.None);
            }
            catch (Exception ex)
            {
                File.WriteAllText(outFile, "ERROR " + ex, Encoding.UTF8);
                return;
            }
            sw.Stop();

            var sb = new StringBuilder();
            sb.AppendLine("path=" + path);
            sb.AppendLine("fullpath=" + root.FullPath);
            sb.AppendLine(string.Format("size={0} ({1})", root.Size, Theme.Size(root.Size)));
            sb.AppendLine(string.Format("files={0} dirs={1} errors={2} ms={3}", scanner.FileCount, scanner.DirCount, scanner.ErrorCount, sw.ElapsedMilliseconds));
            sb.AppendLine("--- top children ---");
            int n = 0;
            if (root.Children != null)
            {
                foreach (var c in root.Children)
                {
                    sb.AppendLine(string.Format("{0,-44} {1,12} {2}", c.Name, Theme.Size(c.Size), c.IsDir ? "[dir]" : ""));
                    if (++n >= 12) break;
                }
            }

            var values = new List<double>();
            if (root.Children != null)
            {
                foreach (var c in root.Children) values.Add(c.Size);
            }
            var rects = TreemapLayout.Compute(values, new RectangleF(0, 0, 1200, 700));
            double sum = 0;
            foreach (var r in rects) sum += (double)r.Width * r.Height;
            sb.AppendLine(string.Format("treemap items={0} area={1:0} / {2} ({3:0.0}%)", values.Count, sum, 1200 * 700, sum * 100.0 / (1200 * 700)));
            if (rects.Length > 0) sb.AppendLine("first rect=" + rects[0]);

            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        }
    }
}
