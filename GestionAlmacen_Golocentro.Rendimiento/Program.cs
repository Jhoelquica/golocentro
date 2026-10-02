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
    default:
        Console.WriteLine("Uso: dotnet run -c Release -- inventario | auditoria | volumen");
        return 1;
}
Console.WriteLine();
Console.WriteLine($"Terminado {DateTime.Now:HH:mm:ss}. El contenedor se elimina al salir.");
return 0;
