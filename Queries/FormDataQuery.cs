using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Queries;

public record FormDataQuery(string File, TranscriptQuery Query) : IRequest<WhisperOptions>;