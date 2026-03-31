using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE CORS
// Esto le dice a tu API que acepte peticiones desde tu portafolio (Astro)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirAstro", policy =>
    {
        policy.AllowAnyOrigin() // En producción, aquí pondrás la URL exacta de tu portafolio
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. CONFIGURACIÓN DE LA BASE DE DATOS (PostgreSQL)
// Por ahora le pasamos una cadena de conexión vacía, la configuraremos en el siguiente paso
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Activar CORS
app.UseCors("PermitirAstro");

// 3. ENDPOINTS (Tus rutas de la API)

// Endpoint GET: Astro llamará a esta ruta para mostrar la tabla de datos
app.MapGet("/api/productos", async (AppDbContext db) =>
{
    var productos = await db.Productos.ToListAsync();
    return Results.Ok(productos);
});

// Endpoint POST: Un botón en Astro llamará a esta ruta para ejecutar el scraper
app.MapPost("/api/scrape", async (AppDbContext db) =>
{
    string url = "https://pcel.com/"; // Reemplazar con el producto real
    
    using HttpClient client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

    try
    {
        string html = await client.GetStringAsync(url);
        HtmlDocument document = new HtmlDocument();
        document.LoadHtml(html);

        // Lógica de scraping (aquí irán tus XPath reales)
        var titleNode = document.DocumentNode.SelectSingleNode("//title");
        string nombreExtraido = titleNode != null ? titleNode.InnerText.Trim() : "Producto Desconocido";

        // Crear el objeto y guardarlo en la base de datos
        var nuevoProducto = new Producto 
        { 
            Nombre = nombreExtraido, 
            Precio = "$0.00", // Falta extraer el precio
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

// 4. MODELOS DE DATOS
// Esta es la estructura de tu tabla en la base de datos
class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Precio { get; set; } = string.Empty;
    public DateTime FechaConsulta { get; set; }
}

// El contexto que maneja la comunicación con PostgreSQL
class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Producto> Productos => Set<Producto>();
}