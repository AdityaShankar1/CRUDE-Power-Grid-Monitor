using Microsoft.EntityFrameworkCore;
using Crude.Infrastructure.Persistence;
using Crude.Core.Interfaces;
using Crude.Core.Services;
using Crude.Infrastructure.Data;
using Crude.Core.Entities; // Ensure this is here for EnergyAsset
using Crude.Api;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Services to the Container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Logic & Background Services
builder.Services.AddSingleton<IEvaluationStrategy, OverConsumptionStrategy>();
builder.Services.AddSingleton<EvaluationEngine>();
builder.Services.AddSingleton<RandomEnergyProvider>();
builder.Services.AddHostedService<EnergyMonitoringWorker>();

// 4. RELAXED CORS Policy for Development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin() 
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

// 5. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Order matters: CORS must be before MapControllers
app.UseCors("AllowReactApp");

// Authorization
app.UseAuthorization();

app.MapControllers();

// 6. AUTOMATIC DATABASE SEEDING
// This block ensures the database is created and has initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        
        // Ensure the database is created (replaces 'dotnet ef database update' for now)
        context.Database.EnsureCreated();

        if (!context.Assets.Any())
        {
            context.Assets.AddRange(
                new EnergyAsset 
                { 
                    Id = Guid.NewGuid(), 
                    Name = "Main Transformer", 
                    CurrentReading = 400, 
                    MaxConsumptionThreshold = 800, 
                    Status = NodeStatus.Healthy, 
                    LastUpdated = DateTime.UtcNow 
                },
                new EnergyAsset 
                { 
                    Id = Guid.NewGuid(), 
                    Name = "Cooling Array", 
                    CurrentReading = 120, 
                    MaxConsumptionThreshold = 150, 
                    Status = NodeStatus.Healthy, 
                    LastUpdated = DateTime.UtcNow 
                },
                new EnergyAsset 
                { 
                    Id = Guid.NewGuid(), 
                    Name = "Heavy Drill", 
                    CurrentReading = 850, 
                    MaxConsumptionThreshold = 800, 
                    Status = NodeStatus.Flagged, 
                    LastUpdated = DateTime.UtcNow 
                }
            );
            context.SaveChanges();
            Console.WriteLine("--> Database seeded with 3 assets.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> Error during seeding: {ex.Message}");
    }
}

app.Run();