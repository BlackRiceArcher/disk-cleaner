using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CDiskCleaner
{
    internal enum CleanupKind { DirContents, Files, RecycleBin, Hibernate }

    internal sealed class CleanupItem
    {
        public string Name;
        public string Desc;
        public bool Caution;
        public bool Checked;
        public CleanupKind Kind;
        public readonly List<string> Paths = new List<string>();
        public readonly List<string> Files = new List<string>();
        public string[] Patterns;
        public long Size;
        public long Items;
        public bool Preset;
    }

    internal sealed class CleanResult
    {
        public long Freed;
        public int Errors;
    }

    internal static class Cleaner
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x1;
        private const uint SHERB_NOPROGRESSUI = 0x2;
        private const uint SHERB_NOSOUND = 0x4;

        public static List<CleanupItem> CreateItems(string driveRoot, bool isSystemDrive)
        {
            var list = new List<CleanupItem>();
            string temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(temp)) temp = Path.GetTempPath();
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var a = new CleanupItem
            {
                Name = "用户临时文件",
                Desc = "应用运行时生成的临时数据",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            a.Paths.Add(temp);
            if (isSystemDrive) list.Add(a);

            var b = new CleanupItem
            {
                Name = "系统临时文件",
                Desc = "Windows 系统临时目录",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            b.Paths.Add(@"C:\Windows\Temp");
            if (isSystemDrive) list.Add(b);

            var c = new CleanupItem
            {
                Name = "Windows 更新缓存",
                Desc = "已下载的更新包，清理后按需重新下载",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            c.Paths.Add(@"C:\Windows\SoftwareDistribution\Download");
            if (isSystemDrive) list.Add(c);

            var d = new CleanupItem
            {
                Name = "系统日志与转储",
                Desc = "系统日志、崩溃转储与内存转储文件",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            d.Paths.Add(@"C:\Windows\Logs");
            d.Paths.Add(@"C:\Windows\Minidump");
            d.Paths.Add(Path.Combine(localApp, "CrashDumps"));
            d.Files.Add(@"C:\Windows\MEMORY.DMP");
            if (isSystemDrive) list.Add(d);

            var e = new CleanupItem
            {
                Name = "错误报告缓存",
                Desc = "Windows 错误报告存档",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            e.Paths.Add(@"C:\ProgramData\Microsoft\Windows\WER");
            if (isSystemDrive) list.Add(e);

            var f = new CleanupItem
            {
                Name = "更新分发缓存",
                Desc = "交付优化（Delivery Optimization）缓存",
                Kind = CleanupKind.DirContents,
                Checked = true
            };
            f.Paths.Add(@"C:\Windows\SoftwareDistribution\DeliveryOptimization");
            f.Paths.Add(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization");
            if (isSystemDrive) list.Add(f);

            var g = new CleanupItem
            {
                Name = "缩略图缓存",
                Desc = "资源管理器缩略图数据库，会自动重建",
                Kind = CleanupKind.Files,
                Checked = true,
                Patterns = new[] { "thumbcache_*.db", "iconcache_*.db" }
            };
            g.Paths.Add(Path.Combine(localApp, @"Microsoft\Windows\Explorer"));
            if (isSystemDrive) list.Add(g);

            var h = new CleanupItem
            {
                Name = "回收站",
                Desc = "清空 " + driveRoot.Substring(0, 2) + " 盘回收站，其中的文件将永久删除",
                Kind = CleanupKind.RecycleBin,
                Checked = false
            };
            h.Paths.Add(driveRoot);
            list.Add(h);

            var i = new CleanupItem
            {
                Name = "浏览器缓存",
                Desc = "Edge / Chrome 网页缓存，建议先关闭浏览器",
                Kind = CleanupKind.DirContents,
                Caution = true,
                Checked = false
            };
            AddBrowserCache(i, Path.Combine(localApp, @"Microsoft\Edge\User Data"));
            AddBrowserCache(i, Path.Combine(localApp, @"Google\Chrome\User Data"));
            if (isSystemDrive && i.Paths.Count > 0) list.Add(i);

            if (isSystemDrive && Directory.Exists(@"C:\Windows.old"))
            {
                var j = new CleanupItem
                {
                    Name = "旧系统备份",
                    Desc = "C:\\Windows.old 旧版本系统文件，删除后无法回滚",
                    Kind = CleanupKind.DirContents,
                    Caution = true,
                    Checked = false
                };
                j.Paths.Add(@"C:\Windows.old");
                list.Add(j);
            }

            if (isSystemDrive && File.Exists(@"C:\hiberfil.sys"))
            {
                var k = new CleanupItem
                {
                    Name = "休眠文件",
                    Desc = "关闭休眠功能以释放 hiberfil.sys 占用",
                    Kind = CleanupKind.Hibernate,
                    Caution = true,
                    Checked = false
                };
                list.Add(k);
            }

            if (!isSystemDrive)
            {
                string driveTemp = Path.Combine(driveRoot, "Temp");
                if (Directory.Exists(driveTemp))
                {
                    var t = new CleanupItem
                    {
                        Name = "该盘临时文件夹",
                        Desc = driveRoot.TrimEnd('\\') + "\\Temp 下的临时数据",
                        Kind = CleanupKind.DirContents,
                        Checked = true
                    };
                    t.Paths.Add(driveTemp);
                    list.Add(t);
                }
                string oldDir = Path.Combine(driveRoot, "Windows.old");
                if (Directory.Exists(oldDir))
                {
                    var o = new CleanupItem
                    {
                        Name = "旧系统备份",
                        Desc = driveRoot.TrimEnd('\\') + "\\Windows.old 旧系统文件，删除后无法回滚",
                        Kind = CleanupKind.DirContents,
                        Caution = true,
                        Checked = false
                    };
                    o.Paths.Add(oldDir);
                    list.Add(o);
                }
            }

            return list;
        }

        private static void AddBrowserCache(CleanupItem item, string userData)
        {
            try
            {
                if (!Directory.Exists(userData)) return;
                item.Paths.Add(Path.Combine(userData, @"Default\Cache"));
                item.Paths.Add(Path.Combine(userData, @"Default\Code Cache"));
                item.Paths.Add(Path.Combine(userData, @"Default\GPUCache"));
                item.Paths.Add(Path.Combine(userData, @"Default\Service Worker\CacheStorage"));
                foreach (var dir in Directory.EnumerateDirectories(userData))
                {
                    string name = Path.GetFileName(dir);
                    if (name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Paths.Add(Path.Combine(dir, "Cache"));
                        item.Paths.Add(Path.Combine(dir, @"Code Cache"));
                    }
                }
            }
            catch { }
        }

        public static void Measure(CleanupItem item)
        {
            if (item == null || item.Preset) return;
            long size = 0;
            long count = 0;
            try
            {
                switch (item.Kind)
                {
                    case CleanupKind.DirContents:
                        foreach (var p in item.Paths)
                        {
                            if (!Directory.Exists(p)) continue;
                            var scanner = new DiskScanner();
                            var node = scanner.Scan(p, CancellationToken.None);
                            size += node.Size;
                            count += scanner.FileCount;
                        }
                        foreach (var file in item.Files)
                        {
                            try
                            {
                                var fi = new FileInfo(file);
                                if (fi.Exists)
                                {
                                    size += fi.Length;
                                    count++;
                                }
                            }
                            catch { }
                        }
                        break;
                    case CleanupKind.Files:
                        foreach (var p in item.Paths)
                        {
                            if (!Directory.Exists(p)) continue;
                            foreach (var pattern in item.Patterns)
                            {
                                try
                                {
                                    foreach (var file in Directory.EnumerateFiles(p, pattern, SearchOption.TopDirectoryOnly))
                                    {
                                        try
                                        {
                                            size += new FileInfo(file).Length;
                                            count++;
                                        }
                                        catch { }
                                    }
                                }
                                catch { }
                            }
                        }
                        break;
                    case CleanupKind.RecycleBin:
                        SHQUERYRBINFO info = new SHQUERYRBINFO();
                        info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                        if (SHQueryRecycleBin(RecycleRoot(item), ref info) == 0)
                        {
                            size = info.i64Size;
                            count = info.i64NumItems;
                        }
                        break;
                    case CleanupKind.Hibernate:
                        try
                        {
                            var fi = new FileInfo(@"C:\hiberfil.sys");
                            if (fi.Exists) size = fi.Length;
                        }
                        catch { }
                        break;
                }
            }
            catch { }

            item.Size = size;
            item.Items = count;
        }

        public static CleanResult Clean(IEnumerable<CleanupItem> items, Action<string> progress)
        {
            var result = new CleanResult();
            foreach (var item in items)
            {
                if (progress != null) progress("正在清理：" + item.Name);
                switch (item.Kind)
                {
                    case CleanupKind.DirContents:
                        foreach (var p in item.Paths)
                        {
                            if (!Directory.Exists(p) || !IsSafeTarget(p)) continue;
                            ClearDirectory(p, result);
                        }
                        foreach (var file in item.Files)
                        {
                            if (!IsSafeTarget(file)) continue;
                            DeleteOne(file, result);
                        }
                        break;
                    case CleanupKind.Files:
                        foreach (var p in item.Paths)
                        {
                            if (!Directory.Exists(p) || !IsSafeTarget(p)) continue;
                            foreach (var pattern in item.Patterns)
                            {
                                string[] matches;
                                try { matches = Directory.GetFiles(p, pattern, SearchOption.TopDirectoryOnly); }
                                catch { result.Errors++; continue; }
                                for (int i = 0; i < matches.Length; i++) DeleteOne(matches[i], result);
                            }
                        }
                        break;
                    case CleanupKind.RecycleBin:
                        string root = RecycleRoot(item);
                        long rb = RecycleBinSize(root);
                        if (EmptyRecycleBin(root)) result.Freed += rb;
                        else result.Errors++;
                        break;
                    case CleanupKind.Hibernate:
                        long hib = HibernateSize();
                        if (RunHidden("powercfg.exe", "/h off")) result.Freed += hib;
                        else result.Errors++;
                        break;
                }
            }
            return result;
        }

        private static bool IsSafeTarget(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                string full = Path.GetFullPath(path).TrimEnd('\\');
                if (full.Length < 8) return false;
                string[] guard = { @"C:\Windows", @"C:\Users", @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows\System32" };
                foreach (var g in guard)
                {
                    if (string.Equals(full, g, StringComparison.OrdinalIgnoreCase)) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static void DeleteOne(string file, CleanResult result)
        {
            try
            {
                long len = 0;
                try { len = new FileInfo(file).Length; } catch { }
                File.Delete(file);
                result.Freed += len;
            }
            catch { result.Errors++; }
        }

        private static void ClearDirectory(string dir, CleanResult result)
        {
            var files = new List<string>();
            CollectFiles(dir, files, 0);
            long freed = 0;
            int errors = 0;
            if (files.Count > 0)
            {
                try
                {
                    Parallel.ForEach(files, delegate(string f)
                    {
                        try
                        {
                            long len = 0;
                            try { len = new FileInfo(f).Length; } catch { }
                            File.Delete(f);
                            Interlocked.Add(ref freed, len);
                        }
                        catch { Interlocked.Increment(ref errors); }
                    });
                }
                catch
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        try
                        {
                            long len = 0;
                            try { len = new FileInfo(files[i]).Length; } catch { }
                            File.Delete(files[i]);
                            freed += len;
                        }
                        catch { errors++; }
                    }
                }
            }
            RemoveEmptyDirs(dir, 0);
            result.Freed += freed;
            result.Errors += errors;
        }

        private static void CollectFiles(string dir, List<string> files, int depth)
        {
            if (depth > 40) return;
            string[] fs;
            string[] ds;
            try
            {
                fs = Directory.GetFiles(dir);
                ds = Directory.GetDirectories(dir);
            }
            catch { return; }

            files.AddRange(fs);
            for (int i = 0; i < ds.Length; i++)
            {
                try
                {
                    if ((File.GetAttributes(ds[i]) & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                CollectFiles(ds[i], files, depth + 1);
            }
        }

        private static void RemoveEmptyDirs(string dir, int depth)
        {
            if (depth > 40) return;
            string[] ds;
            try { ds = Directory.GetDirectories(dir); }
            catch { return; }

            for (int i = 0; i < ds.Length; i++)
            {
                try
                {
                    if ((File.GetAttributes(ds[i]) & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                RemoveEmptyDirs(ds[i], depth + 1);
                try { Directory.Delete(ds[i], false); } catch { }
            }
        }

        private static string RecycleRoot(CleanupItem item)
        {
            if (item != null && item.Paths.Count > 0 && !string.IsNullOrEmpty(item.Paths[0]))
                return item.Paths[0];
            return "C:\\";
        }

        private static long RecycleBinSize(string root)
        {
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                if (SHQueryRecycleBin(root, ref info) == 0) return info.i64Size;
            }
            catch { }
            return 0;
        }

        private static bool EmptyRecycleBin(string root)
        {
            try
            {
                int hr = SHEmptyRecycleBin(IntPtr.Zero, root, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                return hr == 0 || (uint)hr == 0x8000FFFFu;
            }
            catch { return false; }
        }

        private static long HibernateSize()
        {
            try
            {
                var fi = new FileInfo(@"C:\hiberfil.sys");
                if (fi.Exists) return fi.Length;
            }
            catch { }
            return 0;
        }

        private static bool RunHidden(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    p.WaitForExit(20000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch { return false; }
        }
    }
}
