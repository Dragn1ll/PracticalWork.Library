namespace PracticalWork.Email.Tests;

/// <summary>
/// Тестовая реализация TimeProvider с фиксированным временем
/// </summary>
public class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;
 
    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }
 
    public override DateTimeOffset GetUtcNow() => _utcNow;
}