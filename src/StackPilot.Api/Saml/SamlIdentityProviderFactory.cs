using System.Security.Cryptography.X509Certificates;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

namespace StackPilot.Api.Saml;

public static class SamlIdentityProviderFactory
{
    public static IdentityProvider Create(
        string idpEntityId,
        string? ssoUrl,
        string pemCertificate,
        SPOptions spOptions)
    {
        var idp = new IdentityProvider(new EntityId(idpEntityId), spOptions);

        if (!string.IsNullOrWhiteSpace(ssoUrl))
            idp.SingleSignOnServiceUrl = new Uri(ssoUrl);

        var cert = X509Certificate2.CreateFromPem(pemCertificate);
        idp.SigningKeys.AddConfiguredKey(cert);

        return idp;
    }
}
