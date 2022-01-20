using Microsoft.VisualStudio.TestTools.UnitTesting;
using JefimsIncredibleXsltTool.Lib;
using System.Xml;
using System;
using System.Text;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class IncludeFileTest
    {
        public string ExtractInclude(XmlDocument xslt)
        {
            XmlNode root = xslt.DocumentElement;
            string result = "null";

            try
            {
                result = root.SelectSingleNode("//*[local-name()='include']/@href").InnerText;
            } catch (NullReferenceException ex)
            {
                Console.WriteLine("[CRITICAL] Null exception thrown in ExtractInclude().");
            }

            Console.WriteLine("[VERBOSE] Extracted '" + result + "' from XSLT.");
            return result;
        }

        //[TestMethod]
        //public void Test_IfIncludeTagExists()
        //{
        //    var xml = "@" +
        //        "<?xml version='1.0' encoding='utf-8'?>" +
        //        "<xsl:stylesheet" +
        //        "version='1.0'" +
        //        "xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
        //        "<product>" +
        //            "<title><xsl:value-of select='title'/></title>" +
        //            "<price>35.99</price>" +
        //        "</product>" +
        //        "</xsl:stylesheet>";
        //    bool includeFound = xml.Contains("<xsl:include");
        //    Assert.IsTrue(includeFound);
        //}

        [TestMethod]
        public void Test_ExtractInclude()
        {
            var xml = @"
<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>
<xsl:include href='Dependency.xslt'/>
<breakfast_menu>
    <food>
        <calories>950</calories>
    </food>
</breakfast_menu>
</xsl:stylesheet>";

            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);

            Assert.IsNotNull(ExtractInclude(document));
        }

        [TestMethod]
        public void Test_LoadFileSuccessful()
        {
            var xsltA = @"
<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>
<xsl:include href='C:\Users\Andre\source\repos\JefimsIncredibleXsltTool\JefimsIncredibleXSLTTool\Tests\References\IncludeXSLTFile.xslt'/>
<chipotle>
    <burrito>
        <calories><xsl:value-of select='calories'/></calories>
    </burrito>
</chipotle>
</xsl:stylesheet>";

            XmlDocument documentA = new XmlDocument();
            documentA.LoadXml(xsltA);

            XmlDocument documentB = new XmlDocument();
            documentB.Load(ExtractInclude(documentA));

            Console.WriteLine("[INFORMATIONAL] " + documentB.DocumentElement.ChildNodes.Count + " Nodes in doc B.");
            foreach (XmlNode node in documentB.DocumentElement.ChildNodes)
            {
                XmlNode imported = documentA.ImportNode(node, true);
                documentA.DocumentElement.AppendChild(imported);
                Console.WriteLine(" >> Node  '" + imported.OuterXml + "'.");
            }

            Console.WriteLine("[INFORMATIONAL] Finished processing.");
        }

        [TestMethod]
        public void Test_CompileCombinedXSLT()
        {
            var xsltA = @"
<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>
<xsl:include href='C:\Users\Andre\source\repos\JefimsIncredibleXsltTool\JefimsIncredibleXSLTTool\Tests\References\IncludeXSLTFile.xslt'/>
<chipotle>
    <burrito>
        <calories><xsl:value-of select='calories'/></calories>
    </burrito>
</chipotle>
</xsl:stylesheet>";

            var xml = @"
<?xml version='1.0' encoding='UTF-8'?>
<xsl:include href='C:\Users\Andre\source\repos\JefimsIncredibleXsltTool\JefimsIncredibleXSLTTool\Tests\References\IncludeXSLTFile.xslt'/>
<doordash_order>
    <xsl:variable/>
</dashdoor_order>";


            XmlDocument documentA = new XmlDocument();
            documentA.LoadXml(xsltA);

            XmlDocument documentB = new XmlDocument();
            documentB.Load(ExtractInclude(documentA));

            Console.WriteLine("[INFORMATIONAL] " + documentB.DocumentElement.ChildNodes.Count + " Nodes in doc B.");
            foreach (XmlNode node in documentB.DocumentElement.ChildNodes)
            {
                XmlNode imported = documentA.ImportNode(node, true);
                documentA.DocumentElement.AppendChild(imported);
                Console.WriteLine(" >> Node  '" + imported.OuterXml + "'.");
            }

            //TextDocument _document;
            Document _document = new Document(documentB.OuterXml);
            Console.WriteLine("[DEBUG] _document: '" + documentB.OuterXml + "'.");

            var XsltParameters = new ObservableCollection<XsltParameter>();
            //string result = XsltTransformSaxon(xml, xsltA, XsltParameters.Where(o => o?.Name != null).ToArray());
            string result = XsltTransformSaxon(xml, _document.TextDocument.Text, XsltParameters.Where(o => o?.Name != null).ToArray());

            Console.WriteLine("[INFORMATIONAL] Finished processing.");
        }

        public static string XsltTransformSaxon(string xmlString, string xslt, XsltParameter[] xsltParameters)
        {
            var processor = new Processor();
            var compiler = processor.NewXsltCompiler();

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
