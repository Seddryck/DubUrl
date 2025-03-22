using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Didot.Core.TemplateEngines;
using Didot.Core;
using System.Reflection;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Templating;
using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;
public class DropTablesIfExistsRenderer : RendererEngine
{
    public DropTablesIfExistsRenderer(IDialect dialect)
        : this(dialect.DbTypeMapper)
    { }

    public DropTablesIfExistsRenderer(IDbTypeMapper typeMapper)
        : base(typeof(DropTablesIfExistsRenderer).Assembly, $"{typeof(DropTablesIfExistsRenderer).Namespace}.Templates.DropTablesIfExists.sql.hbs")
    { }
}
