using System;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.FileStoring;

public class ContainerNameValidator_Tests
{
    [Fact]
    public void Validate_Should_Reject_Unregistered_Container()
    {
        var options = new AbpBlobStoringOptions();
        options.Containers.Configure("documents", _ => { });
        var provider = new DefaultBlobContainerConfigurationProvider(Options.Create(options));
        var validator = new ContainerNameValidator(provider);

        validator.Validate("documents");
        validator.Validate("Default");
        Should.Throw<BusinessException>(() => validator.Validate("unregistered"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Reject_Invalid_Container_Name(string name)
    {
        var validator = new ContainerNameValidator();

        Should.Throw<ArgumentException>(() => validator.Validate(name));
    }

    [Fact]
    public void Validate_Should_Reject_Overlong_Container_Name()
    {
        var validator = new ContainerNameValidator();

        Should.Throw<ArgumentException>(() => validator.Validate(new string('a', FileConsts.MaxContainerNameLength + 1)));
    }
}
