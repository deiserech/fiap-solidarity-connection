using Azure.Messaging.ServiceBus;
using SolidarityConnection.Api.BackgroundServices;
using SolidarityConnection.Api.Extensions;
using SolidarityConnection.Api.Middlewares;
using SolidarityConnection.Application.Interfaces.Publishers;
using SolidarityConnection.Application.Publishers;
using SolidarityConnection.Application.Interfaces.Services;
using SolidarityConnection.Application.Services;
using SolidarityConnection.Domain.Interfaces.Repositories;
using SolidarityConnection.Infrastructure.Data;
using SolidarityConnection.Infrastructure.Repositories;
using SolidarityConnection.Infrastructure.ServiceBus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

builder.Services.AddSwagger();

builder.Services.AddOpenTel(configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);

var dbConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "";  
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(dbConnectionString));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IDonationRequestedEventPublisher, DonationRequestedEventPublisher>();

var sbConnectionString = configuration["ServiceBus:ConnectionString"] ?? "";

builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));
builder.Services.AddSingleton<IServiceBusClientWrapper, ServiceBusClientWrapper>();
builder.Services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();

builder.Services.AddHostedService<DonationProcessedConsumer>();

var app = builder.Build();

await app.EnsureDatabaseAndMigrationsAsync(dbConnectionString);

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TracingEnrichmentMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

