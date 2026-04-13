using backend.Models;

namespace backend.Data;

public sealed class InMemoryDatabase
{
    public List<UserDto> Users { get; } = new();
    public List<MediaUploadResponse> MediaFiles { get; } = new();
}
