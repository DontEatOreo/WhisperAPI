using CsvHelper.Configuration.Attributes;

namespace WhisperAPI.Models;

public struct CsvFile
{
    [Name("start")]
    public string Start { get; set; }

    [Name("end")]
    public string End { get; set; }

    [Name("text")]
    public string Text { get; set; }
}