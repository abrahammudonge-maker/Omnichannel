using Omni.Api.Extensions;
using Omni.Api.Hubs;
using Omni.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();
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
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new { message = "Omnichannel API is running.", status = "ok" }));
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<VoiceHub>("/hubs/voice");

app.Run();
