using System.Text.Json;
using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Requests;

public record TranscribeRequest(IFormFile File, Request Request) : IRequest<JsonDocument>;