// Original file https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar

using System.Text.RegularExpressions;
using Kerberos.NET.Client;
using Microsoft.Extensions.Options;

namespace PivotalServices.Kerberos.Client.Manager;

public class KerberosOptions
{
    /// <summary>
    /// Gets or sets the location of MIT Kerberos Kerb5Config file
    /// </summary>
    public string Kerb5ConfigFile { get; set; } = null!;

    /// <summary>
    /// Gets or sets the location of MIT Kerberos ticket cache file
    /// </summary>
    public string CacheFile { get; set; } = null!;

    /// <summary>
    /// Gets or sets the location of MIT Kerberos keytab file
    /// </summary>
    public string KeytabFile { get; set; } = null!;
    
    /// <summary>
    /// Location of Kerberos Key Distribution Center (usually AD domain controller)
    /// </summary>
    public string Kdc { get; set; }

    /// <summary>
    /// Gets or sets the service account under which the application runs
    /// </summary>
    public string ServiceAccount { get; set; } = null!;

    /// <summary>
    /// Gets or sets the password for application service account
    /// </summary>
    public string Password { get; set; } = null!;

    /// <summary>
    /// Gets or sets the application hostname (fully qualified domain, e.g. myapp.mydomain.com if apps url is https://myapp.mydomain.com) of application
    /// </summary>
    public string ApplicationHostName { get; set; } = null!;

    public KerberosClient KerberosClient { get; set; } = null!;

    public bool RunOnce { get; set; }
    public bool GenerateKrb5 { get; set; }

    public class Validator : IValidateOptions<KerberosOptions>
    {
        public ValidateOptionsResult Validate(string name, KerberosOptions options)
        {
            var errors = new List<string>();
            if (options.Kerb5ConfigFile == null)
            {
                errors.Add("Kerberos config file not set. Use KRB5_CONFIG environmental variable to configure");
            }
            else if (!CanWrite(options.Kerb5ConfigFile))
            {
                errors.Add($"Cannot open {options.Kerb5ConfigFile} for writing");
            }
            
            if (options.CacheFile == null)
            {
                errors.Add("Kerberos ticket cache file not set. Use KRB5CCNAME environmental variable to configure");
            }
            else if (!CanWrite(options.CacheFile))
            {
                errors.Add($"Cannot open {options.CacheFile} for writing");
            }
            
            if (options.KeytabFile == null)
            {
                errors.Add("Kerberos ticket cache file not set. Use KRB5CCNAME environmental variable to configure");
            }
            else if (!CanWrite(options.KeytabFile))
            {
                errors.Add($"Cannot open {options.KeytabFile} for writing");
            }

            if (options.ServiceAccount == null)
            {
                errors.Add("Service account is not configured. Use KRB_SERVICE_ACCOUNT environmental variable to configure");
            }
            else if(!Regex.IsMatch(options.ServiceAccount, "^.+?@.+$"))
            {
                errors.Add("Service account is not in email format");
            }
            if (options.Password == null)
            {
                errors.Add("Service password is not set. Use KRB_PASSWORD environmental variable to configure");
            }
            if (options.Kdc == null && options.GenerateKrb5)
            {
                errors.Add("KDC is not configured");
            }

            return errors.Any() ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
        }

        private bool CanWrite(string path)
        {
            try
            {
                File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None).Dispose();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
    
}