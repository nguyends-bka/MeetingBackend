using System.Text;
using MeetingBackend.Data;
using MeetingBackend.Models;
using MeetingBackend.Services;
using MeetingBackend.Policies;
using MeetingBackend.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;



var builder = WebApplication.CreateBuilder(args);

// =======================
// Controllers
// =======================
builder.Services.AddControllers();

// =======================
// Swagger
// =======================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token: Bearer {token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// =======================
// Database (PostgreSQL)
// =======================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// =======================
// LiveKit
// =======================
builder.Services.Configure<LiveKitOptions>(
    builder.Configuration.GetSection("LiveKit"));

builder.Services.AddSingleton<LiveKitTokenService>();

// =======================
// Meeting Code Service
// =======================
builder.Services.AddScoped<MeetingCodeService>();

// =======================
// JWT Auth
// =======================
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

// =======================
// Authorization Policies (Dynamic, database-driven)
// =======================
builder.Services.AddAuthorization(options =>
{
    AuthorizationPolicies.ConfigurePolicies(options);
});

// Register authorization handlers for dynamic role checking
builder.Services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, MeetingHostAuthorizationHandler>();

// =======================
// CORS (Frontend Next.js)
// =======================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// =======================
// Middleware
// =======================
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS redirection - chỉ bật trong production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

// WebSocket for virtual mic (PCM16) - must be before UseAuthentication for optional token-in-query
app.UseWebSockets();
app.UseMiddleware<VirtualMicWebSocketMiddleware>();

app.UseAuthentication(); // 🔑 PHẢI TRƯỚC Authorization
app.UseAuthorization();

app.MapControllers();

app.Run();
