using System.Net.Sockets;
using MundoVoxel.Core;

// Prueba automatica del servidor y el protocolo multijugador:
// arranca un GameServer en memoria, se conecta un cliente simulado y se verifican
// crear mundo, unirse, clave privada, romper/colocar bloques, persistencia en memoria y borrado.

int puerto = 25600;
var servidor = new GameServer(puerto, "Servidor de prueba");
servidor.AlRegistrar += Console.WriteLine;
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
Comprobar(mundo.Ancho == Ajustes.Actual.AnchoMundo && mundo.Alto == Ajustes.Actual.AltoMundo && mundo.Profundo == Ajustes.Actual.ProfundoMundo, $"dimensiones del mundo ({mundo.Ancho}x{mundo.Alto}x{mundo.Profundo})");
var aparicion = mundo.ObtenerPuntoAparicion();
Comprobar(mundo.Obtener((int)aparicion.X, (int)aparicion.Y, (int)aparicion.Z) == Bloques.Aire, "punto de aparicion despejado");

// El mundo publico empieza de dia: los hostiles (zombi/esqueleto/creeper) solo salen de noche
var mobsPublico = await c1.LeerHasta<Mobs>(timeoutMs: 8000);
Comprobar(mobsPublico != null && mobsPublico.Lista.Count > 0, $"mobs del mundo publico difundidos ({mobsPublico?.Lista.Count ?? 0})");
Comprobar(mobsPublico != null && mobsPublico.Lista.All(m => m.Tipo <= 2), "de dia solo se generan mobs pasivos");
Comprobar(mobsPublico != null && mobsPublico.Lista.Select(m => m.Tipo).Distinct().Count() >= 3, "hay variedad de tipos de mob (pasivos)");

// ---------- cliente 2: mundo privado ----------
Console.WriteLine("Cliente 2: mundo privado, clave correcta e incorrecta.");
var c2 = await Conectar(puerto);
await c2.Enviar(new Hola { Nombre = "Bruno", Version = "1.0" });
await c2.LeerHasta<Bienvenido>();
await c2.LeerHasta<ListaMundos>();

await c2.Enviar(new CrearMundo { Nombre = "Solo Bruno", Abierto = false, Pin = "1234", Semilla = 12345, HoraInicial = 0 });
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

// ---------- cofre inicial ----------
Console.WriteLine("Cofre inicial: herramientas basicas en el spawn + 4 antorchas.");
var mundoPriv = Mundo.Deserializar(Mundo.Descomprimir(unidoPriv!.MundoComprimido));
int cfx = (int)unidoPriv.Ax + 1, cfz = (int)unidoPriv.Az, cfy = (int)unidoPriv.Ay - 1;
if (mundoPriv.Obtener(cfx, cfy, cfz) != Bloques.Cofre) { cfx = (int)unidoPriv.Ax; cfz = (int)unidoPriv.Az + 1; }
Comprobar(mundoPriv.Obtener(cfx, cfy, cfz) == Bloques.Cofre, "cofre inicial en el spawn");
int antorchas = 0;
foreach (var (dx, dz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
    if (mundoPriv.Obtener(cfx + dx, mundoPriv.Superficie(cfx + dx, cfz + dz), cfz + dz) == Bloques.Antorcha) antorchas++;
Comprobar(antorchas == 4, "4 antorchas alrededor del cofre");
await c1.Enviar(new AbrirCofre { X = cfx, Y = cfy, Z = cfz });
var cofreAb = await c1.LeerHasta<CofreAbierto>(timeoutMs: 8000);
Comprobar(cofreAb != null && cofreAb.Slots.Count >= 5, "abrir el cofre devuelve las herramientas");
Comprobar(cofreAb?.Slots.Any(s => s.Material == (ushort)ItemId.PicoPiedra) == true, "el cofre tiene pico de piedra");

// ---------- mobs ----------
Console.WriteLine("Mobs: el servidor genera y difunde mobs en el mundo.");
var mobs = await c1.LeerHasta<Mobs>(timeoutMs: 8000);
Comprobar(mobs != null && mobs.Lista.Count > 0, $"mobs difundidos ({mobs?.Lista.Count ?? 0})");
Comprobar(mobs != null && mobs.Lista.All(m => m.Px >= 0 && m.Px < Ajustes.Actual.AnchoMundo && m.Pz >= 0 && m.Pz < Ajustes.Actual.ProfundoMundo && m.Py >= 1), "posiciones de mobs dentro del mundo");
Comprobar(mobs != null && mobs.Lista.Select(m => m.Tipo).Distinct().Count() >= 3, "hay variedad de tipos de mob");

// ---------- romper y colocar bloques ----------
Console.WriteLine("Bloques: romper y colocar con difusion.");
var aparicionPriv = unidoPriv!;
int bx = (int)aparicionPriv.Ax, by = (int)aparicionPriv.Ay - 1, bz = (int)aparicionPriv.Az;
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var cambio = await c1.LeerBloqueEn(bx, by, bz);
Comprobar(cambio != null && cambio.Bloque == Bloques.Aire, "romper bloque difunde BloqueCambio");

await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Ladrillo });
cambio = await c1.LeerBloqueEn(bx, by, bz);
Comprobar(cambio?.Bloque == Bloques.Ladrillo, "colocar bloque difunde BloqueCambio");

