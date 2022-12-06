using Kerberos.Client.Manager;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Steeltoe.Common;

var builder = WebApplication.CreateBuilder(args);

if (Platform.IsLinux || Platform.IsOSX)
    builder.Services.AddKerberosClientManagement(builder.Configuration);

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.DocumentFilter<KerberosDiagnosticsDocumentFilter>();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options => {
    options.RoutePrefix = string.Empty;
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Kerberos.Dotnet.Application");
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
