using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;

public sealed class ForeignKeyConstraintViewModel
{
    public string Name { get; }
    public string[] SourceColumns { get; }
    public string TargetTableName { get; }
    public string[] TargetColumns { get; }

    public ForeignKeyConstraintViewModel(ForeignKeyConstraint constraint)
    {
        Name = constraint.Name!;
        SourceColumns = constraint.SourceColumns.ToArray();
        TargetTableName = constraint.TargetTableName;
        TargetColumns = constraint.TargetColumns.ToArray();
    }
}
