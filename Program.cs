using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirAstro", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. CONFIGURACIÓN DE LA BASE DE DATOS
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();
app.UseCors("PermitirAstro");

// --- AUTO-MIGRACIÓN PARA RENDER ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}
// ----------------------------------

// ==============================================================================
// 3. ENDPOINTS
// ==============================================================================
app.MapGet("/api/productos", async (AppDbContext db) =>
{
    var productos = await db.Productos.ToListAsync();
    return Results.Ok(productos);
});

app.MapPost("/api/scrape", async (AppDbContext db) =>
{
    string url = "https://www.pcel.com/laptops/HP-AJ1X0ATCUSTOM16512-Laptop-HP-255-G10-Procesador-AMD-Ryzen-5-7530U-hasta-4-5-GHz-Memoria-de-16GB-DDR4-SSD-de-512GB-Pantalla-de-15-6-LED-Video-Radeon-Graphics-S-O-Window-542792"; 

    using HttpClient client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

    try
    {
        string html = await client.GetStringAsync(url);
        HtmlDocument document = new HtmlDocument();
        document.LoadHtml(html);

        var titleNode = document.DocumentNode.SelectSingleNode("/html/body/div[2]/div[2]/div[2]/div/div[1]/div/div/h3");
        string nombreExtraido = titleNode != null 
            ? titleNode.InnerText.Replace("\r", "").Replace("\n", " ").Trim() 
            : "Producto Desconocido";

        var priceNode = document.DocumentNode.SelectSingleNode("/html/body/div[2]/div[2]/div[2]/div/div[4]/div[2]/div/div/div[1]/div[1]/strong"); 
        string precioExtraido = "Precio no encontrado";
        if (priceNode != null)
        {
            precioExtraido = priceNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
        }

        var nuevoProducto = new Producto 
        { 
            Nombre = nombreExtraido, 
            Precio = precioExtraido, 
            FechaConsulta = DateTime.UtcNow 
        };

        db.Productos.Add(nuevoProducto);
        await db.SaveChangesAsync();

        return Results.Ok(new { mensaje = "Scraping exitoso y guardado en DB", producto = nuevoProducto });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al hacer scraping: {ex.Message}");
    }
});

app.Run();

// ==============================================================================
// 4. MODELOS DE DATOS
// ==============================================================================
class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Precio { get; set; } = string.Empty;
    public DateTime FechaConsulta { get; set; }
}

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Producto> Productos => Set<Producto>();
}