using System.Net.Sockets;
using MundoVoxel.Core;

// Prueba automatica del servidor y el protocolo multijugador:
// arranca un GameServer en memoria, se conecta un cliente simulado y se verifican
// crear mundo, unirse, clave privada, romper/colocar bloques, persistencia en memoria y borrado.

int puerto = 25600;
var servidor = new GameServer(puerto, "Servidor de prueba");
servidor.Iniciar();
await Task.Delay(300);

int errores = 0;
void Comprobar(bool ok, string desc)
{
    Console.WriteLine((ok ? "  [OK]   " : "  [FALLA] ") + desc);
    if (!ok) errores++;
}

// ---------- cliente 1 ----------
Console.WriteLine("Cliente 1: se conecta y crea un mundo publico.");
var c1 = await Conectar(puerto);
await c1.Enviar(new Hola { Nombre = "Ana", Version = "1.0" });

var bienvenido = await c1.LeerHasta<Bienvenido>();
Comprobar(bienvenido != null, "recibe Bienvenido");
var lista0 = await c1.LeerHasta<ListaMundos>();
Comprobar(lista0?.Mundos.Count == 0, "lista de mundos vacia al inicio");

await c1.Enviar(new CrearMundo { Nombre = "Mundo de Ana", Abierto = true });
var creado = await c1.LeerHasta<MundoCreado>();
Comprobar(creado != null, "mundo creado");
string idMundo = creado!.Id;
var unido = await c1.LeerHasta<Unido>();
Comprobar(unido != null && unido.MundoComprimido.Length > 0, "recibe el mundo comprimido");
var mundo = Mundo.Deserializar(Mundo.Descomprimir(unido!.MundoComprimido));
Comprobar(mundo.Ancho == 64 && mundo.Alto == 40, $"dimensiones del mundo ({mundo.Ancho}x{mundo.Alto}x{mundo.Profundo})");
var aparicion = mundo.ObtenerPuntoAparicion();
Comprobar(mundo.Obtener((int)aparicion.X, (int)aparicion.Y, (int)aparicion.Z) == Bloques.Aire, "punto de aparicion despejado");

// ---------- cliente 2: mundo privado ----------
Console.WriteLine("Cliente 2: mundo privado, clave correcta e incorrecta.");
var c2 = await Conectar(puerto);
await c2.Enviar(new Hola { Nombre = "Bruno", Version = "1.0" });
await c2.LeerHasta<Bienvenido>();
await c2.LeerHasta<ListaMundos>();

await c2.Enviar(new CrearMundo { Nombre = "Solo Bruno", Abierto = false, Pin = "1234" });
await c2.LeerHasta<MundoCreado>();
var unido2 = await c2.LeerHasta<Unido>();
Comprobar(unido2 != null, "Bruno entra a su mundo privado");
string idPrivado = unido2!.Id;

// Ana intenta entrar al mundo privado con clave incorrecta
await c1.Enviar(new Unirse { Id = idPrivado, Pin = "9999" });
var errPin = await c1.LeerHasta<ErrorServidor>();
Comprobar(errPin?.Codigo == "PIN_INCORRECTO", "clave incorrecta rechazada");

// con la clave correcta si entra
await c1.Enviar(new Unirse { Id = idPrivado, Pin = "1234" });
var unidoPriv = await c1.LeerHasta<Unido>();
Comprobar(unidoPriv?.Id == idPrivado, "Ana entra con la clave correcta");

// ---------- mobs ----------
Console.WriteLine("Mobs: el servidor genera y difunde mobs en el mundo.");
var mobs = await c1.LeerHasta<Mobs>(timeoutMs: 3000);
Comprobar(mobs != null && mobs.Lista.Count > 0, $"mobs difundidos ({mobs?.Lista.Count ?? 0})");
Comprobar(mobs != null && mobs.Lista.All(m => m.Px >= 0 && m.Px < 64 && m.Pz >= 0 && m.Pz < 64 && m.Py >= 1), "posiciones de mobs dentro del mundo");
Comprobar(mobs != null && mobs.Lista.Select(m => m.Tipo).Distinct().Count() >= 3, "hay variedad de tipos de mob");

// ---------- romper y colocar bloques ----------
Console.WriteLine("Bloques: romper y colocar con difusion.");
var aparicionPriv = unidoPriv!;
int bx = (int)aparicionPriv.Ax, by = (int)aparicionPriv.Ay - 1, bz = (int)aparicionPriv.Az;
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var cambio = await c1.LeerHasta<BloqueCambio>();
Comprobar(cambio != null && cambio.Bloque == Bloques.Aire && cambio.X == bx, "romper bloque difunde BloqueCambio");

await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Ladrillo });
cambio = await c1.LeerHasta<BloqueCambio>();
Comprobar(cambio?.Bloque == Bloques.Ladrillo, "colocar bloque difunde BloqueCambio");

