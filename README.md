# kerberos-auth-dotnet-extension

A simple library to add kerberos auth capabilities (mentioned below) for a dotnet app running in a non domain joined linux container. Most of the code is copied from Andrew Stackhov's [Kerberos Buildpack](https://github.com/macsux/kerberos-buildpack) repo, and thanks to Andrew for all the research and code he had done for this. 

### Features

1. Create kerberos configuration file
1. Create kerberos keytab file based on the given service account credentials
1. Obtain the ticket and stores in cache
1. Refreshes the ticket to keep it updated

> Important Note:  I just created this library for my personal use, but incase you need more info, you can always refer to the original code that Andrew has on his repo. You can also check [NMica.Security](https://github.com/NMica/NMica.Security) for a better library to take away the burden of authentication from the application as a gateway/proxy.

## Getting started

> This repo contains a working sample, for any quick reference.

### Building the library as a nuget package

1. Clone the repo down to your local

1. Install `git version` to compile the library, find installation instructions [here](https://gitversion.net/docs/usage/cli/installation)

1. Make sure you have the compatible .NET Core SDK versions installed

    [.NET Core SDK Version 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

1. Goto the folder and run either `.\build.bat` or `./build.sh` for the initial build.

1. To publish the package to your local, run either `.\build.bat DevPublish` or `./build.sh DevPublish` for the initial build. Fr publishing to other targets, you can refer to `default.ps1` file

1. Now you should be able to see your packages published to the target mentioned. 

### Using the library in your application

1. Add appropriate source configurations to your project's `nuget.config` file

1. Add the package reference to your application project file

    ```xml
    <ItemGroup>
        <PackageReference Include="Kerberos.Client.Manager" Version="1.0.0" />
    </ItemGroup>
    ```

1. Add the below code to the application's `startup/program.cs`, which will add the necessary services for handling kerberos client management

    ```c#
    using Microsoft.FeatureManagement;
    using Kerberos.Client.Manager;
    ...
    builder.Services.AddFeatureManagement(configuration);
    builder.Services.AddKerberosClientManagement(builder.Configuration);
    ```
1. Add the below code to the application's `startup/program.cs`, which remove swagger documentation of diagnostics controller, when environment variable `KRB_ENABLE_DIAGNOSTICS_ENDPOINTS` is set to false.

    ```c#
    using Kerberos.Client.Manager;
    ...
    builder.Services.AddSwaggerGen(options => {
        options.DocumentFilter<KerberosDiagnosticsDocumentFilter>();
    });
    ```

1. Below are the environment variables used by the kerberos manager. This needs to be set when running the app. For local use, you can setup in `launchsettings.json`

    ```json
    {
        "KRB_ENABLE_DIAGNOSTICS_ENDPOINTS": false, //whether diagnostics controller needs to be exposed (set false in prod environment)
        "KRB5_CONFIG": "/<user home directory in container>/.krb5/krb5.conf", //kerberos config file
        "KRB5CCNAME": "/<user home directory in container>/.krb5/krb5cc", //kerberos ticket cache file
        "KRB5_KTNAME": "/<user home directory in container>/.krb5/krb5.keytab", //kerberos keytab file
        "KRB5_CLIENT_KTNAME": "/<user home directory in container>/.krb5/krb5.keytab", //kerberos keytab file
        "KRB_PASSWORD": "", //service account password
        "KRB_SERVICE_ACCOUNT": "", //service account name e.g. sa1@MYDOMAINNAME.COM
        "KRB_KDC": "", //kdc domain name e.g. ADAUTH.MYDOMAINNAME.COM
        "APP_HOSTNAME": "" //application host name e.g. myapp.mydomain.com (for the case if app url is https://myapp.mydomain.com). This is mandatory only for windows ingress authentication
    }
    ```

1. Build app using [pack cli](https://github.com/buildpacks/pack/releases/tag/v0.27.0), command below. Below will produce an image with package `krb5-user` (MIT Kerberos).

    ```
    pack build <docker image name> --tag <docker image name>:<docker image tag> --buildpack paketo-buildpacks/dotnet-core --builder paketobuildpacks/builder:full --env BP_DOTNET_PROJECT_PATH=<dotnet application project path> --env BP_DOTNET_PUBLISH_FLAGS="--verbosity=normal --self-contained=true"
    ```

### Running your application

1. To run the application using [docker](https://www.docker.com/products/docker-desktop/), use the below command. Add `--env KRB5_TRACE=/dev/stdout --env KRB_ENABLE_DIAGNOSTICS_ENDPOINTS=true` for kerberos tracing and diagnostics in lower environment.

    ```bash
    docker run --env ASPNETCORE_ENVIRONMENT=Development --env KRB5_CONFIG=/<user home directory in container>/krb5.conf --env KRB5CCNAME=/<user home directory in container>/krb5cc --env KRB5_KTNAME=/<user home directory in container>/krb5.keytab --env KRB5_CLIENT_KTNAME=/<user home directory in container>/krb5.keytab  --env KRB_SERVICE_ACCOUNT=sa1@MYDOMAINNAME.COM --env KRB_PASSWORD=P@ssw0rd_ --env KRB_KDC=ADAUTH.MYDOMAINNAME.COM -p 8085:8080 <docker image name>:<docker image tag>
    ```

## Setup required for MSSQL Server Windows Integrated Auth

1. SQL Server is running under AD principal
1. SQL server principal account has SPN assigned in for of MSSQLSvc/<fully qualified domain name> 
1. SQL Server is configured to use SSL (required by kerberos authentication). If using cert that is not trusted on client, append `TrustServerCertificate=True` to connection string

## Setup required for Ingress Windows Integrated Auth (browser based applications)

1. Identify the service account for which the application should be running under (imagine as your application running in IIS on an APP POOL, under a service account). It is the one you setup earlier via environment variable `KRB_SERVICE_ACCOUNT`. 

1. The `<app host name >` below is the one you setup earlier via environment variable `APP_HOSTNAME`, which is a mandatory configuration variable

1. Execute the below command to create the spn

```bash
SetSpn -S http/<app host name> <domain\service_account_name>
```
1. To check to see which SPNs are currently registered with your service account, run the following command:

```bash
SetSpn -L <domain\service_account_name>
```

1. In your browser settings or internet options, add the site as `fully trusted`, where the application should be exposing url with `https` scheme.

> For any other errors, follow the kerberos diagnostics trace and fix the issues as needed.

> Alternatively, a simple sidecar container using this library or using [Andrew's sidecar](https://github.com/macsux/kerberos-buildpack/tree/main/src/KerberosSidecar) which can share the kerberos files via a mounted volume will work. The app can also be hosted as a seperate service which can share the kerberos files via a namespace level shared volume.

## Other things that I had to do in my labs environment

1. Modified the service account to accept multiple encryption (AES128 and AES256) following the [article](https://learn.microsoft.com/en-us/archive/blogs/openspecification/windows-configurations-for-kerberos-supported-encryption-type) section `2. Computer Account Encryption Type Setting`. This solved `kerberos encryption not supported` error while trying to pre-auth.
