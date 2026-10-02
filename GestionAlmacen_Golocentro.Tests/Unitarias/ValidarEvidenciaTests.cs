using GestionAlmacen_Golocentro.Services;
using Microsoft.AspNetCore.Http;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Foto de evidencia de entradas y ventas: solo imágenes de hasta 5 MB (se sirven desde wwwroot)
public class ValidarEvidenciaTests
{
    private const long MB = 1024 * 1024;

    private static IFormFile Archivo(string nombre, string tipo, long bytes) =>
        new FormFile(Stream.Null, 0, bytes, "Evidencia", nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = tipo
        };

    [Fact]
    public void Sin_foto_no_hay_error_porque_es_opcional()
    {
        Assert.Null(OperacionesAlmacen.ValidarEvidencia(null));
    }

    [Fact]
    public void Archivo_vacio_se_toma_como_sin_foto()
    {
        Assert.Null(OperacionesAlmacen.ValidarEvidencia(Archivo("foto.jpg", "image/jpeg", 0)));
    }

    [Theory]
    [InlineData("entrega.jpg", "image/jpeg")]
    [InlineData("entrega.jpeg", "image/jpeg")]
    [InlineData("entrega.png", "image/png")]
    [InlineData("entrega.webp", "image/webp")]
    [InlineData("ENTREGA.JPG", "image/jpeg")]
    public void Acepta_fotos_jpg_png_y_webp(string nombre, string tipo)
    {
        Assert.Null(OperacionesAlmacen.ValidarEvidencia(Archivo(nombre, tipo, 800 * 1024)));
    }

    [Theory]
    [InlineData("animacion.gif", "image/gif")]
    [InlineData("pagina.html", "text/html")]
    [InlineData("programa.exe", "application/octet-stream")]
    [InlineData("documento.pdf", "application/pdf")]
    public void Rechaza_lo_que_no_es_foto(string nombre, string tipo)
    {
        Assert.Equal("La foto debe ser una imagen JPG, PNG o WEBP.",
            OperacionesAlmacen.ValidarEvidencia(Archivo(nombre, tipo, 10 * 1024)));
    }

    [Fact]
    public void Rechaza_una_pagina_disfrazada_con_extension_de_imagen()
    {
        Assert.NotNull(OperacionesAlmacen.ValidarEvidencia(Archivo("foto.jpg", "text/html", 10 * 1024)));
    }

    [Fact]
    public void Rechaza_una_pagina_disfrazada_con_tipo_de_imagen()
    {
        Assert.NotNull(OperacionesAlmacen.ValidarEvidencia(Archivo("foto.html", "image/png", 10 * 1024)));
    }

    [Fact]
    public void Cinco_MB_justos_se_aceptan()
    {
        Assert.Null(OperacionesAlmacen.ValidarEvidencia(Archivo("foto.jpg", "image/jpeg", 5 * MB)));
    }

    [Fact]
    public void Mas_de_cinco_MB_se_rechaza()
    {
        Assert.Equal("La foto no debe pasar de 5 MB.",
            OperacionesAlmacen.ValidarEvidencia(Archivo("foto.jpg", "image/jpeg", 5 * MB + 1)));
    }
}
