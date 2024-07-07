using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters;
internal class DateTimeToTimeSpanCaster : ICaster<TimeSpan, DateTime>
{
    public bool CanCast(Type from, Type to)
        => typeof(TimeSpan).Equals(to) && typeof(DateTime).Equals(from);
    public TimeSpan Cast(DateTime value)
        => new(value.Ticks);
    public object? Cast(object value)
        => value is DateTime time ? Cast(time) : null;
}
