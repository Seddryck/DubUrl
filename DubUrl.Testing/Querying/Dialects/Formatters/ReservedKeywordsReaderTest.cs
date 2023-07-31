using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;

namespace DubUrl.Testing.Querying.Dialects.Formatters
{
    public class ReservedKeywordsReaderTest
    {
        private class TestableReservedKeywordsReader : ReservedKeywordsReader
        {
            private Stream Stream { get; }
            public TestableReservedKeywordsReader(string[] values)
                : base(Assembly.GetExecutingAssembly(), "Any")
            {
                Stream = new MemoryStream();
                var writer = new StreamWriter(Stream);
                writer.Write(string.Join("\r\n", values));
                writer.Flush();
                Stream.Position = 0;
            }

            public override IEnumerable<string> ReadAll() 
                => ReadAll(Stream);

            protected override void DisposeManagedState()
                => Stream?.Dispose();
        }

        [Test]
        [TestCase("TIME, ROW, POSITION", "TIME,ROW,POSITION")]
        [TestCase("   TIME  , , POSITION   ", "TIME,POSITION")]
        [TestCase("TIME,//ROW, POSITION   ", "TIME,POSITION")]
        [TestCase("TIME,  // ROW IS ROW ,POSITION", "TIME,POSITION")]
        public void ReadAll_MultipleKeywords_Returned(string input, string expected)
        {
            using var reader = new TestableReservedKeywordsReader(input.Split(','));
            var result = reader.ReadAll().ToArray();
            Assert.That(result, Is.EqualTo(expected.Split(',')));
        }
    }
}
