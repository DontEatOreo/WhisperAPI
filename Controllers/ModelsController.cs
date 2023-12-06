using Microsoft.AspNetCore.Mvc;
using Whisper.net.Ggml;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class ModelsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(Enum.GetNames(typeof(GgmlType)));
}