await c1.Enviar(new RomperBloque { X = 999, Y = 999, Z = 999 });
var cambioInvalido = await c1.LeerBloqueEn(999, 999, 999, 600);
Comprobar(cambioInvalido == null, "romper fuera del mundo se ignora");

// ---------- inventario, crafteo, cocina y drops ----------
Console.WriteLine("Mecanicas: inventario, crafteo, cocina y drops de mobs.");

// Romper el ladrillo colocado: el servidor envia Inventario ANTES que BloqueCambio
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invLadrillo = await c1.LeerHasta<Inventario>();
await c1.LeerBloqueEn(bx, by, bz);
Comprobar(invLadrillo?.Slots.Any(s => s.Material == Bloques.Ladrillo && s.Cantidad >= 1) == true, "romper bloque lo mete al inventario");

// Conseguir 3 madera (colocar y romper troncos)
for (int i = 0; i < 3; i++)
{
    await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Madera });
    await c1.LeerBloqueEn(bx, by, bz);
    await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
    await c1.LeerHasta<Inventario>();
    await c1.LeerBloqueEn(bx, by, bz);
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

// Picar piedra SIN pico: no suelta bloque (el inventario no gana piedra)
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
await c1.LeerBloqueEn(bx, by, bz);
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invSinPico = await c1.LeerHasta<Inventario>(timeoutMs: 400);
await c1.LeerBloqueEn(bx, by, bz);
int piedraAntes = invSinPico?.Slots.FirstOrDefault(s => s.Material == Bloques.Piedra)?.Cantidad ?? 0;
Comprobar(piedraAntes <= 5, "sin pico, la piedra no suelta bloque");

// Pico de madera: 3 tablones + 2 palos (buscar la receta por nombre)
int idxPicoMadera = Array.FindIndex(Objetos.RecetasCrafteo, r => r.Nombre == "Pico de madera");
await c1.Enviar(new Craftear { Receta = idxPicoMadera });
var invPico = await c1.LeerHasta<Inventario>();
Comprobar(invPico?.Slots.Any(s => s.Material == (ushort)ItemId.PicoMadera) == true, "craftear pico de madera");
// Seleccionar el pico en la hotbar (slot = indice en la lista del inventario)
int idxPicoInv = invPico!.Slots.FindIndex(s => s.Material == (ushort)ItemId.PicoMadera);
await c1.Enviar(new SeleccionarSlot { Slot = Math.Min(idxPicoInv, 8), Material = (ushort)ItemId.PicoMadera });

// Picar piedra CON pico: suelta bloque (8 veces para el horno)
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
await c1.LeerBloqueEn(bx, by, bz);
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var invConPico = await c1.LeerHasta<Inventario>();
await c1.LeerBloqueEn(bx, by, bz);
int piedraConPico = invConPico?.Slots.FirstOrDefault(s => s.Material == Bloques.Piedra)?.Cantidad ?? 0;
Comprobar(piedraConPico > 5, "con pico, la piedra suelta bloque");
for (int i = 0; i < 7; i++)
{
    await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
    await c1.LeerBloqueEn(bx, by, bz);
    await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
    await c1.LeerHasta<Inventario>();
    await c1.LeerBloqueEn(bx, by, bz);
}

