using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;

namespace DubUrl.Testing.Rewriting;
internal class PathHelper
{
    public string Create(string rootPath, IEnumerable<string> segments)
    {
        if (segments == null || !segments.Any())
            throw new InvalidConnectionUrlMissingSegmentsException("Sqlite");

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
