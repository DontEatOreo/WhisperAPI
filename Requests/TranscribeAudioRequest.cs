using System.Text.Json;
using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Requests;

public record TranscribeAudioRequest(IFormFile File, PostRequest Request) : IRequest<JsonDocument>;