using System.Data;
using DubUrl.Adomd.Wrappers;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd;

public static class AdomdCommandExtensions
{
    /// <summary>
    /// Executes the configured MDX command and returns the native cellset with its axes,
    /// cell order, raw values, and server-formatted values.
    /// </summary>
    /// <param name="command">A command created by a DubUrl ADOMD connection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    /// <exception cref="NotSupportedException">The command was not created by DubUrl ADOMD.</exception>
    public static CellSet ExecuteCellSet(this IDbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command is AdomdCommandWrapper { InnerCommand: AdomdCommand adomdCommand })
            return adomdCommand.ExecuteCellSet();

        throw new NotSupportedException("ExecuteCellSet requires a command created by DubUrl ADOMD.");
    }
}
