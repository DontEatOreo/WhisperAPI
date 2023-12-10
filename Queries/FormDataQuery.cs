using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Queries;

public record FormDataQuery(string File, string? Lang, TranscriptQuery Query) : IRequest<WhisperOptions>;