// Horno (receta 3): 8 piedra -> horno
await c1.Enviar(new Craftear { Receta = 3 });
var invHorno = await c1.LeerHasta<Inventario>();
Comprobar(invHorno?.Slots.Any(s => s.Material == Bloques.Horno) == true, "craftear 8 piedra -> horno");

// Cocinar sin horno cerca -> error
await c1.Enviar(new Cocinar { Receta = 0 });
var errHorno = await c1.LeerHasta<ErrorServidor>();
Comprobar(errHorno?.Codigo == "SIN_HORNO", "cocinar sin horno devuelve SIN_HORNO");

// Minerales: el generador coloca carbon, hierro, oro y diamante en el subsuelo
var mundoMin = Mundo.Generar(12345);
var conteoMin = new int[20];
for (int x = 0; x < mundoMin.Ancho; x++)
    for (int y = 0; y < mundoMin.Alto; y++)
        for (int z = 0; z < mundoMin.Profundo; z++)
        {
            var b = mundoMin.Obtener(x, y, z);
            if (b >= Bloques.Carbon && b <= Bloques.Diamante) conteoMin[b]++;
        }
Comprobar(conteoMin[Bloques.Carbon] > 0, $"el mundo tiene carbon ({conteoMin[Bloques.Carbon]} bloques)");
Comprobar(conteoMin[Bloques.Hierro] > 0, $"el mundo tiene hierro ({conteoMin[Bloques.Hierro]} bloques)");
Comprobar(conteoMin[Bloques.Oro] > 0, $"el mundo tiene oro ({conteoMin[Bloques.Oro]} bloques)");
Comprobar(conteoMin[Bloques.Diamante] > 0, $"el mundo tiene diamante ({conteoMin[Bloques.Diamante]} bloques)");

// Matar un mob pasivo para obtener carne cruda (drop + auto-recogida)
Console.WriteLine("  matando un mob pasivo para probar drops...");
MobEstado? objetivo = null;
for (int intento = 0; intento < 6 && objetivo == null; intento++)
{
    var mobsMsg = await c1.LeerHasta<Mobs>(timeoutMs: 8000);
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

    var invDrop = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
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
        var invCocido = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
        Comprobar(invCocido != null && invCocido.Slots.Any(s =>
            s.Material == (ushort)ItemId.CarneCocinadaCerdo || s.Material == (ushort)ItemId.CarneCocinadaVaca || s.Material == (ushort)ItemId.CarneCocinadaOveja),
            "cocinar carne cruda -> carne cocinada");
    }
}

// ---------- mecanicas nuevas: fundicion, cultivos, TNT, ataque hostil y dia/noche ----------
Console.WriteLine("Mecanicas nuevas: fundicion, cultivos, TNT, hostiles y dia/noche.");

// Fundicion: picar carbon y oro en bruto, fundir el oro en el horno
// Ana se aparta del spawn: la celda (bx, by+2, bz) es la de su cabeza y Colocar
// rechaza bloques encima de un jugador. Bruno tambien se aparta (sigue en el spawn).
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax + 4, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
await c2.Enviar(new Posicion { Px = aparicionPriv.Ax + 6, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az + 6, Ry = 0, Pitch = 0 });
await Task.Delay(100);
await c1.Enviar(new ColocarBloque { X = bx, Y = by + 2, Z = bz, Bloque = Bloques.Carbon });
await c1.LeerHasta<BloqueCambio>();
await c1.Enviar(new RomperBloque { X = bx, Y = by + 2, Z = bz });
var invCarbon = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
await c1.LeerHasta<BloqueCambio>();
Comprobar(invCarbon?.Slots.Any(s => s.Material == (ushort)ItemId.CarbonItem) == true, "picar carbon da carbon (combustible)");

