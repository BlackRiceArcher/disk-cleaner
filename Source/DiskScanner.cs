using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CDiskCleaner
{
    internal sealed class DiskScanner
    {
        public long FileCount;
        public long DirCount;
        public long ErrorCount;

        private CancellationToken _token;

        public Node Scan(string rootPath, CancellationToken token)
        {
            _token = token;
            var root = new Node(rootPath, true, null);
            root.Size = ScanDir(root, 0);
            return root;
        }

        private long ScanDir(Node dir, int depth)
        {
            _token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref DirCount);

            var children = new List<Node>();
            var subDirs = new List<Node>();
            long bytes = 0;

            try
            {
                var info = new DirectoryInfo(dir.FullPath);
                foreach (FileSystemInfo entry in info.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                {
                    _token.ThrowIfCancellationRequested();
                    try
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((entry.Attributes & FileAttributes.Directory) != 0)
                        {
                            var d = new Node(entry.Name, true, dir);
                            subDirs.Add(d);
                            children.Add(d);
                        }
                        else
                        {
                            long len = ((FileInfo)entry).Length;
                            var f = new Node(entry.Name, false, dir);
                            f.Size = len;
                            f.Modified = entry.LastWriteTime;
                            children.Add(f);
                            bytes += len;
                            Interlocked.Increment(ref FileCount);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref ErrorCount);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                Interlocked.Increment(ref ErrorCount);
            }

            if (subDirs.Count > 0)
            {
                if (depth < 5 && subDirs.Count > 1)
                {
                    var sizes = new long[subDirs.Count];
                    try
                    {
                        var opt = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = _token };
                        Parallel.For(0, subDirs.Count, opt, i =>
                        {
                            sizes[i] = ScanDir(subDirs[i], depth + 1);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    for (int i = 0; i < sizes.Length; i++) bytes += sizes[i];
                }
                else
                {
                    for (int i = 0; i < subDirs.Count; i++) bytes += ScanDir(subDirs[i], depth + 1);
                }
            }

            dir.Size = bytes;
            if (children.Count > 0)
            {
                children.Sort(delegate(Node a, Node b) { return b.Size.CompareTo(a.Size); });
                dir.Children = children;
            }
            return bytes;
        }
    }
}
