namespace Brqosh.Api.Tests;

public sealed class ApiTestFixture : IDisposable
{
    public IsolatedApiFactory Factory { get; } = new();

    public void Dispose() => Factory.Dispose();
}
