// Original copied from https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar

namespace Kerberos.Client.Manager;

public class SqlServerBindingInfo
{
    public string Type { get; set; }
    public string ConnectionString { get; set; }
}