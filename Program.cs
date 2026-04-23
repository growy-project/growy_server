using growy_server.Data;
using growy_server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.WithOrigins(
                      "http://localhost:3000",
                      "https://momentum-scanner.com",
                      "https://gentle-stone-0ea32490f.7.azurestaticapps.net")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddDbContext<GrowyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<ISymbolService, SymbolService>();

builder.Services.AddSingleton<IStatisticsJobService, StatisticsJobService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddTransient<IEmailService, EmailService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowLocalhost");


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