await c1.Enviar(new RomperBloque { X = 999, Y = 999, Z = 999 });
var cambioInvalido = await c1.LeerHasta<BloqueCambio>(timeoutMs: 600);
Comprobar(cambioInvalido == null, "romper fuera del mundo se ignora");

// ---------- inventario, crafteo, cocina y drops ----------
Console.WriteLine("Mecanicas: inventario, crafteo, cocina y drops de mobs.");

// Romper el ladrillo colocado: el servidor envia Inventario ANTES que BloqueCambio
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invLadrillo = await c1.LeerHasta<Inventario>();
await c1.LeerHasta<BloqueCambio>();
Comprobar(invLadrillo?.Slots.Any(s => s.Material == Bloques.Ladrillo && s.Cantidad >= 1) == true, "romper bloque lo mete al inventario");

// Conseguir 3 madera (colocar y romper troncos)
for (int i = 0; i < 3; i++)
{
    await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Madera });
    await c1.LeerHasta<BloqueCambio>();
    await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
    await c1.LeerHasta<Inventario>();
    await c1.LeerHasta<BloqueCambio>();
}

// 3 x madera -> 12 tablones (receta 0)
Inventario? invTablones = null;
for (int i = 0; i < 3; i++) { await c1.Enviar(new Craftear { Receta = 0 }); invTablones = await c1.LeerHasta<Inventario>(); }
Comprobar(invTablones?.Slots.Any(s => s.Material == Bloques.Tablones && s.Cantidad >= 12) == true, "craftear madera -> tablones (3x4=12)");

// Palos (receta 1): 2 tablones -> 4 palos
await c1.Enviar(new Craftear { Receta = 1 });
var invPalos = await c1.LeerHasta<Inventario>();
Comprobar(invPalos?.Slots.Any(s => s.Material == (ushort)ItemId.Palo && s.Cantidad >= 4) == true, "craftear 2 tablones -> 4 palos");

// Mesa de trabajo (receta 2): 4 tablones -> mesa
await c1.Enviar(new Craftear { Receta = 2 });
var invMesa = await c1.LeerHasta<Inventario>();
Comprobar(invMesa?.Slots.Any(s => s.Material == Bloques.Mesa) == true, "craftear 4 tablones -> mesa de trabajo");

// Picar piedra SIN pico: no suelta bloque
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
await c1.LeerHasta<BloqueCambio>();
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invSinPico = await c1.LeerHasta<Inventario>(timeoutMs: 400);
await c1.LeerHasta<BloqueCambio>();
Comprobar(invSinPico == null || !invSinPico.Slots.Any(s => s.Material == Bloques.Piedra), "sin pico, la piedra no suelta bloque");

// Pico de madera (receta 5): 3 tablones + 2 palos
await c1.Enviar(new Craftear { Receta = 5 });
var invPico = await c1.LeerHasta<Inventario>();
Comprobar(invPico?.Slots.Any(s => s.Material == (ushort)ItemId.PicoMadera) == true, "craftear pico de madera");

// Picar piedra CON pico: suelta bloque (8 veces para el horno)
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
await c1.LeerHasta<BloqueCambio>();
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invConPico = await c1.LeerHasta<Inventario>();
await c1.LeerHasta<BloqueCambio>();
Comprobar(invConPico?.Slots.Any(s => s.Material == Bloques.Piedra) == true, "con pico, la piedra suelta bloque");
for (int i = 0; i < 7; i++)
{
    await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
    await c1.LeerHasta<BloqueCambio>();
    await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
    await c1.LeerHasta<Inventario>();
    await c1.LeerHasta<BloqueCambio>();
}

// Horno (receta 3): 8 piedra -> horno
await c1.Enviar(new Craftear { Receta = 3 });
var invHorno = await c1.LeerHasta<Inventario>();
Comprobar(invHorno?.Slots.Any(s => s.Material == Bloques.Horno) == true, "craftear 8 piedra -> horno");

// Cocinar sin horno cerca -> error
await c1.Enviar(new Cocinar { Receta = 0 });
var errHorno = await c1.LeerHasta<ErrorServidor>();
Comprobar(errHorno?.Codigo == "SIN_HORNO", "cocinar sin horno devuelve SIN_HORNO");

// Matar un mob pasivo para obtener carne cruda (drop + auto-recogida)
Console.WriteLine("  matando un mob pasivo para probar drops...");
MobEstado? objetivo = null;
for (int intento = 0; intento < 6 && objetivo == null; intento++)
{
    var mobsMsg = await c1.LeerHasta<Mobs>(timeoutMs: 2000);
    if (mobsMsg == null) break;
    objetivo = mobsMsg.Lista.FirstOrDefault(m => m.Tipo <= 2);
}
Comprobar(objetivo != null, "hay un mob pasivo en el mundo");

