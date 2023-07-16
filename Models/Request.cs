using Microsoft.AspNetCore.Mvc;

namespace WhisperAPI.Models;

public record Request(
    [FromForm(Name = "lang")] string? Lang,
    [FromForm(Name = "translate")] bool Translate,
    [FromForm(Name = "model")] string Model
);