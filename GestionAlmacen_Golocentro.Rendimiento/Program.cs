using System.Globalization;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Rendimiento;

// Herramienta de medición del apartado 2.3. Cada paso crea su propio PostgreSQL desechable.
CultureInfo.DefaultThreadCurrentCulture = Cultura.Peru;
CultureInfo.CurrentCulture = Cultura.Peru;

var paso = args.FirstOrDefault() ?? "";
Console.WriteLine($"Golocentro · medición de rendimiento · paso '{paso}' · {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
Console.WriteLine($".NET {Environment.Version} · {Environment.ProcessorCount} núcleos · {Environment.OSVersion}");

await using var bd = await BaseDesechable.CrearAsync();
Console.WriteLine($"Base desechable: {await bd.VersionAsync()}");
Console.WriteLine();

switch (paso)
{
    case "inventario":
        await Inventario.EjecutarAsync(bd);
        break;
    case "auditoria":
        await AuditoriaN1.EjecutarAsync(bd);
        break;
    case "volumen":
        await VolumenPaso.EjecutarAsync(bd);
        break;
    case "medir":
        await Medicion.QuitarIndiceAsync(bd);
        Console.WriteLine($"## Volumen: {Volumen.Medicion}");
        Console.WriteLine(await Generador.GenerarAsync(bd, Volumen.Medicion));
        Console.WriteLine($"Huella md5 de los datos: {await VolumenPaso.HuellaAsync(bd)} (la misma del paso c)");
        Console.WriteLine();
        await Medicion.MedirAsync(bd, "sin cambios");
        break;
    case "indice":
        // Paso e: antes y después del índice, sobre los mismos datos y en la misma base
        await Medicion.QuitarIndiceAsync(bd);
        Console.WriteLine($"## Volumen: {Volumen.Medicion}");
        Console.WriteLine(await Generador.GenerarAsync(bd, Volumen.Medicion));
        Console.WriteLine($"Huella md5 de los datos: {await VolumenPaso.HuellaAsync(bd)} (la misma del paso c)");
        Console.WriteLine();
        var antes = await Medicion.MedirAsync(bd, "ANTES del índice");
        Console.WriteLine();
        Console.WriteLine($"## Se corre Database/{Medicion.ScriptIndice} ({DateTime.Now:HH:mm:ss})");
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine(await bd.CorrerScriptAsync(Medicion.ScriptIndice));
        Console.WriteLine($"El script tardó {reloj.Elapsed.TotalMilliseconds:N0} ms con estos datos.");
        Console.WriteLine();
        var despues = await Medicion.MedirAsync(bd, "DESPUÉS del índice", antes.Where(r => r.Explain != null).Select(r => r.Pantalla).ToHashSet());
        Console.WriteLine();
        Medicion.Comparar(antes, despues);
        break;
    default:
        Console.WriteLine("Uso: dotnet run -c Release -- inventario | auditoria | volumen | medir | indice");
        return 1;
}
Console.WriteLine();
Console.WriteLine($"Terminado {DateTime.Now:HH:mm:ss}. El contenedor se elimina al salir.");
return 0;
