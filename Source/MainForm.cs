using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CDiskCleaner
{
    internal sealed class MainForm : Form
    {
        private readonly string _shotFile;
        private readonly string _shotPath;
        private bool _sizeApplied;

        private HeaderBar _header;
        private DriveBar _driveBar;
        private Panel _status;
        private Label _lblStatus;
        private Label _lblStats;
        private FlatButton _btnCancel;

        private CardPanel _leftCard;
        private CardPanel _mapCard;
        private CardPanel _listCard;

        private Label _lblCleanTitle;
        private Label _lblCleanSub;
        private Label _lblTotalCap;
        private Label _lblTotalVal;
        private CleanupList _cleanList;
        private FlatButton _btnClean;
        private FlatButton _btnCleanRescan;

        private Label _lblMapTitle;
        private Breadcrumb _crumbs;
        private FlatButton _btnUp;
        private FlatButton _btnMapRescan;
        private TreemapView _map;

        private Label _lblListTitle;
        private FlatButton _btnTabChildren;
        private FlatButton _btnTabBig;
        private ItemList _list;

        private Node _root;
        private Node _current;
        private bool _bigMode;

        private DiskScanner _scanner;
        private CancellationTokenSource _cts;
        private System.Windows.Forms.Timer _uiTimer;
        private readonly Stopwatch _scanWatch = new Stopwatch();
        private string _scanPath = "";
        private bool _scanning;
        private bool _measuring;
        private string _driveRoot;
        private int _scanSeq;
        private readonly HashSet<string> _addedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long _lastFileCount;
        private long _lastErrorCount;

        public MainForm() : this(null, null) { }

        public MainForm(string shotFile, string shotPath)
        {
            _shotFile = shotFile;
            _shotPath = shotPath;

            Text = "磁盘清理工具";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            MinimumSize = new Size(1080, 700);
            Icon = BuildIcon();

            if (_shotFile != null)
            {
                Opacity = 0;
                ShowInTaskbar = false;
            }

            BuildUi();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _uiTimer.Tick += delegate { UpdateScanProgress(); };
        }

        private float S { get { return DeviceDpi / 96f; } }
        private int Px(float v) { return (int)Math.Round(v * S); }

        private static Label MakeLabel(Control parent, string text, Font font, Color color)
        {
            var lbl = new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = Color.White,
                AutoSize = true,
                UseMnemonic = false
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private static Icon BuildIcon()
        {
            try
            {
                using (var bmp = new Bitmap(64, 64))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, 63, 63), 14f))
                        using (var b = new SolidBrush(Theme.Navy)) g.FillPath(b, path);
                        using (var b = new SolidBrush(Theme.Gold)) g.FillRectangle(b, 14, 16, 13, 32);
                        using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, 31, 16, 19, 14);
                        using (var b = new SolidBrush(Color.FromArgb(74, 123, 184))) g.FillRectangle(b, 31, 34, 19, 14);
                    }
                    IntPtr h = bmp.GetHicon();
                    return Icon.FromHandle(h);
                }
            }
            catch { return null; }
        }

        private void BuildUi()
        {
            _header = new HeaderBar();
            Controls.Add(_header);

            _driveBar = new DriveBar();
            _driveBar.DriveSelected += SwitchDrive;
            Controls.Add(_driveBar);

            _status = new Panel { BackColor = Color.White };
            _status.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (var p = new Pen(Theme.Line)) pe.Graphics.DrawLine(p, 0, 0, _status.Width, 0);
            };
            Controls.Add(_status);

            _lblStatus = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.Small,
                ForeColor = Theme.Dim,
                BackColor = Color.White,
                Text = "准备就绪"
            };
            _status.Controls.Add(_lblStatus);

            _lblStats = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = Theme.Small,
                ForeColor = Theme.Faint,
                BackColor = Color.White,
                Text = ""
            };
            _status.Controls.Add(_lblStats);

            _btnCancel = new FlatButton { Text = "取消扫描", Kind = FlatButton.BtnStyle.Soft, Visible = false };
            _btnCancel.Click += delegate { if (_cts != null) _cts.Cancel(); };
            _status.Controls.Add(_btnCancel);

            _leftCard = new CardPanel();
            _mapCard = new CardPanel();
            _listCard = new CardPanel();
            Controls.Add(_leftCard);
            Controls.Add(_mapCard);
            Controls.Add(_listCard);

            _lblCleanTitle = MakeLabel(_leftCard, "系统垃圾清理", Theme.H2, Theme.Navy);
            _lblCleanSub = MakeLabel(_leftCard, "勾选项目后点击右侧按钮，安全项已默认选中", Theme.Small, Theme.Dim);

            _cleanList = new CleanupList();
            _cleanList.Changed += delegate { UpdateCleanupTotal(); };
            _leftCard.Controls.Add(_cleanList);

            _lblTotalCap = MakeLabel(_leftCard, "预计可释放空间", Theme.Small, Theme.Dim);
            _lblTotalVal = MakeLabel(_leftCard, "—", Theme.Num, Theme.Navy);

            _btnClean = new FlatButton { Text = "立即清理", Kind = FlatButton.BtnStyle.Primary };
            _btnClean.Click += OnCleanClick;
            _leftCard.Controls.Add(_btnClean);

            _btnCleanRescan = new FlatButton { Text = "刷新大小", Kind = FlatButton.BtnStyle.Ghost };
            _btnCleanRescan.Click += delegate { RefreshCleanupSizes(); };
            _leftCard.Controls.Add(_btnCleanRescan);

            _lblMapTitle = MakeLabel(_mapCard, "磁盘空间分析", Theme.H2, Theme.Navy);

            _crumbs = new Breadcrumb();
            _crumbs.Navigate += SetCurrent;
            _mapCard.Controls.Add(_crumbs);

            _btnUp = new FlatButton { Text = "上一级", Kind = FlatButton.BtnStyle.Ghost };
            _btnUp.Click += delegate
            {
                if (_current != null && _current.Parent != null) SetCurrent(_current.Parent);
            };
            _mapCard.Controls.Add(_btnUp);

            _btnMapRescan = new FlatButton { Text = "重新扫描", Kind = FlatButton.BtnStyle.Ghost };
            _btnMapRescan.Click += delegate { if (_current != null) StartScan(_current.FullPath, _current); };
            _mapCard.Controls.Add(_btnMapRescan);

            _map = new TreemapView();
            _map.NodeActivated += Drill;
            _map.MenuRequested += ShowNodeMenu;
            _mapCard.Controls.Add(_map);

            _lblListTitle = MakeLabel(_listCard, "目录明细", Theme.H2, Theme.Navy);

            _btnTabChildren = new FlatButton { Text = "当前目录", Active = true };
            _btnTabChildren.Click += delegate { SetListMode(false); };
            _listCard.Controls.Add(_btnTabChildren);

            _btnTabBig = new FlatButton { Text = "大文件 Top 200" };
            _btnTabBig.Click += delegate { SetListMode(true); };
            _listCard.Controls.Add(_btnTabBig);

            _lblListTitle.AutoSize = false;
            _lblListTitle.AutoEllipsis = true;
            _lblListTitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblListTitle.SendToBack();

            _list = new ItemList();
            _list.RowActivated += delegate(ItemList.Row row)
            {
                if (row.Node == null) return;
                if (row.Node.IsDir) SetCurrent(row.Node);
                else OpenInExplorer(row.Node);
            };
            _list.MenuRequested += delegate(ItemList.Row row, Point pt)
            {
                if (row.Node != null) ShowNodeMenu(row.Node, pt);
            };
            _listCard.Controls.Add(_list);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_sizeApplied)
            {
                _sizeApplied = true;
                ClientSize = new Size(Px(1340), Px(840));
                MinimumSize = new Size(Px(1080), Px(700));
            }
            LayoutUi();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutUi();
        }

        private void LayoutUi()
        {
            if (_header == null) return;
            float s = S;
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pad = (int)Math.Round(18 * s);
            int headerH = (int)Math.Round(112 * s);
            int statusH = (int)Math.Round(34 * s);

            _header.SetBounds(0, 0, w, headerH);
            int driveH = (int)Math.Round(46 * s);
            _driveBar.SetBounds(0, headerH, w, driveH);
            _status.SetBounds(0, h - statusH, w, statusH);

            int cancelW = (int)Math.Round(76 * s);
            int statsW = (int)Math.Round(470 * s);
            _lblStats.SetBounds(w - pad - statsW, 0, statsW, statusH);
            _btnCancel.SetBounds(w - pad - statsW - cancelW - (int)(8 * s), (statusH - (int)(22 * s)) / 2, cancelW, (int)(22 * s));
            _lblStatus.SetBounds(pad, 0, Math.Max(50, w - pad * 3 - statsW - cancelW), statusH);

            int bodyTop = headerH + driveH;
            int bodyH = h - bodyTop - statusH;
            int leftW = (int)Math.Round(400 * s);
            int cardH = bodyH - pad * 2;

            _leftCard.SetBounds(pad, bodyTop + pad, leftW, cardH);

            int rightX = pad * 2 + leftW;
            int rightW = w - rightX - pad;
            int mapH = (int)((cardH - pad) * 0.60f);
            _mapCard.SetBounds(rightX, bodyTop + pad, rightW, mapH);
            _listCard.SetBounds(rightX, bodyTop + pad + mapH + pad, rightW, cardH - mapH - pad);

            // 左侧清理卡片
            int cw = _leftCard.Width;
            int ch = _leftCard.Height;
            _lblCleanTitle.Location = new Point((int)(24 * s), (int)(20 * s));
            _lblCleanSub.Location = new Point((int)(24 * s), (int)(50 * s));
            _cleanList.SetBounds((int)(12 * s), (int)(78 * s), cw - (int)(24 * s), ch - (int)(78 * s) - (int)(124 * s));
            _lblTotalCap.Location = new Point((int)(24 * s), ch - (int)(112 * s));
            _lblTotalVal.Location = new Point((int)(24 * s), ch - (int)(94 * s));
            _btnClean.SetBounds(cw - (int)(24 * s) - (int)(122 * s), ch - (int)(52 * s), (int)(122 * s), (int)(36 * s));
            _btnCleanRescan.SetBounds(cw - (int)(24 * s) - (int)(122 * s) - (int)(10 * s) - (int)(96 * s), ch - (int)(52 * s), (int)(96 * s), (int)(36 * s));

            // 空间分析卡片
            cw = _mapCard.Width;
            ch = _mapCard.Height;
            _lblMapTitle.Location = new Point((int)(22 * s), (int)(16 * s));
            int btnW = (int)(88 * s);
            int btnH = (int)(30 * s);
            _btnMapRescan.SetBounds(cw - (int)(22 * s) - btnW, (int)(42 * s), btnW, btnH);
            _btnUp.SetBounds(cw - (int)(22 * s) - btnW - (int)(8 * s) - (int)(78 * s), (int)(42 * s), (int)(78 * s), btnH);
            _crumbs.SetBounds((int)(22 * s), (int)(44 * s), Math.Max(40, _btnUp.Left - (int)(34 * s) - (int)(22 * s)), (int)(26 * s));
            _map.SetBounds((int)(14 * s), (int)(82 * s), cw - (int)(28 * s), ch - (int)(82 * s) - (int)(14 * s));

            // 明细卡片
            cw = _listCard.Width;
            ch = _listCard.Height;
            int tabChildW = (int)(84 * s);
            int tabBigW = (int)(116 * s);
            int tabGap = (int)(8 * s);
            int tabX2 = cw - (int)(18 * s) - tabBigW;
            int tabX1 = tabX2 - tabGap - tabChildW;
            _lblListTitle.SetBounds((int)(22 * s), (int)(12 * s), tabX1 - (int)(34 * s), (int)(30 * s));
            _btnTabChildren.SetBounds(tabX1, (int)(13 * s), tabChildW, (int)(28 * s));
            _btnTabBig.SetBounds(tabX2, (int)(13 * s), tabBigW, (int)(28 * s));
            _list.SetBounds((int)(14 * s), (int)(52 * s), cw - (int)(28 * s), ch - (int)(52 * s) - (int)(12 * s));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_shotFile != null)
            {
                RunShot();
                return;
            }

            _driveRoot = SystemRoot();
            RefreshDriveBar();
            UpdateDriveInfo();
            RebuildCleanupItems();
            RefreshCleanupSizes();
            StartScan(_driveRoot, null);
        }

        private void RunShot()
        {
            _driveRoot = Path.GetPathRoot(_shotPath);
            if (string.IsNullOrEmpty(_driveRoot)) _driveRoot = SystemRoot();
            RefreshDriveBar();
            UpdateDriveInfo();
            RebuildCleanupItems();
            RefreshCleanupSizes();
            try
            {
                _scanner = new DiskScanner();
                _scanWatch.Restart();
                _root = _scanner.Scan(_shotPath, CancellationToken.None);
                _scanWatch.Stop();
                _lastFileCount = _scanner.FileCount;
                _lastErrorCount = _scanner.ErrorCount;
                SetCurrent(_root);
                UpdateStatusSummary();
                AppendCacheCandidates();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "扫描失败：" + ex.Message;
            }

            var t = new System.Windows.Forms.Timer { Interval = 6000 };
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(_shotFile, ImageFormat.Png);
                    }
                }
                catch { }
                Close();
            };
            t.Start();
        }

        private static bool IsAdmin()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static String SystemRoot()
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root)) root = "C:\\";
            return root;
        }

        private bool IsSystemDrive()
        {
            return string.Equals(_driveRoot, SystemRoot(), StringComparison.OrdinalIgnoreCase);
        }

        private void AppendCacheCandidates()
        {
            if (_root == null || IsSystemDrive()) return;

            var found = new List<Node>();
            var stack = new Stack<Node>();
            stack.Push(_root);
            int guard = 0;

            while (stack.Count > 0)
            {
                if (++guard > 400000) break;
                var node = stack.Pop();
                if (!node.IsDir) continue;

                if (node.Parent != null)
                {
                    if (IsSkipDir(node.Name)) continue;

                    if (IsCacheName(node.Name))
                    {
                        if (node.Size >= 100L * 1024 * 1024 && !_addedCache.Contains(node.FullPath))
                        {
                            found.Add(node);
                            _addedCache.Add(node.FullPath);
                        }
                        continue;
                    }
                }

                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        if (node.Children[i].IsDir) stack.Push(node.Children[i]);
                    }
                }
            }

            if (found.Count == 0) return;
            found.Sort(delegate(Node a, Node b) { return b.Size.CompareTo(a.Size); });

            int added = 0;
            for (int i = 0; i < found.Count && added < 5; i++)
            {
                var node = found[i];
                string rel = RelativeToDrive(node);
                string shortRel = rel;
                string[] parts = rel.Split('\\');
                if (parts.Length > 2) shortRel = parts[parts.Length - 2] + "\\" + parts[parts.Length - 1];

                var item = new CleanupItem
                {
                    Name = node.Name + "  ·  " + shortRel,
                    Desc = "识别到的缓存/临时目录：" + rel,
                    Kind = CleanupKind.DirContents,
                    Caution = true,
                    Checked = false,
                    Preset = true,
                    Size = node.Size
                };
                item.Paths.Add(node.FullPath);
                _cleanList.Items.Add(item);
                added++;
            }

            if (added > 0)
            {
                _cleanList.FooterText = "自动识别到的缓存/临时目录已标注「谨慎」，删除前请确认内容。\n\n更多大文件可用右侧「磁盘空间分析」定位后右键删除。";
                _cleanList.Invalidate();
                UpdateCleanupTotal();
            }
        }

        private string RelativeToDrive(Node node)
        {
            string full = node.FullPath;
            if (full.StartsWith(_driveRoot, StringComparison.OrdinalIgnoreCase))
                return full.Substring(_driveRoot.Length).TrimStart('\\');
            return full;
        }

        private static bool IsCacheName(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "node_modules":
                case ".cache":
                case "cache":
                case "caches":
                case "temp":
                case "tmp":
                case "tempfiles":
                case "downloads":
                case "下载":
                case "缓存":
                case "临时文件":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSkipDir(string name)
        {
            string n = name.ToLowerInvariant();
            return n == "windows" || n == "system volume information" || n == "$recycle.bin"
                || n == "program files" || n == "program files (x86)" || n == "programdata";
        }

        private void UpdateDriveInfo()
        {
            try
            {
                if (string.IsNullOrEmpty(_driveRoot)) _driveRoot = SystemRoot();
                var drive = new DriveInfo(_driveRoot);
                _header.DriveLabel = _driveRoot.Substring(0, 2);
                _header.Total = drive.TotalSize;
                _header.Free = drive.TotalFreeSpace;
                _header.Used = drive.TotalSize - drive.TotalFreeSpace;
                _header.IsAdmin = IsAdmin();
                _header.Invalidate();
            }
            catch { }
        }

        private void RefreshDriveBar()
        {
            var list = new List<DriveBarInfo>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                        list.Add(new DriveBarInfo
                        {
                            Root = d.RootDirectory.FullName,
                            Free = d.TotalFreeSpace,
                            Total = d.TotalSize
                        });
                    }
                    catch { }
                }
            }
            catch { }
            list.Sort(delegate(DriveBarInfo a, DriveBarInfo b)
            {
                return string.Compare(a.Root, b.Root, StringComparison.OrdinalIgnoreCase);
            });
            if (string.IsNullOrEmpty(_driveRoot) && list.Count > 0) _driveRoot = list[0].Root;
            _driveBar.SetDrives(list, _driveRoot);
        }

        private void RebuildCleanupItems()
        {
            bool isSystem = IsSystemDrive();
            _cleanList.SetItems(Cleaner.CreateItems(_driveRoot, isSystem));
            _lblCleanTitle.Text = isSystem ? "系统垃圾清理" : "磁盘垃圾清理";
            _lblCleanSub.Text = isSystem
                ? "勾选项目后点击右侧按钮，安全项已默认选中"
                : "该盘主要可清理回收站与临时文件，其余建议用空间分析定位";
            _cleanList.FooterText = isSystem
                ? null
                : "非系统盘没有通用的系统缓存，可释放空间主要来自回收站。\n\n其他大文件建议使用右侧「磁盘空间分析」定位后右键删除。";
            _cleanList.Invalidate();
            UpdateCleanupTotal();
        }

        private void SwitchDrive(string root)
        {
            if (string.IsNullOrEmpty(root)) return;
            if (string.Equals(root, _driveRoot, StringComparison.OrdinalIgnoreCase)) return;

            _driveRoot = root;
            _addedCache.Clear();
            RefreshDriveBar();
            UpdateDriveInfo();
            RebuildCleanupItems();
            RefreshCleanupSizes();

            _root = null;
            _current = null;
            _map.SetRoot(null);
            _crumbs.SetCurrent(null);
            _list.SetRows(new List<ItemList.Row>());
            _lblListTitle.Text = "目录明细";
            StartScan(root, null);
        }

        private void StartScan(string path, Node target)
        {
            if (_scanning && _cts != null) _cts.Cancel();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var scanner = new DiskScanner();
            _scanner = scanner;
            _scanPath = path;
            _scanning = true;
            _scanSeq++;
            int seq = _scanSeq;
            _scanWatch.Restart();
            SetScanningUi(true);
            _uiTimer.Start();
            _lblStatus.Text = "正在扫描 " + path + " …";

            var form = this;
            Task<Node> task = Task.Run(delegate { return scanner.Scan(path, token); }, token);
            task.ContinueWith(delegate(Task<Node> t)
            {
                if (!form.IsDisposed) form.BeginInvoke((Action)delegate { OnScanFinished(t, target, seq); });
            });
        }

        private void OnScanFinished(Task<Node> task, Node target, int seq)
        {
            if (seq != _scanSeq) return;
            _uiTimer.Stop();
            _scanWatch.Stop();
            _scanning = false;
            var scanner = _scanner;
            if (scanner != null)
            {
                _lastFileCount = scanner.FileCount;
                _lastErrorCount = scanner.ErrorCount;
            }
            _scanner = null;
            SetScanningUi(false);

            if (task.IsCanceled)
            {
                _lblStatus.Text = "扫描已取消";
                return;
            }
            if (task.IsFaulted)
            {
                Exception ex = task.Exception != null && task.Exception.InnerException != null
                    ? task.Exception.InnerException : task.Exception;
                _lblStatus.Text = "扫描失败";
                MessageBox.Show(this, "扫描失败：" + (ex != null ? ex.Message : "未知错误"), "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Node result = task.Result;
            if (result == null) return;

            if (_root == null || target == null)
            {
                _root = result;
                SetCurrent(result);
            }
            else
            {
                long oldSize = target.Size;
                target.Adopt(result);
                long delta = target.Size - oldSize;
                for (var a = target.Parent; a != null; a = a.Parent) a.Size += delta;
                SetCurrent(target);
            }

            UpdateDriveInfo();
            UpdateStatusSummary();
            AppendCacheCandidates();
        }

        private void UpdateScanProgress()
        {
            var scanner = _scanner;
            if (scanner == null) return;
            _lblStatus.Text = string.Format("正在扫描 {0} …  已索引 {1:N0} 个文件", _scanPath, scanner.FileCount);
            _lblStats.Text = string.Format("文件夹 {0:N0} · 跳过 {1:N0}", scanner.DirCount, scanner.ErrorCount);
        }

        private void SetScanningUi(bool on)
        {
            _btnMapRescan.Enabled = !on;
            _btnClean.Enabled = !on;
            _btnCleanRescan.Enabled = !on;
            _btnCancel.Visible = on;
            _btnUp.Enabled = !on && _current != null && _current.Parent != null;
        }

        private void UpdateStatusSummary()
        {
            _lblStatus.Text = string.Format("扫描完成 · {0:N0} 个文件 · 用时 {1:0.0} 秒", _lastFileCount, _scanWatch.Elapsed.TotalSeconds);
            if (_current != null)
                _lblStats.Text = string.Format("无权限或已跳过 {0:N0} 项 · 当前目录 {1}", _lastErrorCount, Theme.Size(_current.Size));
        }

        private void SetCurrent(Node node)
        {
            if (node == null) return;
            _current = node;
            _crumbs.SetCurrent(node);
            _map.SetRoot(node);
            _btnUp.Enabled = node.Parent != null && !_scanning;
            RebuildList();
        }

        private void SetListMode(bool big)
        {
            _bigMode = big;
            _btnTabChildren.Active = !big;
            _btnTabBig.Active = big;
            _btnTabChildren.Invalidate();
            _btnTabBig.Invalidate();
            RebuildList();
        }

        private void RebuildList()
        {
            var node = _current;
            if (node == null)
            {
                _list.SetRows(new List<ItemList.Row>());
                return;
            }

            if (!_bigMode)
            {
                var rows = new List<ItemList.Row>();
                int count = node.Children != null ? node.Children.Count : 0;
                _lblListTitle.Text = string.Format("目录明细   ·   {0:N0} 项", count);
                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        var c = node.Children[i];
                        rows.Add(new ItemList.Row
                        {
                            Node = c,
                            Name = c.Name,
                            Size = c.Size,
                            Percent = node.Size > 0 ? c.Size * 100.0 / node.Size : 0,
                            Modified = c.Modified,
                            IsDir = c.IsDir
                        });
                        if (rows.Count >= 5000) break;
                    }
                }
                _list.SetRows(rows);
                return;
            }

            _lblListTitle.Text = "目录明细   ·   大文件 Top 200";
            var target = node;
            Task.Run(delegate
            {
                var files = CollectBigFiles(target, 200);
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (_current != target) return;
                        var rows = new List<ItemList.Row>();
                        foreach (var f in files)
                        {
                            rows.Add(new ItemList.Row
                            {
                                Node = f,
                                Name = f.Name,
                                Size = f.Size,
                                Percent = target.Size > 0 ? f.Size * 100.0 / target.Size : 0,
                                Modified = f.Modified,
                                IsDir = false
                            });
                        }
                        _list.SetRows(rows);
                    });
                }
                catch { }
            });
        }

        private static List<Node> CollectBigFiles(Node root, int top)
        {
            var all = new List<Node>();
            var stack = new Stack<Node>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n.IsDir)
                {
                    if (n.Children != null)
                    {
                        for (int i = 0; i < n.Children.Count; i++) stack.Push(n.Children[i]);
                    }
                }
                else if (n.Size >= 1024 * 1024)
                {
                    all.Add(n);
                }
            }
            all.Sort(delegate(Node a, Node b) { return b.Size.CompareTo(a.Size); });
            if (all.Count > top) all.RemoveRange(top, all.Count - top);
            return all;
        }

        private void Drill(Node node)
        {
            if (node == null || _scanning) return;
            if (!node.IsDir)
            {
                OpenInExplorer(node);
                return;
            }
            if (node.Children == null && node.Size > 0) StartScan(node.FullPath, node);
            else SetCurrent(node);
        }

        private void OpenInExplorer(Node node)
        {
            try
            {
                string args = node.IsDir
                    ? "\"" + node.FullPath + "\""
                    : "/select,\"" + node.FullPath + "\"";
                Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
            }
            catch { }
        }

        private void ShowNodeMenu(Node node, Point screen)
        {
            if (node == null || node.Parent == null) return;
            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = Theme.Body;
            menu.Items.Add("打开所在位置", null, delegate { OpenInExplorer(node); });
            menu.Items.Add("复制完整路径", null, delegate
            {
                try { Clipboard.SetText(node.FullPath); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除到回收站", null, delegate { DeleteNode(node, false); });
            menu.Items.Add("永久删除", null, delegate { DeleteNode(node, true); });
            menu.Show(screen);
        }

        private void DeleteNode(Node node, bool permanent)
        {
            if (node == null || node.Parent == null)
            {
                MessageBox.Show(this, "该位置不支持删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string msg = permanent
                ? string.Format("确定永久删除“{0}”吗？\n\n大小：{1}\n\n此操作不可恢复。", node.Name, Theme.Size(node.Size))
                : string.Format("确定把“{0}”移动到回收站吗？\n\n大小：{1}", node.Name, Theme.Size(node.Size));
            DialogResult r = MessageBox.Show(this, msg, permanent ? "永久删除" : "删除确认",
                MessageBoxButtons.OKCancel, permanent ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
            if (r != DialogResult.OK) return;

            try
            {
                if (node.IsDir)
                {
                    if (permanent) Directory.Delete(node.FullPath, true);
                    else Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(node.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else
                {
                    if (permanent) File.Delete(node.FullPath);
                    else Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(node.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "删除失败：" + ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RemoveNodeFromTree(node);
            SetCurrent(_current);
            UpdateDriveInfo();
            RefreshDriveBar();
            _lblStatus.Text = "已删除：" + node.Name;
        }

        private void RemoveNodeFromTree(Node node)
        {
            var parent = node.Parent;
            if (parent == null) return;
            long delta = node.Size;
            if (parent.Children != null) parent.Children.Remove(node);
            for (var a = parent; a != null; a = a.Parent)
            {
                a.Size -= delta;
                if (a.Size < 0) a.Size = 0;
            }
            node.Parent = null;
        }

        private void RefreshCleanupSizes()
        {
            var items = _cleanList.Items;
            _measuring = true;
            _lblTotalVal.Text = "计算中…";
            Task.Run(delegate
            {
                try
                {
                    Parallel.ForEach(items, delegate(CleanupItem item)
                    {
                        Cleaner.Measure(item);
                        try
                        {
                            BeginInvoke((Action)delegate
                            {
                                _cleanList.Invalidate();
                                UpdateCleanupTotal();
                            });
                        }
                        catch { }
                    });
                }
                catch { }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        _measuring = false;
                        _cleanList.Invalidate();
                        UpdateCleanupTotal();
                    });
                }
                catch { }
            });
        }

        private void UpdateCleanupTotal()
        {
            long sum = 0;
            foreach (var item in _cleanList.Items)
            {
                if (item.Checked) sum += item.Size;
            }
            if (sum > 0) _lblTotalVal.Text = Theme.Size(sum);
            else if (_measuring) _lblTotalVal.Text = "计算中…";
            else _lblTotalVal.Text = "—";
        }

        private async void OnCleanClick(object sender, EventArgs e)
        {
            var chosen = new List<CleanupItem>();
            foreach (var item in _cleanList.Items)
            {
                if (item.Checked && item.Size > 0) chosen.Add(item);
            }
            if (chosen.Count == 0)
            {
                MessageBox.Show(this, "没有可清理的项目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long total = 0;
            var sb = new StringBuilder();
            foreach (var item in chosen)
            {
                total += item.Size;
                if (item.Preset && item.Paths.Count > 0)
                    sb.AppendLine("· " + item.Name + "\n      " + item.Paths[0] + "   " + Theme.Size(item.Size));
                else
                    sb.AppendLine("· " + item.Name + "   " + Theme.Size(item.Size));
            }
            string msg = "即将清理以下内容：\n\n" + sb + "\n预计释放 " + Theme.Size(total) + "。\n\n清理后不可恢复，确认继续？";
            if (MessageBox.Show(this, msg, "确认清理", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            SetBusy(true, "正在清理，请稍候…");

            CleanResult result = null;
            try
            {
                result = await Task.Run(delegate
                {
                    return Cleaner.Clean(chosen, delegate(string text)
                    {
                        try { BeginInvoke((Action)delegate { _lblStatus.Text = text; }); } catch { }
                    });
                });
            }
            catch (Exception ex)
            {
                SetBusy(false, "清理失败");
                MessageBox.Show(this, "清理失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetBusy(false, "清理完成");
            foreach (var it in _cleanList.Items)
            {
                if (it.Preset) { it.Preset = false; it.Size = 0; }
            }
            UpdateDriveInfo();
            RefreshDriveBar();
            RefreshCleanupSizes();

            string done = "清理完成，共释放 " + Theme.Size(result.Freed) + "。";
            if (result.Errors > 0) done += "\n\n有 " + result.Errors + " 个文件正被占用或拒绝访问，已跳过。";
            MessageBox.Show(this, done, "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _lblStatus.Text = "清理完成，释放 " + Theme.Size(result.Freed);
        }

        private void SetBusy(bool busy, string text)
        {
            _btnClean.Enabled = !busy;
            _btnCleanRescan.Enabled = !busy;
            _btnMapRescan.Enabled = !busy;
            _cleanList.Enabled = !busy;
            _lblStatus.Text = text;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
            }
            base.OnFormClosing(e);
        }
    }
}
