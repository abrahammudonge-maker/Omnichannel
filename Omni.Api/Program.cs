using Omni.Api.Extensions;
using Omni.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
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
