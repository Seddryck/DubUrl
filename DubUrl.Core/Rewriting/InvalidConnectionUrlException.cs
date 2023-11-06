using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting
{
    public class InvalidConnectionUrlException : DubUrlException
    {
        public InvalidConnectionUrlException(string message)
            : base(message) { }
    }

    public class InvalidConnectionUrlTooManySegmentsException : InvalidConnectionUrlException
    {
        public InvalidConnectionUrlTooManySegmentsException(string database, string[] segments)
            : base($"Connection-url for {database} is not expecting more than a single segment. This connection-url is containing {segments.Length} segments: '{string.Join("', '", segments)}'.") { }

        public InvalidConnectionUrlTooManySegmentsException(string database, string[] segments, int countMaxExpected)
            : base($"Connection-url for {database} is not expecting more than {countMaxExpected} segments. This connection-url is containing {segments.Length} segments: '{string.Join("', '", segments)}'.") { }
    }

    public class InvalidConnectionUrlMissingSegmentsException : InvalidConnectionUrlException
    {
        public InvalidConnectionUrlMissingSegmentsException(string database)
            : base($"Connection-url for {database} is expecting a minimum of one segment. This connection-url is not containing any segment") { }

        public InvalidConnectionUrlMissingSegmentsException(string database, string[] segments, int countMinExpected)
            : base($"Connection-url for {database} is expecting a minimum of {countMinExpected} segments. This connection-url is {(segments.Length==0 ? "not containing any segment" :  $"{segments.Length} segments: '{string.Join("', '", segments)}'")}") { }
    }
}
