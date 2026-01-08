// LittleHelper AI - Main Program Entry Point
// .NET 8 Web API with MySQL and Redis support

using LittleHelperAI.API.Middleware;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data;
using LittleHelperAI.Agents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "littlehelper-ai-secret-key-2024";
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// Register MySQL Database Context
var connectionString = builder.Configuration.GetConnectionString("MySQL") 
    ?? "Server=localhost;Database=littlehelper_ai;User=root;Password=;";
builder.Services.AddSingleton<IDbContext>(new MySqlDbContext(connectionString));

// Register Redis (optional)
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
}
else
{
    builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
}

// Register Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<LittleHelperAI.Agents.IAIService>(sp => sp.GetRequiredService<AIService>());
builder.Services.AddScoped<AIService>(); // Register concrete type for explicit resolution
builder.Services.AddScoped<IJobOrchestrationService, JobOrchestrationService>();

// Register Agents
builder.Services.AddScoped<IAgentRegistry, AgentRegistry>();
builder.Services.AddScoped<PlannerAgent>();
builder.Services.AddScoped<ResearcherAgent>();
builder.Services.AddScoped<DeveloperAgent>();
builder.Services.AddScoped<TestDesignerAgent>();
builder.Services.AddScoped<ExecutorAgent>();
builder.Services.AddScoped<DebuggerAgent>();
builder.Services.AddScoped<VerifierAgent>();
builder.Services.AddScoped<ErrorAnalyzerAgent>();

// Background Job Service (for multi-agent orchestration)
builder.Services.AddHostedService<JobWorkerService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Custom middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapControllers();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
    await db.InitializeAsync();
}

Console.WriteLine("LittleHelper AI API starting on port 8001...");
app.Run("http://0.0.0.0:8001");
