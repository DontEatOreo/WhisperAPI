using MediatR;

namespace WhisperAPI.Commands;

public sealed record ConvertToWavCommand(string Input, string Output) : IRequest;