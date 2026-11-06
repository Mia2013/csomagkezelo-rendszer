using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Csomagkuldo.OpenApi
{
    public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;
        public BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
        {
            _authenticationSchemeProvider = authenticationSchemeProvider;
        }

        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var schemes = await _authenticationSchemeProvider.GetAllSchemesAsync();
            if (!schemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme)) return;

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
            });
        }
    }
}
