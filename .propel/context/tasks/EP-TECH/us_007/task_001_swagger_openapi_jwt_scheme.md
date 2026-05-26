# Task 001: Swagger/OpenAPI with JWT Bearer Security Scheme

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-007 |
| **Epic** | EP-TECH |
| **Layer** | API |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | None |

## Objective

Enhance the existing `AddSwaggerGen()` stub (added in US-005/US-006) so that:

1. The generated OpenAPI spec carries full `OpenApiInfo` metadata.
2. Every controller endpoint and its request/response schemas appear in the
   spec (XML documentation included).
3. A JWT Bearer security definition is registered so the **Authorize** button
   appears in Swagger UI, allowing developers to supply a `Bearer <token>`
   header that flows into API calls made from the UI.
4. Swagger UI remains gated to the Development environment only.

`Swashbuckle.AspNetCore 10.1.7` is already present in
`HealthPlatform.Api.csproj`; no new package is needed.

## Acceptance Criteria Covered

- AC-1: Swagger UI accessible at `/swagger` in Development
- AC-2: OpenAPI spec includes all controller endpoints with request/response schemas
- AC-3: JWT Bearer authentication configured in Swagger UI (Authorize button)

## Implementation Steps

### 1. Enable XML Documentation in `HealthPlatform.Api.csproj`

Add the following inside the existing `<Project>` element (no `<PropertyGroup>`
tag exists yet — add one):

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

`NoWarn 1591` suppresses the "missing XML comment" warning for public members
not yet documented, keeping `TreatWarningsAsErrors` clean.

### 2. Add `using` Directives in `Program.cs`

```csharp
using Microsoft.OpenApi.Models;
```

### 3. Replace `AddSwaggerGen()` in `Program.cs`

Replace the existing one-liner:

```csharp
builder.Services.AddSwaggerGen();
```

With the full configuration:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "HealthPlatform API",
        Version     = "v1",
        Description = "RESTful API for the HealthPlatform scheduling and queue management system."
    });

    // Include XML doc comments in the spec
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // JWT Bearer security definition
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Description  = "Enter: Bearer {your JWT token}",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id   = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});
```

### 4. Update `UseSwaggerUI()` to Set Explicit Route Prefix

The existing code already gates Swagger to Development. No structural change is
needed for AC-1; however, explicitly set the URL prefix to confirm `/swagger`
resolves:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "HealthPlatform API v1"));
}
```

## Files Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/HealthPlatform.Api.csproj` | Add `<PropertyGroup>` with XML doc generation + NoWarn 1591 |
| `src/HealthPlatform.Api/Program.cs` | Replace `AddSwaggerGen()` with full config; update `UseSwaggerUI()` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
# Start the API and navigate to https://localhost:{port}/swagger
# Confirm: Authorize button present, all controller routes visible
```

## Notes

- `Microsoft.OpenApi.Models` is part of the `Microsoft.OpenApi` package pulled
  in transitively by `Swashbuckle.AspNetCore` — no extra NuGet needed.
- The security requirement is applied globally. Individual endpoints that do not
  require auth can opt out via `[AllowAnonymous]`.
- Task 002 provisions the actual JWT Bearer authentication middleware; this task
  only configures how Swagger represents the scheme.
