using Microsoft.AspNetCore.Mvc;

namespace WhisperAPI.Models;

public class PostRequest
{
    [FromForm(Name = "time_stamps")]
    public bool TimeStamps { get; set; }

    [FromForm(Name = "lang")]
    public string Lang { get; set; } = "auto";

    [FromForm(Name = "translate")]
    public bool Translate { get; set; }

    [FromForm(Name = "model")]
    public string Model { get; set; } = "base";
}