using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Tarefas.Api.Persistence;
using Tarefas.Api.Models;
using Tarefas.Api.Services;
using Tarefas.Api.Validation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<TarefaService>();

builder.Services.AddDbContext<TarefasDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Tarefas");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Configure ConnectionStrings:Tarefas com a conexão PostgreSQL. Consulte o README para usar .NET User Secrets.");
    options.UseNpgsql(connectionString);
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<TarefasDbContext>();

builder.Services.AddIdentityApiEndpoints<Usuario>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 4;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<TarefasDbContext>()
    .AddUserValidator<NomeUsuarioValidator>();

var corsOrigins = builder.Configuration["CORS_ORIGINS"]?
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TarefasDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGroup("/api/identity").MapIdentityApi<Usuario>();

app.MapPost("/api/auth/register", async (RegistrarUsuarioRequest request, UserManager<Usuario> userManager) =>
{
    var nome = request.Nome?.Trim();
    var email = request.Email?.Trim();
    if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100 || string.IsNullOrWhiteSpace(email))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["dados"] = ["Informe nome (até 100 caracteres) e e-mail."],
        });

    var usuario = new Usuario { UserName = email, Email = email, Nome = nome };
    var resultado = await userManager.CreateAsync(usuario, request.Senha ?? "");
    if (!resultado.Succeeded)
        return Results.ValidationProblem(resultado.Errors
            .GroupBy(erro => erro.Code)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(erro => erro.Description).ToArray()));

    return Results.Created("/api/auth/me", null);
}).AllowAnonymous();

app.MapGet("/api/auth/me", async (System.Security.Claims.ClaimsPrincipal principal,
    UserManager<Usuario> userManager) =>
{
    var usuario = await userManager.GetUserAsync(principal);
    return usuario is null
        ? Results.Unauthorized()
        : Results.Ok(new UsuarioResposta(usuario.Id, usuario.Nome, usuario.Email ?? ""));
}).RequireAuthorization();

app.MapControllers();

app.Run();

// Permite testes HTTP em memória com WebApplicationFactory.
public partial class Program { }

public record RegistrarUsuarioRequest(string? Nome, string? Email, string? Senha);
public record UsuarioResposta(string Id, string Nome, string Email);
