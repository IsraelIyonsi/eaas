using EaaS.Api.Features.Domains;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Domains;

public sealed class AddDomainValidatorTests
{
    private readonly AddDomainValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidDomain()
    {
        var command = TestDataBuilders.AddDomain()
            .WithDomainName("example.com")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_EmptyDomain()
    {
        var command = TestDataBuilders.AddDomain()
            .WithDomainName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DomainName);
    }

    [Fact]
    public void Should_Fail_When_InvalidDomainFormat()
    {
        var command = TestDataBuilders.AddDomain()
            .WithDomainName("not a domain!!")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DomainName)
            .WithErrorMessage("Domain name format is invalid.");
    }
}
