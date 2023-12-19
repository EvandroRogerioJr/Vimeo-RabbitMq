using Microsoft.AspNetCore.Http.Features;
using VimeoUpdate.Domain;
using VimeoUpdate.Interface;
using VimeoUpdate.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMvc(); // ou AddMvc(), dependendo do que você precisa


builder.Services.Configure<RabbitMQConfiguration>(builder.Configuration.GetSection("RabbitMqConfig"));
builder.Services.AddHostedService<VimeoQueueConsumer>();
builder.Services.AddTransient<IRabbitMQMessageRepository, RabbitMQMessageRepository>();

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1073741824; // 1 GB
    options.ValueCountLimit = int.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.MultipartHeadersCountLimit = int.MaxValue;
    options.MultipartBoundaryLengthLimit = int.MaxValue;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 1073741824; // 1 GB
    options.MaxRequestBodyBufferSize = 1073741824; // Opcional: ajuste conforme necessário
    options.AllowSynchronousIO = true; // Opcional: permite operações de IO síncronas.
});
// ...

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Remova a linha abaixo se você escolheu usar AddRazorPages() acima.

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.MapRazorPages();

app.Run();
