namespace MamCom.Core.Models;

/// <summary>Image reference shared by ingredients and dishes. A declared image must carry <c>Source</c> and <c>License</c>.</summary>
/// <param name="IsIllustration">True for AI or placeholder images; the UI labels them "Ảnh minh hoạ".</param>
public sealed record ImageRef(string? Thumb, string? Full, string? Source, string? License, bool IsIllustration);
