// Original file https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar

namespace PivotalServices.Kerberos.Client.Manager;

public class SqlServerInfo
{
    public string ConnectionString { get; set; }
    public string Server { get; set; }
    public string Database { get; set; }
    public string Version { get; set; }
}