await c1.Enviar(new ColocarBloque { X = bx, Y = by + 2, Z = bz, Bloque = Bloques.Oro });
await c1.LeerHasta<BloqueCambio>();
await c1.Enviar(new RomperBloque { X = bx, Y = by + 2, Z = bz });
var invOroBruto = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
await c1.LeerHasta<BloqueCambio>();
Comprobar(invOroBruto?.Slots.Any(s => s.Material == (ushort)ItemId.OroBruto) == true, "picar oro da oro en bruto");

await c1.Enviar(new Cocinar { Receta = 3 }); // fundir oro (receta 3 del horno)
var invLingote = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
Comprobar(invLingote?.Slots.Any(s => s.Material == (ushort)ItemId.LingoteOro) == true, "fundir oro en bruto -> lingote de oro");

// Un mob hostil ataca al jugador si esta cerca (se prueba de noche, antes de que
// los hostiles se quemen al amanecer). Se usa un ZOMBI (Tipo 3) porque golpea en
// bucle. Tras comprobar el ataque se mata al zombi y se cura a Ana con el modo
// espectador (el test de muerte + respawn se hace al final de la suite).
var mobsHostilMsg = await c1.LeerHasta<Mobs>(timeoutMs: 8000);
var hostil = mobsHostilMsg?.Lista.FirstOrDefault(m => m.Tipo == 3); // zombi
if (hostil != null)
{
    await c1.Enviar(new Posicion { Px = hostil.Px, Py = hostil.Py, Pz = hostil.Pz, Ry = 0, Pitch = 0 });
    await Task.Delay(300);
    var saludMsg = await c1.LeerHasta<JugadorSalud>(timeoutMs: 8000);
    Comprobar(saludMsg != null && saludMsg.Salud < 20, "un mob hostil ataca al jugador cercano (la vida baja)");
    // Matar al zombi de verdad: 20 de salud, cada golpe hace 5+espada; se
    // golpea en bucle hasta que desaparezca del mensaje Mobs.
    for (int g = 0; g < 8; g++)
        await c1.Enviar(new GolpearMob { Id = hostil.Id });
    await Task.Delay(300);
    bool zombiMuerto = true;
    var msVerif = await c1.LeerHasta<Mobs>(timeoutMs: 3000);
    if (msVerif != null && msVerif.Lista.Any(m => m.Id == hostil.Id)) zombiMuerto = false;
    if (!zombiMuerto)
        for (int g = 0; g < 8; g++)
            await c1.Enviar(new GolpearMob { Id = hostil.Id });
    await Task.Delay(300);
}
else Comprobar(false, "un mob hostil ataca al jugador cercano (la vida baja)");
// Curar a Ana (el modo espectador restaura la vida) y volver al spawn
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(200);
await c1.Enviar(new ModoEspectador { Activo = false });
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
// Drenar los inventarios de los drops del zombi muerto
for (int d = 0; d < 4; d++)
    _ = await c1.LeerHasta<Inventario>(timeoutMs: 300);
// Barrer SOLO los hostiles cercanos al spawn: zombis (3) y esqueletos (5).
// Los creepers (4) NO se golpean: explotan al recibir dano y matan a Ana
// (cascada de fallos). El radio de agresion los trae al spawn igualmente.
for (int g = 0; g < 8; g++)
{
    var msBarrido = await c1.LeerHasta<Mobs>(timeoutMs: 1200);
    if (msBarrido == null) break;
    var hostiles = msBarrido.Lista
        .Where(m => (m.Tipo == 3 || m.Tipo == 5)
            && MathF.Abs(m.Px - aparicionPriv.Ax) < 8
            && MathF.Abs(m.Pz - aparicionPriv.Az) < 8)
        .ToList();
    if (hostiles.Count == 0) break;
    foreach (var h in hostiles)
        for (int k = 0; k < 6; k++)
            await c1.Enviar(new GolpearMob { Id = h.Id });
}
await Task.Delay(400);
for (int d = 0; d < 4; d++)
    _ = await c1.LeerHasta<Inventario>(timeoutMs: 300);
// Poner el mundo de dia: los hostiles restantes se queman con el sol y dejan
// de acosar a Ana durante el resto de la suite (el trigo tarda en madurar).
await c1.Enviar(new FijarHora { Hora = 9f });
await Task.Delay(400);
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });

