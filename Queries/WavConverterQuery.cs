using MediatR;

namespace WhisperAPI.Queries;

public sealed record WavConverterQuery(string Input, string Output) : IRequest;