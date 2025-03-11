using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;
public class PathHelper
{
    public static string Create(string rootPath, IEnumerable<string> segments)
    {
        var path = new StringBuilder();
        var first = segments.First();
        if (!Path.IsPathRooted(first))
            path.Append(rootPath);

        foreach (var segment in segments)
            if (!string.IsNullOrEmpty(segment))
                path.Append(segment).Append(Path.DirectorySeparatorChar);
        path.Remove(path.Length - 1, 1);
        return path.ToString();
    }
}
