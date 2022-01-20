using JefimsIncredibleXsltTool.Lib;
using JUST;
using Saxon.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JefimsIncredibleXsltTool.Transformers
{
    public class SaxonTransformer
    {
        public static string XsltTransformSaxon(string xmlString, string xslt, XsltParameter[] xsltParameters)
        {
            var processor = new Processor();
            var compiler = processor.NewXsltCompiler();
            compiler.ErrorList = new List<StaticError>();

            using (var xmlDocumentOut = new StringWriter())
            using (var xsltReader = new StringReader(xslt))
            using (var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes(xmlString)))
            {
                XsltExecutable executable;
                try
                {
                    executable = compiler.Compile(xsltReader);
                }
                catch (Exception ex)
                {
                    var errorsStr = string.Join(Environment.NewLine, ((List<StaticError>)compiler.ErrorList).Select(o => $"{o.Message} at line {o.LineNumber}, column {o.ColumnNumber}").Distinct());
                    if (string.IsNullOrWhiteSpace(errorsStr))
                    {
                        throw;
                    }
                    throw new Exception(ex.Message, new Exception(errorsStr));
                }

                var transformer = executable.Load();
                transformer.SetInputStream(xmlStream, new Uri("file://"));
                xsltParameters?.ToList().ForEach(x => transformer.SetParameter(new QName(x.Name), new XdmAtomicValue(x.Value)));

                var serializer = processor.NewSerializer();
                serializer.SetOutputWriter(xmlDocumentOut);
                transformer.Run(serializer);
                return xmlDocumentOut.ToString().Replace("\n", Environment.NewLine);
            }
        }
    }
}
