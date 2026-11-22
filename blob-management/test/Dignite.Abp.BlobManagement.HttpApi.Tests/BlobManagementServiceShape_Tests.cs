using System;
using System.Linq;
using System.Reflection;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Files;
using Shouldly;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// How the services that answer over HTTP are built, pinned: an integration service that authorizes its caller does not
/// inherit the trusted in-process implementation, and the base classes they share give nothing public. So every method
/// of a contract is written in the service that exposes it, with its authorization - one added to a contract does not
/// reach a remote caller through an inherited, unauthorized body. Reflection only; nothing is hosted.
/// </summary>
public class BlobManagementServiceShape_Tests
{
    [Fact]
    public void An_Integration_Service_Should_Not_Inherit_The_Trusted_Implementation()
    {
        typeof(BlobManagementBlobService).IsAssignableFrom(typeof(BlobManagementBlobIntegrationService)).ShouldBeFalse();
        typeof(BlobManagementFileService).IsAssignableFrom(typeof(BlobManagementFileIntegrationService)).ShouldBeFalse();
        typeof(BlobManagementFileService).IsAssignableFrom(typeof(BlobManagementFileAppService)).ShouldBeFalse();
        typeof(BlobManagementFileIntegrationService).IsAssignableFrom(typeof(BlobManagementFileAppService)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(BlobManagementBlobServiceBase))]
    [InlineData(typeof(BlobManagementFileServiceBase))]
    public void A_Shared_Base_Should_Declare_No_Public_Method(Type baseType)
    {
        baseType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ShouldBeEmpty();
    }
}
