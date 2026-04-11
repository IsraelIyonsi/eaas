# Backend Testing Standards

xUnit + FluentAssertions + NSubstitute

## Test Class Structure

```csharp
public sealed class FeatureHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly FeatureHandler _sut;

    public FeatureHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new FeatureHandler(_dbContext);
    }

    [Fact]
    public async Task Should_ExpectedBehavior_WhenCondition() { ... }

    public void Dispose() => _dbContext.Dispose();
}
```

## Rules

- Sealed test classes implementing `IDisposable`
- Naming: `Should_ExpectedBehavior_WhenCondition`
- Handler tests: success + each failure case
- Validator tests: valid + each invalid field (use `FluentValidation.TestHelper`)
- Use `TestDataBuilders` for all test data — never raw constructors
- `_sut` naming convention for system under test
- `DbContextFactory.Create()` for in-memory database
