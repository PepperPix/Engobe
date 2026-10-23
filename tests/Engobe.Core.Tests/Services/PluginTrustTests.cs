namespace Engobe.Core.Tests.Services;

using Engobe.Models;
using Engobe.Services;

public class PluginTrustTests
{
    [Test]
    [Arguments("Engobe.Plugin.EmailProtect", true, PluginTrustLevel.FirstParty)]
    [Arguments("engobe.plugin.emailprotect", true, PluginTrustLevel.FirstParty)]
    [Arguments("ENGOBE.PLUGIN.Seo", true, PluginTrustLevel.FirstParty)]
    [Arguments("Engobe.Plugin.EmailProtect", false, PluginTrustLevel.Community)]
    [Arguments("Kiln.Plugin.EmailProtect", true, PluginTrustLevel.Community)]
    [Arguments("Contoso.Engobe.Plugin.Widget", true, PluginTrustLevel.Community)]
    [Arguments("Contoso.Widget", true, PluginTrustLevel.Community)]
    [Arguments("Contoso.Widget", false, PluginTrustLevel.Community)]
    [Arguments("Engobe.Plugin", true, PluginTrustLevel.Community)]
    public async Task Classify_ReturnsFirstPartyOnlyForReservedEngobePluginPrefix(string packageId, bool prefixReserved, PluginTrustLevel expected)
    {
        var level = PluginTrust.Classify(packageId, prefixReserved);

        await Assert.That(level).IsEqualTo(expected);
    }
}
