// Original copied from https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar

using Kerberos.NET;
using Kerberos.NET.Client;
using Kerberos.NET.Credentials;
using Kerberos.NET.Entities;
using Kerberos.NET.Transport;
using Microsoft.Extensions.Options;
using Kerberos.NET.Configuration;
using Microsoft.FeatureManagement;
using Kerberos.NET.Crypto;

namespace Kerberos.Client.Manager;

public static class Extensions
{
    public static IServiceCollection AddKerberosClientManagement(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KerberosOptions>()
            .Configure(c =>
            {
                c.Kerb5ConfigFile = configuration.GetValue<string>("KRB5_CONFIG");
                c.CacheFile = configuration.GetValue<string>("KRB5CCNAME");
                c.KeytabFile = configuration.GetValue<string>("KRB5_KTNAME");
                c.ServiceAccount = configuration.GetValue<string>("KRB_SERVICE_ACCOUNT");
                c.Password = configuration.GetValue<string>("KRB_PASSWORD");
                c.Kdc = configuration.GetValue<string>("KRB_KDC");
                c.RunOnce = configuration.GetValue<bool>("KRB_RunOnce");
                c.ApplicationHostName = configuration.GetValue<string>("APP_HOSTNAME");
            })
            .PostConfigure<ILoggerFactory>((options, loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("KerberosExtensions");

                if (options.ApplicationHostName == null)
                    logger.LogWarning("Service/Application hostname is not set. Use APP_HOSTNAME environmental variable to configure windows ingress authentication");

                options.GenerateKrb5 = options.Kerb5ConfigFile == null! || !File.Exists(options.Kerb5ConfigFile);

                Directory.CreateDirectory(Path.GetDirectoryName(options.Kerb5ConfigFile)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.KeytabFile)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.CacheFile)!);

                logger.LogDebug($"Kerb5 Config File - {options.Kerb5ConfigFile}");
                logger.LogDebug($"Kerb5 Keytab File - {options.KeytabFile}");
                logger.LogDebug($"Kerb5 Cache File - {options.CacheFile}");

                Krb5Config config;
                if (options.GenerateKrb5)
                {
                    logger.LogWarning($"No krb5.conf exists - generating the defaults may not work for your specific environment, in that case better to provide your own {options.KeytabFile} file...");
                    logger.LogInformation($"No krb5.conf exists - generating {options.KeytabFile} using defaults...");
                    config = Krb5Config.Default();
                    string realm;
                    try
                    {
                        realm = new KerberosPasswordCredential(options.ServiceAccount, options.Password).Domain;
                    }
                    catch (Exception)
                    {
                        return; // we're gonna handle this case during validation
                    }

                    options.Kdc ??= realm;
                    if (realm != null)
                    {
                        config.Defaults.DefaultRealm = realm;
                        config.Realms[realm].Kdc.Add(options.Kdc);
                        config.Realms[realm].DefaultDomain = realm.ToLower();
                        config.DomainRealm.Add(realm.ToLower(), realm.ToUpper());
                        config.DomainRealm.Add($".{realm.ToLower()}", realm.ToUpper());
                    }

                    config.Defaults.DefaultCCacheName = options.CacheFile;
                    config.Defaults.DefaultKeytabName = options.KeytabFile;
                    config.Defaults.DefaultClientKeytabName = options.KeytabFile;

                    config.Defaults.KdcTimeSync = 1;
                    config.Defaults.CCacheType = 4;
                    config.Defaults.Forwardable = true;
                    config.Defaults.Proxiable = true;
                    config.Defaults.RDNS = false;

                    config.Defaults.DefaultTgsEncTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA1_96);
                    config.Defaults.DefaultTgsEncTypes.Add(EncryptionType.AES128_CTS_HMAC_SHA1_96);
                    config.Defaults.DefaultTgsEncTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA384_192);
                    config.Defaults.DefaultTgsEncTypes.Add(EncryptionType.AES128_CTS_HMAC_SHA256_128);

                    config.Defaults.DefaultTicketEncTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA1_96);
                    config.Defaults.DefaultTicketEncTypes.Add(EncryptionType.AES128_CTS_HMAC_SHA1_96);
                    config.Defaults.DefaultTicketEncTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA384_192);
                    config.Defaults.DefaultTicketEncTypes.Add(EncryptionType.AES128_CTS_HMAC_SHA256_128);

                    config.Defaults.PreferredPreAuthTypes.Add(PaDataType.PA_SVR_REFERRAL_INFO);
                    config.Defaults.PreferredPreAuthTypes.Add(PaDataType.PA_ETYPE_INFO2);
                    config.Defaults.PreferredPreAuthTypes.Add(PaDataType.PA_PK_AS_REP);
                }
                else
                {
                    logger.LogInformation($"Existing {options.KeytabFile} was detected");
                    config = Krb5Config.Parse(File.ReadAllText(options.Kerb5ConfigFile!));
                }

                var client = new KerberosClient(config, loggerFactory);
                client.CacheInMemory = false;
                client.Cache = new Krb5TicketCache(options.CacheFile);
                client.RenewTickets = true;
                options.KerberosClient = client;
            });

        services.AddSingleton<IValidateOptions<KerberosOptions>, KerberosOptions.Validator>();
        services.AddSingleton<KerberosCredentialFactory>();
        services.AddHostedService<KerberosWorker>();
        services.AddFeatureManagement(configuration);
        return services;
    }

    internal static async Task LoadSalts(this KerberosCredential credential, CancellationToken cancellationToken)
    {
        if (credential.Configuration == null)
            throw new InvalidOperationException($"Can't load salts when {nameof(credential.Configuration)} is null");

        var asReqMessage = KrbAsReq.CreateAsReq(credential, AuthenticationOptions.Renewable);
        var asReq = asReqMessage.EncodeApplication();


        var transport = new KerberosTransportSelector(
            new IKerberosTransport[]
            {
                new TcpKerberosTransport(null),
                new UdpKerberosTransport(null),
                new HttpsKerberosTransport(null)
            },
            credential.Configuration,
            null
        )
        {
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };
        try
        {
            await transport.SendMessage<KrbAsRep>(credential.Domain, asReq, cancellationToken);
        }
        catch (KerberosProtocolException pex)
        {
            var paData = pex?.Error?.DecodePreAuthentication();
            if (paData != null)
            {
                credential.IncludePreAuthenticationHints(paData);
            }
        }
        return;
    }
}