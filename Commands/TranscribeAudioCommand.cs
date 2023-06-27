using MediatR;
using WhisperAPI.Models;

namespace WhisperAPI.Commands;

public sealed record TranscribeAudioCommand(AudioOptions Options)  : IRequest<PostResponseRoot>;