// El servidor envia el estado de oxigeno (se agota bajo el agua). Si el zombi
// del test anterior dejo a Ana herida/muerta, se cura con el modo espectador.
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(150);
await c1.Enviar(new ModoEspectador { Activo = false });
var oxMsg = await c1.LeerHasta<OxigenoMsg>(timeoutMs: 8000);
Comprobar(oxMsg != null && oxMsg.MaxOxigeno > 0, "el servidor envia el estado de oxigeno");

// La lava es un liquido (para lagos que queman) y no es colocable a mano
Comprobar(Bloques.EsLiquido(Bloques.Lava), "la lava es un liquido");
Comprobar(!Bloques.EsColocable(Bloques.Lava), "la lava no se puede colocar a mano");

// Modo espectador: no puede romper bloques (el servidor lo ignora)
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(300);
await c1.Enviar(new RomperBloque { X = bx, Y = by, Z = bz });
var bloqueNoRoto = await c1.LeerHasta<BloqueCambio>(timeoutMs: 1200);
bool rotoEnEspectador = bloqueNoRoto != null && bloqueNoRoto.Bloque == Bloques.Aire && bloqueNoRoto.X == bx && bloqueNoRoto.Y == by && bloqueNoRoto.Z == bz;
Comprobar(!rotoEnEspectador, "el espectador no rompe bloques");
await c1.Enviar(new ModoEspectador { Activo = false });

// Trigo: la azada labra la tierra, las semillas se plantan, crece y se cosecha
int idxAzada = Array.FindIndex(Objetos.RecetasCrafteo, r => r.Nombre == "Azada de madera");
await c1.Enviar(new Craftear { Receta = idxAzada });
var invActual = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
if (invActual == null) // reintento: el servidor puede estar ocupado con los mobs
{
    await c1.Enviar(new Craftear { Receta = idxAzada });
    invActual = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
}
await c1.Enviar(new ColocarBloque { X = bx + 1, Y = by + 2, Z = bz, Bloque = Bloques.Tierra });
var cambioTierra = await c1.LeerBloqueEn(bx + 1, by + 2, bz, 1500);
if (invActual == null) { Console.WriteLine("  [FALLA] craftear azada de madera (sin respuesta)"); errores++; return 1; }
int idxAzadaInv = invActual.Slots.FindIndex(s => s.Material == (ushort)ItemId.AzadaMadera);
await c1.Enviar(new SeleccionarSlot { Slot = Math.Max(0, Math.Min(idxAzadaInv, 8)), Material = (ushort)ItemId.AzadaMadera });
await c1.Enviar(new UsarBloque { X = bx + 1, Y = by + 2, Z = bz });
var cambioLabrado = await c1.LeerBloqueEn(bx + 1, by + 2, bz, 1500);
Comprobar(cambioLabrado?.Bloque == Bloques.TierraLabrada, "la azada labra la tierra");
int idxSemilla = invActual.Slots.FindIndex(s => s.Material == (ushort)ItemId.SemillasTrigo);
await c1.Enviar(new SeleccionarSlot { Slot = Math.Max(0, Math.Min(idxSemilla, 8)), Material = (ushort)ItemId.SemillasTrigo });
await c1.Enviar(new UsarBloque { X = bx + 1, Y = by + 2, Z = bz });
// El trigo se planta en (bx+1, by+3): el servidor pone Trigo0 en ub.Y + 1
var cambioTrigo = await c1.LeerBloqueEn(bx + 1, by + 3, bz, 5000);
Comprobar(cambioTrigo?.Bloque == Bloques.Trigo0, "plantar semillas en tierra labrada");
// Esperar a que madure. Ana se pone en modo espectador: no recibe dano de los
// hostiles nocturnos (mientras tanto el servidor sigue haciendo crecer el trigo).
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(200);
bool trigoMaduro = false;
for (int i = 0; i < 300 && !trigoMaduro; i++)
{
    var cb = await c1.LeerBloqueEn(bx + 1, by + 3, bz, 700);
    if (cb?.Bloque == Bloques.Trigo3) trigoMaduro = true;
}
await c1.Enviar(new ModoEspectador { Activo = false });
Comprobar(trigoMaduro, "el trigo crece hasta madurar");
await c1.Enviar(new RomperBloque { X = bx + 1, Y = by + 3, Z = bz });
var invCosecha = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
await c1.LeerBloqueEn(bx + 1, by + 3, bz);
Comprobar(invCosecha?.Slots.Any(s => s.Material == (ushort)ItemId.Trigo) == true, "cosechar trigo maduro da trigo");

