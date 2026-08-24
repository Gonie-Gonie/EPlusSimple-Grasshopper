using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class InvariantCsvTests
{
    [Fact]
    public void ParserHandlesUtf8QuotedCommasEscapedQuotesAndEmbeddedNewlines()
    {
        CsvDocument document = CsvDocument.Parse(
            "Name,Value,Note\r\n\"서울, 종로\",1.25,\"a \"\"quote\"\"\"\r\n둘,2,\"line one\nline two\"\r\n",
            "test.csv");

        Assert.Equal(2, document.Rows.Count);
        Assert.Equal("서울, 종로", document.Rows[0].Required("Name"));
        Assert.Equal(1.25d, document.Rows[0].Number("Value"));
        Assert.Equal("a \"quote\"", document.Rows[0].Required("Note"));
        Assert.Equal("line one\nline two", document.Rows[1].Required("Note"));
    }

    [Fact]
    public void ParserStripsUnitSuffixesAndIgnoresOnlyEmptyTrailingColumns()
    {
        CsvDocument document = CsvDocument.Parse(
            "Name,Power [W],\nDragon,12.5, \n",
            "units.csv",
            stripHeaderUnits: true);

        Assert.Collection(
            document.Headers,
            header => Assert.Equal("Name", header),
            header => Assert.Equal("Power", header));
        Assert.Equal(12.5d, document.Rows[0].Number("Power"));
    }

    [Fact]
    public void ParserRejectsMalformedRowWidthWithSourceAndRowDiagnostic()
    {
        CsvDataException exception = Assert.Throws<CsvDataException>(
            () => CsvDocument.Parse("A,B\n1\n", "bad.csv"));

        Assert.Equal("bad.csv", exception.SourceName);
        Assert.Equal(2, exception.RowNumber);
    }
}
