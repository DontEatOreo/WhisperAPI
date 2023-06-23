using Microsoft.AspNetCore.Mvc;

namespace WhisperAPI.Models;

public record PostRequest(
    [FromForm(Name = "lang")] string? Lang,
    [FromForm(Name = "translate")] bool Translate,
    [FromForm(Name = "model")] string Model
);