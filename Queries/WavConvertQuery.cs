using MediatR;

namespace WhisperAPI.Queries;

public sealed record WavConvertQuery(IFormFile Stream) : IRequest<(string, Func<Task>)>;