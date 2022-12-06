$ErrorActionPreference = 'Stop'

if($args.Length -eq 0 || $args[0] -eq '')
{
    throw "pass argument as the image version suffix"
}

$versionSuffix=$args[0]
$registry = 'index.docker.io/ajaganathan'
$imageName = 'kerberos-auth-dotnet-app-img'
$imageTag = "1.0.0-$versionSuffix"

$fullImageName = "$($imageName):$($imageTag)"
$fullImageTag = "$registry/$fullImageName"

$serviceAccountName="sa1@MYDOMAINNAME.COM"
$serviceAccountPassword=""
$kdc="ADAUTH.MYDOMAINNAME.COM"

docker run --env ASPNETCORE_ENVIRONMENT=Development --env KRB5_CONFIG=/workspace/krb5.conf --env KRB5CCNAME=/workspace/krb5cc --env KRB5_KTNAME=/workspace/krb5.keytab --env KRB5_CLIENT_KTNAME=/workspace/krb5.keytab --env KRB5_TRACE=/dev/stdout --env KRB_SERVICE_ACCOUNT=$serviceAccountName --env KRB_PASSWORD=$serviceAccountPassword --env KRB_KDC=$kdc --env KRB_ENABLE_DIAGNOSTICS_ENDPOINTS=true -p 8085:8080 $fullImageTag