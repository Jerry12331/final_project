using GKR_Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 設定 JSON 序列化為 PascalCase (保持 C# 原本大小寫)
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AskStepService 內部維護記憶體快取與限流狀態，必須註冊成 Singleton 才能跨請求共用
builder.Services.AddHttpClient("anthropic");
builder.Services.AddSingleton<AskStepService>();

// --- �s�W CORS �]�w ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- �ҥ� CORS ---
app.UseCors("AllowAll");

// app.UseHttpsRedirection(); // 註解掉以解決 HTTPS 重定向問題
app.UseAuthorization();
app.MapControllers();
app.Run();