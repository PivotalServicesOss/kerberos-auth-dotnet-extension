using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.FeatureManagement.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kerberos.Client.Manager;

public class KerberosDiagnosticsDocumentFilter : IDocumentFilter
{
    private readonly IConfiguration configuration;

    public KerberosDiagnosticsDocumentFilter(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (context == null)
            return;

        IEnumerable<object> controllerAttributes = Array.Empty<object>();

        foreach (ApiDescription description in context.ApiDescriptions)
        {
            description.TryGetMethodInfo(out var methodInfo);

            if (methodInfo != null && methodInfo.DeclaringType != null)
                controllerAttributes = methodInfo.DeclaringType.GetCustomAttributes(true);

            var featureFlagAttribute = controllerAttributes.OfType<FeatureGateAttribute>().SingleOrDefault();

            if (featureFlagAttribute != null)
            {
                var isDiagnosticsEnabled = Convert.ToBoolean(configuration[FeatureFlags.EnableKerberosDiagnostics] ?? "false");
                if (!isDiagnosticsEnabled)
                {
                    var controllerName = ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)description.ActionDescriptor).ControllerName;
                    var path = description.RelativePath;
                    var routesToRemove = swaggerDoc.Paths.Where(
                        x => x.Key.ToLower().Contains(path.ToLower()) &&
                        x.Value.Operations[0].Tags[0].Name == controllerName).ToList();
                    routesToRemove.ForEach(x => { swaggerDoc.Paths.Remove(x.Key);});
                }
            }
        }
    }
}