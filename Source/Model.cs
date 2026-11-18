using System;
using System.Collections.Generic;

namespace CDiskCleaner
{
    internal sealed class Node
    {
        public string Name;
        public Node Parent;
        public bool IsDir;
        public long Size;
        public DateTime Modified;
        public List<Node> Children;

        public Node(string name, bool isDir, Node parent)
        {
            Name = name;
            IsDir = isDir;
            Parent = parent;
        }

        public string FullPath
        {
            get
            {
                if (Parent == null) return Name;
                string p = Parent.FullPath;
                if (!p.EndsWith("\\", StringComparison.Ordinal)) p += "\\";
                return p + Name;
            }
        }

        public string Extension
        {
            get
            {
                if (IsDir || Name == null) return "";
                int i = Name.LastIndexOf('.');
                if (i < 0 || i == Name.Length - 1) return "";
                return Name.Substring(i + 1).ToLowerInvariant();
            }
        }

        public void Adopt(Node other)
        {
            Size = other.Size;
            Modified = other.Modified;
            Children = other.Children;
            if (Children != null)
            {
                foreach (var c in Children) c.Parent = this;
            }
        }
    }
}