if (objetivo != null)
{
    // Teletransportar a Ana junto al mob (el servidor actualiza Pos con el mensaje Posicion)
    await c1.Enviar(new Posicion { Px = objetivo.Px, Py = objetivo.Py, Pz = objetivo.Pz, Ry = 0, Pitch = 0 });
    await Task.Delay(150);

    for (int i = 0; i < 5; i++) await c1.Enviar(new GolpearMob { Id = objetivo.Id });
    await Task.Delay(900); // esperar drop + auto-recogida

    var invDrop = await c1.LeerHasta<Inventario>(timeoutMs: 2000);
    bool tieneCarne = invDrop != null && invDrop.Slots.Any(s =>
        s.Material == (ushort)ItemId.CarneCrudaCerdo || s.Material == (ushort)ItemId.CarneCrudaVaca || s.Material == (ushort)ItemId.CarneCrudaOveja);
    Comprobar(tieneCarne, "matar mob -> drop recogido -> carne cruda en inventario");

    // Cocinar: volver al spawn, colocar un horno y cocinar la carne
    if (tieneCarne)
    {
        await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
        await Task.Delay(100);
        await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Horno });
        await c1.LeerHasta<BloqueCambio>();
        int recetaCocina = invDrop!.Slots.Any(s => s.Material == (ushort)ItemId.CarneCrudaCerdo) ? 0
            : invDrop.Slots.Any(s => s.Material == (ushort)ItemId.CarneCrudaVaca) ? 1 : 2;
        await c1.Enviar(new Cocinar { Receta = recetaCocina });
        var invCocido = await c1.LeerHasta<Inventario>(timeoutMs: 2000);
        Comprobar(invCocido != null && invCocido.Slots.Any(s =>
            s.Material == (ushort)ItemId.CarneCocinadaCerdo || s.Material == (ushort)ItemId.CarneCocinadaVaca || s.Material == (ushort)ItemId.CarneCocinadaOveja),
            "cocinar carne cruda -> carne cocinada");
    }
}

// ---------- chat ----------
await c1.Enviar(new Chat { Texto = "¡Hola a todos!" });
var chat = await c2.LeerHasta<Chat>();
Comprobar(chat?.Nombre == "Ana" && chat.Texto == "¡Hola a todos!", "chat difundido");

// ---------- persistencia en memoria ----------
Console.WriteLine("Persistencia: el mundo vacio sigue existiendo y luego se borra.");
await c1.Enviar(new Salir());
await c2.Enviar(new Salir());
await Task.Delay(200);
await c1.Enviar(new ListarMundos());
var lista1 = await c1.LeerHasta<ListaMundos>();
Comprobar(lista1!.Mundos.Any(m => m.Id == idPrivado), "el mundo privado permanece en memoria sin jugadores");

// Borrar el mundo publico (Ana es la duena)
await c1.Enviar(new BorrarMundo { Id = idMundo });
await Task.Delay(200);
await c1.Enviar(new ListarMundos());
var lista2 = await c1.LeerHasta<ListaMundos>();
Comprobar(!lista2!.Mundos.Any(m => m.Id == idMundo), "el dueno borra su mundo");
Comprobar(lista2.Mundos.Any(m => m.Id == idPrivado), "el mundo de Bruno sigue en memoria");

// ---------- cierre ----------
c1.Cerrar(); c2.Cerrar();
await servidor.DetenerAsync();

Console.WriteLine();
Console.WriteLine(errores == 0 ? "PRUEBAS SUPERADAS" : $"{errores} PRUEBAS FALLARON");
return errores == 0 ? 0 : 1;

// ------------------------------------------------------------------

static async Task<ClientePrueba> Conectar(int puerto)
{
    var tcp = new TcpClient();
    await tcp.ConnectAsync("127.0.0.1", puerto);
    return new ClientePrueba(tcp);
}

sealed class ClientePrueba
{
    readonly TcpClient _tcp;
    readonly NetworkStream _flujo;
    public ClientePrueba(TcpClient tcp) { _tcp = tcp; _tcp.NoDelay = true; _flujo = tcp.GetStream(); }
    public Task Enviar(Mensaje m) { var d = Protocolo.Codificar(m); return _flujo.WriteAsync(d).AsTask(); }
    public async Task<Mensaje?> LeerCualquiera(int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try { return await Frames.LeerAsync(_flujo, cts.Token); }
        catch { return null; }
    }
    public async Task<T?> LeerHasta<T>(int timeoutMs = 3000) where T : Mensaje
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            while (true)
            {
                var m = await Frames.LeerAsync(_flujo, cts.Token);
                if (m == null) return null;
                if (m is T t) return t;
            }
        }
        catch { return null; }
    }
    public void Cerrar() { try { _tcp.Close(); } catch { } }
}
