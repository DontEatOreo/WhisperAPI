using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Queries;

public sealed record TranscriptQuery(AudioOptions Options)  : IRequest<PostResponseRoot>;