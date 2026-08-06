using Omni.Api.Extensions;
using Omni.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
app.UseCors(policy =>
{
    if (allowedOrigins is { Length: > 0 })
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else
    {
        // No explicit allow-list configured (local dev default) — open to any origin.
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
});

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new { message = "Omnichannel API is running.", status = "ok" }));
app.MapControllers();

app.Run();
