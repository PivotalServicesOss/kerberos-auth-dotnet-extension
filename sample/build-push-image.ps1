$ErrorActionPreference = 'Stop'

if($args.Length -eq 0 || $args[0] -eq '')
{
    throw "pass argument as the version suffix"
}

$applicationPath = './Kerberos.Dotnet.Application'

$versionSuffix=$args[0]
$registry = 'index.docker.io/ajaganathan'
$imageName = 'kerberos-auth-dotnet-app-img'
$imageTag = "1.0.0-$versionSuffix"

$fullImageName = "$($imageName):$($imageTag)"
$fullImageTag = "$registry/$fullImageName"

pack build $imageName --tag $fullImageTag `
                      --buildpack paketo-buildpacks/dotnet-core `
                      --builder paketobuildpacks/builder:full `
                      --env BP_DOTNET_PROJECT_PATH=$applicationPath `
                      --env BP_DOTNET_PUBLISH_FLAGS="--verbosity=normal --self-contained=true" `
                      --volume $PWD/bindings:/platform/bindings `
                      --volume /Users/ajaganathan/.nugetrepo:/platform/nugetrepo:ro

docker push $fullImageTag