using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    public abstract class RendererAttribute : Attribute
    {
        public virtual Type RendererType { get; protected set; } = typeof(AnsiRenderer);

        public RendererAttribute(Type rendererType)
        {
            RendererType = rendererType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class RendererAttribute<R> : RendererAttribute where R : IRenderer
    {
        public RendererAttribute()
            : base(typeof(R))
        { }
    }
}
