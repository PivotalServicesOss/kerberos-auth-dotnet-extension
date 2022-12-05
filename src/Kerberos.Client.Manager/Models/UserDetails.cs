// Original copied from https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar

namespace Kerberos.Client.Manager;

public class UserDetails
{
    public string Name { get; set; }
    public List<ClaimSummary> Claims { get; set; }
}