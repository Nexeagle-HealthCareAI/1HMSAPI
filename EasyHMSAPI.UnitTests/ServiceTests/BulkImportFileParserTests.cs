using System.IO;
using System.Text;
using ClosedXML.Excel;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class BulkImportFileParserTests
    {
        [Test]
        public void ResolveCanonicalField_RecognizesCommonAliasSpellings()
        {
            Assert.That(BulkImportFileParser.ResolveCanonicalField("Exp Date"), Is.EqualTo("EXPIRYDATE"));
            Assert.That(BulkImportFileParser.ResolveCanonicalField("Val Date"), Is.EqualTo("EXPIRYDATE"));
            Assert.That(BulkImportFileParser.ResolveCanonicalField("Item Code"), Is.EqualTo("ITEMCODE"));
            Assert.That(BulkImportFileParser.ResolveCanonicalField("SKU"), Is.EqualTo("ITEMCODE"));
            Assert.That(BulkImportFileParser.ResolveCanonicalField("Random Column"), Is.Null);
        }

        [Test]
        public void Parse_Csv_MapsHeadersAndSkipsBlankRows()
        {
            var csv = "Store,Item Code,Batch No,Qty,Rate\n"
                    + "MAIN,PARA,B-001,10,2.5\n"
                    + "\n"
                    + "MAIN,CALPOL,B-002,5,3.0\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var rows = BulkImportFileParser.Parse(stream, "stock.csv");

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0]["STORECODE"], Is.EqualTo("MAIN"));
            Assert.That(rows[0]["ITEMCODE"], Is.EqualTo("PARA"));
            Assert.That(rows[0]["RECEIVEDQTY"], Is.EqualTo("10"));
            Assert.That(rows[1]["ITEMCODE"], Is.EqualTo("CALPOL"));
        }

        [Test]
        public void Parse_Csv_HandlesQuotedFieldsWithEmbeddedCommas()
        {
            var csv = "Store,Item Code,Batch No,Qty\n"
                    + "MAIN,PARA,\"B-001, Lot A\",10\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var rows = BulkImportFileParser.Parse(stream, "stock.csv");

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["BATCHNUMBER"], Is.EqualTo("B-001, Lot A"));
        }

        [Test]
        public void Parse_Csv_IgnoresUnrecognizedColumns()
        {
            var csv = "Store,Item Code,Some Random Column,Qty\nMAIN,PARA,whatever,10\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var rows = BulkImportFileParser.Parse(stream, "stock.csv");

            Assert.That(rows[0].ContainsKey("SOME RANDOM COLUMN"), Is.False);
            Assert.That(rows[0]["RECEIVEDQTY"], Is.EqualTo("10"));
        }

        [Test]
        public void Parse_Xlsx_MapsHeadersAndReadsDataRows()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Sheet1");
            ws.Cell(1, 1).Value = "Store";
            ws.Cell(1, 2).Value = "Item Code";
            ws.Cell(1, 3).Value = "Batch No";
            ws.Cell(1, 4).Value = "Qty";
            ws.Cell(2, 1).Value = "MAIN";
            ws.Cell(2, 2).Value = "PARA";
            ws.Cell(2, 3).Value = "B-001";
            ws.Cell(2, 4).Value = 10;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var rows = BulkImportFileParser.Parse(stream, "stock.xlsx");

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["STORECODE"], Is.EqualTo("MAIN"));
            Assert.That(rows[0]["ITEMCODE"], Is.EqualTo("PARA"));
            Assert.That(rows[0]["BATCHNUMBER"], Is.EqualTo("B-001"));
        }

        [Test]
        public void Parse_UnsupportedExtension_Throws()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("irrelevant"));
            Assert.Throws<System.InvalidOperationException>(() => BulkImportFileParser.Parse(stream, "stock.pdf"));
        }
    }
}
