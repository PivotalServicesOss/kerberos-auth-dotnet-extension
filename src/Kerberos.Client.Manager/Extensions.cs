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
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var userKerbDir = Path.Combine(homeDir, ".krb5");

                // default files to user's ~/.krb/ folder if not set
                options.Kerb5ConfigFile ??= Path.Combine(userKerbDir, "krb5.conf");
                options.KeytabFile ??= Path.Combine(userKerbDir, "krb5.keytab");
                options.CacheFile ??= Path.Combine(userKerbDir, "krb5cc");
                options.GenerateKrb5 = options.Kerb5ConfigFile == null! || !File.Exists(options.Kerb5ConfigFile);

                Directory.CreateDirectory(Path.GetDirectoryName(options.Kerb5ConfigFile)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.KeytabFile)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.CacheFile)!);

                Krb5Config config;
                if (options.GenerateKrb5)
                {
                    logger.LogInformation("No krb5.conf exists - generating");
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
                    // config.Defaults.DnsLookupKdc = true;
                    // AddEncryptionTypes(config.Defaults.DefaultTgsEncTypes);
                    // AddEncryptionTypes(config.Defaults.DefaultTicketEncTypes);
                }
                else
                {
                    logger.LogInformation("Existing krb5.conf was detected");
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

    private static void AddEncryptionTypes(ICollection<EncryptionType> encryptionTypes)
    {
        encryptionTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA1_96_PLAIN);
        encryptionTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA1_96);
        encryptionTypes.Add(EncryptionType.AES256_CTS_HMAC_SHA384_192);
        encryptionTypes.Add(EncryptionType.DES_CBC_MD5);
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