using PivotalServices.Kerberos.Client.Manager;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFeatureManagement(builder.Configuration);
// To enable kerberos based authentication
builder.Services.AddKerberosClientManagement(builder.Configuration);

// Ingress Auth - Negotiate
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options => {
    // Helps to enable or disable diagnostic endpoints
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