// Soltar item con Q: el inventario baja 1 y el drop se recoge solo.
// Se usa el Inventario fresco de la cosecha y el slot con mas cantidad.
var invPreSoltar = invCosecha;
int slotSoltar = -1; int cantInicial = 0; ushort matSoltado = 0;
if (invPreSoltar != null)
{
    var mejor = invPreSoltar.Slots.OrderByDescending(s => s.Cantidad).FirstOrDefault();
    if (mejor != null && mejor.Cantidad > 0)
    {
        slotSoltar = invPreSoltar.Slots.IndexOf(mejor);
        cantInicial = mejor.Cantidad;
        matSoltado = mejor.Material;
    }
}
bool soltoOk = false;
if (slotSoltar >= 0)
{
    int objetivo1 = cantInicial >= 2 ? cantInicial - 1 : 0;
    int objetivo2 = cantInicial >= 2 ? cantInicial : 1;
    await c1.Enviar(new SoltarItem { Slot = slotSoltar });
    int cantS1 = -1, cantS2 = -1;
    for (int i = 0; i < 8 && cantS1 != objetivo1; i++)
    {
        var invS1 = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
        cantS1 = invS1?.Slots.FirstOrDefault(s => s.Material == matSoltado)?.Cantidad ?? 0;
    }
    await Task.Delay(900);
    for (int i = 0; i < 8 && cantS2 != objetivo2; i++)
    {
        var invS2 = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
        cantS2 = invS2?.Slots.FirstOrDefault(s => s.Material == matSoltado)?.Cantidad ?? 0;
    }
    soltoOk = cantS1 == objetivo1 && cantS2 == objetivo2;
}
else Console.WriteLine("[DEBUG] soltar: sin slot disponible");
Comprobar(soltoOk, "soltar item (Q) suelta 1 y el drop se recoge");

// TNT: colocar, encender con el mechero y esperar la explosion
await c1.Enviar(new ColocarBloque { X = bx + 2, Y = by + 2, Z = bz, Bloque = Bloques.Tnt });
var cambioTnt = await c1.LeerBloqueEn(bx + 2, by + 2, bz, 5000);
Comprobar(cambioTnt?.Bloque == Bloques.Tnt, "colocar TNT difunde BloqueCambio");
int idxMechero = invActual.Slots.FindIndex(s => s.Material == (ushort)ItemId.Mechero);
await c1.Enviar(new SeleccionarSlot { Slot = Math.Max(0, Math.Min(idxMechero, 8)), Material = (ushort)ItemId.Mechero });
await c1.Enviar(new UsarBloque { X = bx + 2, Y = by + 2, Z = bz });
bool tntExploto = false;
// La explosion destruye ~100 bloques (radio 3.5); el BloqueCambio del centro
// llega en medio de la rafaga. Se filtra por posicion para no perderse con el
// ruido de cultivos/mobs del mundo grande.
for (int i = 0; i < 400 && !tntExploto; i++)
{
    var cb = await c1.LeerBloqueEn(bx + 2, by + 2, bz, 500);
    if (cb != null && cb.Bloque == Bloques.Aire) tntExploto = true;
}
Comprobar(tntExploto, "el mechero enciende la TNT y explota");

// Ciclo dia/noche: la hora avanza
var t1m = await c1.LeerHasta<TiempoMundo>(timeoutMs: 8000);
await Task.Delay(1500);
var t2m = await c1.LeerHasta<TiempoMundo>(timeoutMs: 8000);
Comprobar(t1m != null && t2m != null && t2m.Hora != t1m.Hora, "el ciclo dia/noche avanza");

