using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Whisper.net.Ggml;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class ModelsController : ControllerBase
{
    [HttpGet]
    [Produces(MediaTypeNames.Application.Xml, MediaTypeNames.Application.Json)]
    public IActionResult Get() => Ok(Enum.GetNames(typeof(GgmlType)));
}