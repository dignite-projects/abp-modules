using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.Localization.External;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Stands in for Language Management's external localization store: it knows the "PublisherA" resource, which this
/// process never registers in <see cref="AbpLocalizationOptions"/>.
/// </summary>
[DisableConventionalRegistration]
public class InMemoryExternalLocalizationStore : IExternalLocalizationStore
{
    public static readonly IReadOnlyDictionary<string, string> PublisherATexts = new Dictionary<string, string>
    {
        ["Notification:Group:Orders"] = "Orders",
        ["Notification:OrderShipped"] = "Your order has shipped",
        ["Notification:OrderShipped:Description"] = "Sent when an order leaves the warehouse"
    };

    private readonly LocalizationResourceBase _publisherA;

    public InMemoryExternalLocalizationStore()
    {
        _publisherA = new NonTypedLocalizationResource(
            PublisherADefinitionProvider.ResourceName,
            "en",
            new DictionaryLocalizationResourceContributor("en", PublisherATexts));
    }

    public LocalizationResourceBase? GetResourceOrNull(string resourceName)
    {
        return resourceName == _publisherA.ResourceName ? _publisherA : null;
    }

    public Task<LocalizationResourceBase?> GetResourceOrNullAsync(string resourceName)
    {
        return Task.FromResult(GetResourceOrNull(resourceName));
    }

    public Task<string[]> GetResourceNamesAsync()
    {
        return Task.FromResult(new[] { _publisherA.ResourceName });
    }

    public Task<LocalizationResourceBase[]> GetResourcesAsync()
    {
        return Task.FromResult(new[] { _publisherA });
    }

    private sealed class DictionaryLocalizationResourceContributor : ILocalizationResourceContributor
    {
        private readonly string _cultureName;
        private readonly IReadOnlyDictionary<string, string> _texts;

        public DictionaryLocalizationResourceContributor(string cultureName, IReadOnlyDictionary<string, string> texts)
        {
            _cultureName = cultureName;
            _texts = texts;
        }

        public bool IsDynamic => false;

        public void Initialize(LocalizationResourceInitializationContext context)
        {
        }

        public LocalizedString? GetOrNull(string cultureName, string name)
        {
            return cultureName == _cultureName && _texts.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : null;
        }

        public void Fill(string cultureName, Dictionary<string, LocalizedString> dictionary)
        {
            if (cultureName != _cultureName)
            {
                return;
            }

            foreach (var (name, value) in _texts)
            {
                dictionary[name] = new LocalizedString(name, value);
            }
        }

        public Task FillAsync(string cultureName, Dictionary<string, LocalizedString> dictionary)
        {
            Fill(cultureName, dictionary);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetSupportedCulturesAsync()
        {
            return Task.FromResult<IEnumerable<string>>(new[] { _cultureName });
        }
    }
}