// Creeper: radio de explosion configurable (default 3) y no rompe con agua cerca
Comprobar(MobsInfo.Datos(TipoMob.Creeper).RadioExplosion == 3f, "el creeper explota con radio 3 por defecto");
Comprobar(MobsInfo.Datos(TipoMob.Zombi).SoloNoche, "el zombi solo sale de noche");
Comprobar(MobsInfo.Datos(TipoMob.Zombi).SeQuemaSol, "el zombi se quema con el sol");

// ---------- muerte y respawn ----------
// Se usa la LAVA (los lagos existen siempre, no dependen de la hora): Ana se
// teleporta dentro de un lago de lava, recibe dano continuo y muere. Primero se
// cura con el modo espectador por si la explosion de la TNT la dejo herida o
// muerta. Debe llegar MuerteInfo con la causa y el Respawn solo se responde si
// el jugador lo pide (vuelve al spawn con vida llena).
Console.WriteLine("Muerte: causa + respawn manual.");
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(200);
await c1.Enviar(new ModoEspectador { Activo = false });
int lx = -1, ly = -1, lz = -1;
// Buscar la SUPERFICIE de un lago de lava (lava con aire encima): teleportarse
// al fondo (con los lagos profundos de 4-20) dejaria a Ana dentro de piedra.
for (int y = 0; y < mundoPriv.Alto && lx < 0; y++)
    for (int x = 0; x < mundoPriv.Ancho && lx < 0; x++)
        for (int z = 0; z < mundoPriv.Profundo && lx < 0; z++)
            if (mundoPriv.Obtener(x, y, z) == Bloques.Lava && y + 1 < mundoPriv.Alto &&
                mundoPriv.Obtener(x, y + 1, z) == Bloques.Aire) { lx = x; ly = y; lz = z; }
bool murioConCausa = false;
if (lx >= 0)
{
    await c1.Enviar(new Posicion { Px = lx + 0.5f, Py = ly - 0.5f, Pz = lz + 0.5f, Ry = 0, Pitch = 0 });
    for (int i = 0; i < 30 && !murioConCausa; i++)
    {
        var mi = await c1.LeerHasta<MuerteInfo>(timeoutMs: 8000);
        if (mi != null && mi.Causa.Length > 0) murioConCausa = true;
    }
}
Comprobar(murioConCausa, "al morir se envia la causa de muerte");
// Reaparecer: el servidor responde con Respawn (posicion del spawn) y vida llena.
// Ojo: el JugadorSalud(0) de la muerte queda en la cola antes del nuevo (20),
// asi que se leen varios hasta ver la salud 20 (el Respawn llega despues del 20).
await c1.Enviar(new Respawn());
int saludFinal = -1;
for (int i = 0; i < 12 && saludFinal != 20; i++)
{
    var s2 = await c1.LeerHasta<JugadorSalud>(timeoutMs: 8000);
    if (s2 != null) saludFinal = s2.Salud;
}
var rp = await c1.LeerHasta<Respawn>(timeoutMs: 8000);
Comprobar(rp != null && saludFinal == 20, "reaparecer vuelve al spawn con vida llena");
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });

// ---------- chat ----------
await c1.Enviar(new Chat { Texto = "Â¡Hola a todos!" });
var chat = await c2.LeerHasta<Chat>();
Comprobar(chat?.Nombre == "Ana" && chat.Texto == "Â¡Hola a todos!", "chat difundido");

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
    public async Task<T?> LeerHasta<T>(int timeoutMs = 10000) where T : Mensaje
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
    /// <summary>Espera un BloqueCambio en la posicion indicada (ignora el ruido de
    /// cultivos/mobs de otras zonas del mundo, que con mundos grandes es mucho).</summary>
    public async Task<BloqueCambio?> LeerBloqueEn(int x, int y, int z, int timeoutMs = 10000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            while (true)
            {
                var m = await Frames.LeerAsync(_flujo, cts.Token);
                if (m == null) return null;
                if (m is BloqueCambio bc && bc.X == x && bc.Y == y && bc.Z == z) return bc;
            }
        }
        catch { return null; }
    }
    public void Cerrar() { try { _tcp.Close(); } catch { } }
}

