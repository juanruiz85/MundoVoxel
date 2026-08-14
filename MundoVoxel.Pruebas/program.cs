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
