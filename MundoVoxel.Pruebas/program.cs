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
int idAna = bienvenido!.IdJugador;
var lista0 = await c1.LeerHasta<ListaMundos>();
Comprobar(lista0?.Mundos.Count == 0, "lista de mundos vacia al inicio");

await c1.Enviar(new CrearMundo { Nombre = "Mundo de Ana", Abierto = true });
var creado = await c1.LeerHasta<MundoCreado>();
Comprobar(creado != null, "mundo creado");
string idMundo = creado!.Id;
var unido = (await LeerUnidoCompleto(c1)).Unido;
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

await c2.Enviar(new CrearMundo { Nombre = "Solo Bruno", Abierto = false, Pin = "123456", Semilla = 12345, HoraInicial = 0 });
await c2.LeerHasta<MundoCreado>();
var unido2 = (await LeerUnidoCompleto(c2)).Unido;
// Diagnostico CI: colector dedicado para Bruno desde su entrada al mundo; cuenta
// los mensajes por tipo para ver si la entrega al cliente inactivo se cae.
var colaBruno = new System.Collections.Concurrent.ConcurrentQueue<Mensaje>();
var tiposBruno = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
_ = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            var m = await c2.LeerSinTimeout();
            if (m == null) break;
            tiposBruno.AddOrUpdate(m.GetType().Name, 1, (_, v) => v + 1);
            colaBruno.Enqueue(m);
        }
    }
    catch { /* cierre de la conexion al final de la suite */ }
});
Comprobar(unido2 != null, "Bruno entra a su mundo privado");
string idPrivado = unido2!.Id;

// Ana intenta entrar al mundo privado con clave incorrecta
await c1.Enviar(new Unirse { Id = idPrivado, Pin = "999999" });
var errPin = await c1.LeerHasta<ErrorServidor>();
Comprobar(errPin?.Codigo == "PIN_INCORRECTO", "clave incorrecta rechazada");

// con la clave correcta si entra
await c1.Enviar(new Unirse { Id = idPrivado, Pin = "123456" });
var resPriv = await LeerUnidoCompleto(c1);
var unidoPriv = resPriv.Unido;
Comprobar(unidoPriv!.MundoComprimido.Length > 0 && resPriv.Trozos >= 1, $"el mundo llega troceado y se reensambla ({resPriv.Trozos} trozos, {unidoPriv.MundoComprimido.Length} bytes)");
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
var roturaInicial = await RomperHasta(c1, bx, by, bz);
var cambio = roturaInicial.Cambio;
Comprobar(cambio != null && cambio.Bloque == Bloques.Aire, "romper bloque difunde BloqueCambio");

// Colocar TIERRA (el kit la trae): difunde y CONSUME 1 del inventario (survival)
// El servidor envia Inventario ANTES que BloqueCambio, asi que se lee primero.
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Tierra });
var invColocada = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
cambio = await c1.LeerBloqueEn(bx, by, bz, 8000);
Comprobar(cambio?.Bloque == Bloques.Tierra, "colocar bloque difunde BloqueCambio");
var tierraColocada = invColocada?.Slots.FirstOrDefault(s => s.Material == Bloques.Tierra)?.Cantidad ?? 0;
Comprobar(tierraColocada == 9, $"colocar consume 1 del inventario (tierra 10->9, quedo {tierraColocada})");

// Colocar un bloque que NO se tiene (ladrillo): el servidor lo rechaza (anti-cheat)
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Ladrillo });
var cambioLadrillo = await c1.LeerBloqueEn(bx, by, bz, 700);
Comprobar(cambioLadrillo == null, "colocar bloque sin tenerlo se ignora");

// Romper la tierra colocada: vuelve al inventario
var roturaTierra = await RomperHasta(c1, bx, by, bz);
Comprobar(roturaTierra.Inv?.Slots.Any(s => s.Material == Bloques.Tierra && s.Cantidad >= 9) == true, "romper bloque lo mete al inventario");

// Conseguir 3 madera (colocar y romper troncos)
for (int i = 0; i < 3; i++)
{
    await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Madera });
    await c1.LeerBloqueEn(bx, by, bz);
    _ = await RomperHasta(c1, bx, by, bz);
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

// Cavar hacia abajo hasta conseguir 9 piedra natural (con pico en el inventario):
// la piedra solo cae si el jugador tiene un pico (aunque no este seleccionado).
await c1.Enviar(new SeleccionarSlot { Slot = 0, Material = Bloques.Madera });
int yCava = by - 1;
int piedras = 0;
for (int i = 0; i < 14 && piedras < 9; i++)
{
    var roturaCava = await RomperHasta(c1, bx, yCava, bz);
    piedras = roturaCava.Inv?.Slots.FirstOrDefault(s => s.Material == Bloques.Piedra)?.Cantidad ?? piedras;
    yCava--;
}
Comprobar(piedras >= 8, $"picar piedra natural con pico en el inventario (conseguidas {piedras})");

// Pico de madera: 3 tablones + 2 palos (buscar la receta por nombre)
int idxPicoMadera = Array.FindIndex(Objetos.RecetasCrafteo, r => r.Nombre == "Pico de madera");
await c1.Enviar(new Craftear { Receta = idxPicoMadera });
var invPico = await c1.LeerHasta<Inventario>();
Comprobar(invPico?.Slots.Any(s => s.Material == (ushort)ItemId.PicoMadera) == true, "craftear pico de madera");

// Horno (receta 3): 8 piedra -> horno
await c1.Enviar(new Craftear { Receta = 3 });
var invHorno = await c1.LeerHasta<Inventario>();
Comprobar(invHorno?.Slots.Any(s => s.Material == Bloques.Horno) == true, "craftear 8 piedra -> horno");

// Soltar TODOS los picos del inventario: sin pico, la piedra rompida no se guarda
for (int intento = 0; intento < 3; intento++)
{
    var invAct = await c1.LeerHasta<Inventario>(timeoutMs: 600);
    int idx = invAct?.Slots.FindIndex(s => s.Material == (ushort)ItemId.PicoMadera) ?? -1;
    if (idx < 0) break;
    await c1.Enviar(new SoltarItem { Slot = idx });
}
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Piedra });
await c1.LeerBloqueEn(bx, by, bz, 8000);
var roturaSinPico = await RomperHasta(c1, bx, by, bz);
int piedraSinPico = roturaSinPico.Inv?.Slots.FirstOrDefault(s => s.Material == Bloques.Piedra)?.Cantidad ?? 0;
Comprobar(piedraSinPico == 0, $"sin pico en el inventario, la piedra rompida no se guarda ({piedraSinPico})");

// ---------- herramientas: golpes por bloque (pico/hacha/pala con funcion real) ----------
Comprobar(Objetos.GolpesPara(Bloques.Piedra, 0) == 5, "piedra a mano requiere 5 golpes");
Comprobar(Objetos.GolpesPara(Bloques.Piedra, (ushort)ItemId.PicoMadera) == 2, "pico de madera rompe piedra en 2 golpes");
Comprobar(Objetos.GolpesPara(Bloques.Piedra, (ushort)ItemId.PicoHierro) == 1, "pico de hierro rompe piedra al primer golpe");
Comprobar(Objetos.GolpesPara(Bloques.Madera, 0) == 3, "madera a mano requiere 3 golpes");
Comprobar(Objetos.GolpesPara(Bloques.Madera, (ushort)ItemId.HachaPiedra) == 1, "hacha rompe madera al primer golpe");
Comprobar(Objetos.GolpesPara(Bloques.Tierra, 0) == 2, "tierra a mano requiere 2 golpes");
Comprobar(Objetos.GolpesPara(Bloques.Tierra, (ushort)ItemId.PalaMadera) == 1, "pala rompe tierra al primer golpe");
Comprobar(Objetos.GolpesPara(Bloques.Hoja, 0) == 1, "las plantas siguen rompiendose al primer golpe");

// Tablones extra: el hacha y la pala de madera se craftean con tablones
await c1.Enviar(new Craftear { Receta = 0 });
_ = await c1.LeerHasta<Inventario>(timeoutMs: 8000);

// Hacha de madera: craftearla de verdad y verificar que la madera cae al primer golpe
int idxHacha = Array.FindIndex(Objetos.RecetasCrafteo, r => r.Nombre == "Hacha de madera");
await c1.Enviar(new Craftear { Receta = idxHacha });
var invHacha = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
Comprobar(invHacha?.Slots.Any(s => s.Material == (ushort)ItemId.HachaMadera) == true, "craftear hacha de madera");
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Madera });
await c1.LeerBloqueEn(bx, by, bz, 8000);
var roturaHacha = await RomperHasta(c1, bx, by, bz, maxGolpes: 1);
Comprobar(roturaHacha.Cambio != null, "con hacha en el inventario la madera cae al primer golpe");

// Pala de madera: palos extra, craftearla y verificar que la tierra cae al primer golpe
await c1.Enviar(new Craftear { Receta = 1 });
_ = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
int idxPala = Array.FindIndex(Objetos.RecetasCrafteo, r => r.Nombre == "Pala de madera");
await c1.Enviar(new Craftear { Receta = idxPala });
var invPala = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
Comprobar(invPala?.Slots.Any(s => s.Material == (ushort)ItemId.PalaMadera) == true, "craftear pala de madera");
await c1.Enviar(new ColocarBloque { X = bx, Y = by, Z = bz, Bloque = Bloques.Tierra });
await c1.LeerBloqueEn(bx, by, bz, 8000);
var roturaPala = await RomperHasta(c1, bx, by, bz, maxGolpes: 1);
Comprobar(roturaPala.Cambio != null, "con pala en el inventario la tierra cae al primer golpe");
if (roturaPala.Cambio == null) _ = await RomperHasta(c1, bx, by, bz); // limpieza: no dejar bloque puesto

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

    for (int i = 0; i < 5; i++) { await c1.Enviar(new GolpearMob { Id = objetivo.Id }); await Task.Delay(300); } // cooldown anti-autoclick del servidor
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

// Fundicion: picar carbon y oro NATURAL del mundo (el anti-cheat ya no permite
// colocar minerales sin tenerlos). Se buscan vetas en el mundo deserializado y
// Ana se teletransporta junto a ellas para picarlas con el pico.
int cxC = -1, cyC = 0, czC = 0;
for (int x = 1; x < mundoPriv.Ancho && cxC < 0; x++)
    for (int y = 1; y < mundoPriv.Alto && cxC < 0; y++)
        for (int z = 1; z < mundoPriv.Profundo && cxC < 0; z++)
            if (mundoPriv.Obtener(x, y, z) == Bloques.Carbon) { cxC = x; cyC = y; czC = z; }
Comprobar(cxC >= 0, "hay carbon natural en el mundo");
int cxO = -1, cyO = 0, czO = 0;
for (int x = 1; x < mundoPriv.Ancho && cxO < 0; x++)
    for (int y = 1; y < mundoPriv.Alto && cxO < 0; y++)
        for (int z = 1; z < mundoPriv.Profundo && cxO < 0; z++)
            if (mundoPriv.Obtener(x, y, z) == Bloques.Oro) { cxO = x; cyO = y; czO = z; }
Comprobar(cxO >= 0, "hay oro natural en el mundo");
if (cxC >= 0)
{
    await c1.Enviar(new Posicion { Px = cxC, Py = cyC, Pz = czC, Ry = 0, Pitch = 0 });
    await Task.Delay(150);
    var invCarbon = (await RomperHasta(c1, cxC, cyC, czC)).Inv;
    Comprobar(invCarbon?.Slots.Any(s => s.Material == (ushort)ItemId.CarbonItem) == true, "picar carbon da carbon (combustible)");
}
if (cxO >= 0)
{
    await c1.Enviar(new Posicion { Px = cxO, Py = cyO, Pz = czO, Ry = 0, Pitch = 0 });
    await Task.Delay(150);
    var invOroBruto = (await RomperHasta(c1, cxO, cyO, czO)).Inv;
    Comprobar(invOroBruto?.Slots.Any(s => s.Material == (ushort)ItemId.OroBruto) == true, "picar oro da oro en bruto");
}
// Volver al spawn (junto al horno) para la fundicion
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax + 4, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
await Task.Delay(150);

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
    for (int g = 0; g < 8; g++) { await c1.Enviar(new GolpearMob { Id = hostil.Id }); await Task.Delay(300); }
    await Task.Delay(300);
    bool zombiMuerto = true;
    var msVerif = await c1.LeerHasta<Mobs>(timeoutMs: 3000);
    if (msVerif != null && msVerif.Lista.Any(m => m.Id == hostil.Id)) zombiMuerto = false;
    if (!zombiMuerto)
        for (int g = 0; g < 8; g++) { await c1.Enviar(new GolpearMob { Id = hostil.Id }); await Task.Delay(300); }
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
        for (int k = 0; k < 6; k++) { await c1.Enviar(new GolpearMob { Id = h.Id }); await Task.Delay(300); }
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
// Tablones extra: el flujo de pruebas consumio tablones en palos, mesa y pico
for (int i = 0; i < 2; i++) { await c1.Enviar(new Craftear { Receta = 0 }); await c1.LeerHasta<Inventario>(); }
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
// Tope por reloj real: en corridas documentadas (0.10.5) el trigo tardo mas que
// el presupuesto original de ~210 s (300 lecturas x 700 ms) y el test fallo. Se
// espera hasta 5 min; el caso feliz sale antes, al ver Trigo3.
await c1.Enviar(new ModoEspectador { Activo = true });
await Task.Delay(200);
bool trigoMaduro = false;
var plazoTrigo = DateTime.UtcNow.AddSeconds(300);
while (!trigoMaduro && DateTime.UtcNow < plazoTrigo)
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
// El servidor SOLO envia Inventario tras un evento (crafteo, recogida...):
// leerlo "a secas" devuelve null si no hay ninguno en vuelo. Se fuerza uno
// con un crafteo barato (madera -> tablones) para partir de un estado FRESCO
// justo antes de soltar: el de la cosecha podria estar desfasado (o no
// existir si el trigo fallo) y el indice del slot ya no corresponderia.
// Se usa el slot con mas cantidad.
await c1.Enviar(new Craftear { Receta = 0 });
var invPreSoltar = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
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
string detalleSoltar = "";
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
    // Acercarse al drop (cayo a 3 bloques, fuera del radio de auto-recogida)
    await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay + 0.4f, Pz = aparicionPriv.Az - 3f, Ry = 0, Pitch = 0 });
    await Task.Delay(400);
    for (int i = 0; i < 8 && cantS2 != objetivo2; i++)
    {
        var invS2 = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
        cantS2 = invS2?.Slots.FirstOrDefault(s => s.Material == matSoltado)?.Cantidad ?? 0;
    }
    soltoOk = cantS1 == objetivo1 && cantS2 == objetivo2;
    detalleSoltar = $" (slot={slotSoltar} mat={matSoltado} inicial={cantInicial} trasSoltar={cantS1}/{objetivo1} trasRecoger={cantS2}/{objetivo2})";
}
else detalleSoltar = " (sin slot disponible)";
Comprobar(soltoOk, "soltar item (Q) suelta 1 y el drop se recoge" + detalleSoltar);

// TNT: sacar del cofre inicial (el anti-cheat no permite colocar lo que no tienes),
// colocar, encender con el mechero y esperar la explosion.
// El slot de la TNT se busca en el CofreAbierto en vez de suponer el 6: si
// antes se extrajo alguna herramienta del cofre los slots se desplazan y el
// 6 deja de ser TNT (fallo documentado en 0.10.5).
await c1.Enviar(new AbrirCofre { X = cfx, Y = cfy, Z = cfz });
var cofreTnt = await c1.LeerHasta<CofreAbierto>(timeoutMs: 8000);
int slotTnt = cofreTnt?.Slots.FindIndex(s => s.Material == Bloques.Tnt) ?? -1;
bool tntSacada = false;
for (int intento = 0; intento < 4 && !tntSacada && slotTnt >= 0; intento++)
{
    await c1.Enviar(new SacarDeCofre { X = cfx, Y = cfy, Z = cfz, Slot = slotTnt, Cantidad = 1 });
    var invTnt = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
    tntSacada = invTnt?.Slots.Any(s => s.Material == Bloques.Tnt) == true;
}
Comprobar(tntSacada, "sacar TNT del cofre inicial al inventario");
await c1.Enviar(new ColocarBloque { X = bx + 2, Y = by + 2, Z = bz, Bloque = Bloques.Tnt });
var cambioTnt = await c1.LeerBloqueEn(bx + 2, by + 2, bz, 5000);
Comprobar(cambioTnt?.Bloque == Bloques.Tnt, "colocar TNT difunde BloqueCambio");
var invMechero = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
int idxMechero = invMechero?.Slots.FindIndex(s => s.Material == (ushort)ItemId.Mechero) ?? -1;
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

// ---------- anti-cheat de movimiento (opt-in por configuracion) ----------
Console.WriteLine("Anti-cheat: el salto imposible se ignora y el movimiento normal pasa.");
Ajustes.Actual.AntiCheatSaltoMax = 25f;
Ajustes.Actual.AntiCheatVelocidadMax = 40f;
await Task.Delay(150);
// Teletransporte imposible (+50 bloques de golpe): el servidor conserva la
// posicion anterior y el estado difundido en Posiciones no salta.
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax + 50, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
bool sinSalto = true;
for (int i = 0; i < 6 && sinSalto; i++)
{
    var pm = await c1.LeerHasta<Posiciones>(timeoutMs: 8000);
    var yo = pm?.Jugadores.FirstOrDefault(j => j.Id == idAna);
    if (yo != null && MathF.Abs(yo.Px - aparicionPriv.Ax) > 1f) sinSalto = false;
}
Comprobar(sinSalto, "anti-cheat: el teletransporte se ignora (la posicion no salta)");
// Movimiento normal (+1 bloque): se acepta y se difunde
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax + 1, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });
bool movAceptado = false;
for (int i = 0; i < 10 && !movAceptado; i++)
{
    var pm = await c1.LeerHasta<Posiciones>(timeoutMs: 8000);
    var yo = pm?.Jugadores.FirstOrDefault(j => j.Id == idAna);
    if (yo != null && MathF.Abs(yo.Px - (aparicionPriv.Ax + 1f)) < 0.01f) movAceptado = true;
}
Comprobar(movAceptado, "anti-cheat: el movimiento normal se acepta");
Ajustes.Actual.AntiCheatSaltoMax = 0f;
Ajustes.Actual.AntiCheatVelocidadMax = 0f;
await c1.Enviar(new Posicion { Px = aparicionPriv.Ax, Py = aparicionPriv.Ay, Pz = aparicionPriv.Az, Ry = 0, Pitch = 0 });

// ---------- chat ----------
Console.WriteLine($"[diag] Bruno antes del chat: conectado={c2.Conectado}, en cola={colaBruno.Count}, tipos={string.Join(",", tiposBruno.Select(kv => kv.Key + "=" + kv.Value))}");
await c1.Enviar(new Chat { Texto = "Â¡Hola a todos!" });
var chat = await EsperarChat(colaBruno);
if (chat == null) Console.WriteLine($"[diag] chat difundido NO llego; conectado={c2.Conectado}, tipos={string.Join(",", tiposBruno.Select(kv => kv.Key + "=" + kv.Value))}");
Comprobar(chat?.Nombre == "Ana" && chat.Texto == "Â¡Hola a todos!", "chat difundido");

// Moderacion basica: los caracteres de control (saltos de linea, bell) se quitan
await c1.Enviar(new Chat { Texto = "linea1\nlinea2\u0007" });
var chatLimpio = await EsperarChat(colaBruno);
if (chatLimpio == null) Console.WriteLine($"[diag] chat limpio NO llego; conectado={c2.Conectado}, tipos={string.Join(",", tiposBruno.Select(kv => kv.Key + "=" + kv.Value))}");
Comprobar(chatLimpio?.Texto == "linea1linea2", "el chat se limpia de caracteres de control");

// ---------- persistencia en memoria ----------
// ---------- autoguardado periodico en disco ----------
Console.WriteLine("Autoguardado: el servidor reescribe los .mundo periodicamente.");
var rutaMundo = Path.Combine(GameServer.CarpetaMundos, idPrivado + ".mundo");
var antesGuardado = File.Exists(rutaMundo) ? File.GetLastWriteTimeUtc(rutaMundo) : DateTime.MinValue;
Ajustes.Actual.AutoguardadoSegundos = 1;
await Task.Delay(2500);
Comprobar(File.Exists(rutaMundo) && File.GetLastWriteTimeUtc(rutaMundo) > antesGuardado,
    "autoguardado: el .mundo se reescribe en disco periodicamente");

// ---------- reconexion: caida dura del socket y reentrada al mismo mundo ----------
// Cubre a nivel de protocolo la reconexion automatica del cliente (0.10.8):
// el socket muere sin Salir, el servidor conserva el mundo, el jugador vuelve
// a conectarse con su nombre, se retransmite el mundo troceado y se restaura
// su inventario persistido.
Console.WriteLine("Reconexion: caida dura del socket y reentrada al mismo mundo.");
// Ana se mueve a un punto conocido antes de la caida: al volver debe estar ahi
var pxEsperado = aparicionPriv.Ax + 3f;
await c1.Enviar(new Posicion { Px = pxEsperado, Py = aparicionPriv.Ay + 2f, Pz = aparicionPriv.Az, Ry = 1f, Pitch = 0 });
await Task.Delay(200);
c1.Cerrar();
await Task.Delay(300);
c1 = await Conectar(puerto);
await c1.Enviar(new Hola { Nombre = "Ana", Version = "1.0" });
await c1.LeerHasta<Bienvenido>();
await c1.LeerHasta<ListaMundos>();
await c1.Enviar(new Unirse { Id = idPrivado, Pin = "123456" });
var resRe = await LeerUnidoCompleto(c1);
var unidoRe = resRe.Unido;
Comprobar(unidoRe != null && unidoRe.Id == idPrivado, $"reconexion: el mundo se retransmite troceado tras la caida ({resRe.Trozos} trozos)");
Comprobar(unidoRe != null && MathF.Abs(unidoRe.Ax - pxEsperado) < 0.5f,
    $"reconexion: vuelve a la posicion guardada (esperada x={pxEsperado:F1}, recibida x={unidoRe?.Ax ?? -999:F1})");
var invRe = await c1.LeerHasta<Inventario>(timeoutMs: 8000);
Comprobar(invRe != null && invRe.Slots.Count > 0, "reconexion: el inventario persistido se restaura");
await Task.Delay(150);
while (await c1.LeerCualquiera(60) != null) { } // drenar notificaciones del rejoin

Console.WriteLine("Persistencia: el mundo vacio sigue existiendo y luego se borra.");
await c1.Enviar(new Salir());
await c2.Enviar(new Salir());
await Task.Delay(200);
while (await c1.LeerCualquiera(60) != null) { } // drenar antes de pedir la lista
await c1.Enviar(new ListarMundos());
var lista1 = await c1.LeerHasta<ListaMundos>();
Comprobar(lista1!.Mundos.Any(m => m.Id == idPrivado), "el mundo privado permanece en memoria sin jugadores");

// Borrar el mundo publico (Ana es la duena)
await c1.Enviar(new BorrarMundo { Id = idMundo });
await Task.Delay(200);
while (await c1.LeerCualquiera(60) != null) { } // drenar la notificacion del borrado
await c1.Enviar(new ListarMundos());
var lista2 = await c1.LeerHasta<ListaMundos>();
Comprobar(!lista2!.Mundos.Any(m => m.Id == idMundo), "el dueno borra su mundo");
Comprobar(lista2.Mundos.Any(m => m.Id == idPrivado), "el mundo de Bruno sigue en memoria");

// ---------- tope de descompresion (bomba gzip) ----------
var bomba = Mundo.Comprimir(new byte[70 * 1024 * 1024]);
bool topeLanzo = false;
try { _ = Mundo.Descomprimir(bomba); }
catch (InvalidDataException) { topeLanzo = true; }
Comprobar(topeLanzo, "la descompresion de mundos tiene tope de seguridad (bomba gzip rechazada)");

// ---------- favoritos de servidores del cliente ----------
// Persistencia JSON de la lista de servidores favoritos (MundoVoxel.Core):
// usa una ruta temporal para no tocar los favoritos reales del usuario.
Console.WriteLine("Favoritos de servidores: persistencia JSON en ruta temporal.");
var rutaFav = Path.Combine(Path.GetTempPath(), "mundovoxel-pruebas-" + Guid.NewGuid().ToString("N"), "servidores.json");
Comprobar(ServidoresFavoritos.Cargar(rutaFav).Count == 0, "favoritos vacios cuando el archivo no existe");
ServidoresFavoritos.AgregarRuta(rutaFav, new ServidorFavorito { Alias = "Casa", Ip = "192.168.1.10", Puerto = 25575 });
ServidoresFavoritos.AgregarRuta(rutaFav, new ServidorFavorito { Alias = "Internet", Ip = "juego.example.com", Puerto = 25575 });
ServidoresFavoritos.AgregarRuta(rutaFav, new ServidorFavorito { Alias = "Casa renombrado", Ip = "192.168.1.10", Puerto = 25575 });
var favoritos = ServidoresFavoritos.Cargar(rutaFav);
Comprobar(favoritos.Count == 2, "agregar dos veces la misma direccion no duplica (solo renombra)");
Comprobar(favoritos[0].Alias == "Casa renombrado" && favoritos[0].Ip == "192.168.1.10", "el alias del favorito existente se actualiza");
Comprobar(favoritos[1].Ip == "juego.example.com" && favoritos[1].Puerto == 25575, "el favorito nuevo conserva ip y puerto");
ServidoresFavoritos.QuitarRuta(rutaFav, "192.168.1.10", 25575);
Comprobar(ServidoresFavoritos.Cargar(rutaFav).Count == 1, "quitar un favorito lo elimina del archivo");
for (int i = 0; i < ServidoresFavoritos.Maximo + 5; i++)
    ServidoresFavoritos.AgregarRuta(rutaFav, new ServidorFavorito { Alias = "S" + i, Ip = "10.0.0." + i, Puerto = 25575 });
Comprobar(ServidoresFavoritos.Cargar(rutaFav).Count == ServidoresFavoritos.Maximo, $"la lista de favoritos respeta el tope de {ServidoresFavoritos.Maximo}");
await File.WriteAllTextAsync(rutaFav, "{ esto no es json ]");
Comprobar(ServidoresFavoritos.Cargar(rutaFav).Count == 0, "un archivo de favoritos danado se trata como lista vacia (sin romper)");
ServidoresFavoritos.QuitarRuta(rutaFav, "10.0.0.0", 25575); // sin efecto: archivo danado, no debe lanzar
Comprobar(true, "quitar sobre un archivo danado no lanza excepcion");
try { Directory.Delete(Path.GetDirectoryName(rutaFav)!, true); } catch { }

// ---------- cierre ----------
c1.Cerrar(); c2.Cerrar();
await servidor.DetenerAsync();

Console.WriteLine();
Console.WriteLine(errores == 0 ? "PRUEBAS SUPERADAS" : $"{errores} PRUEBAS FALLARON");
return errores == 0 ? 0 : 1;

// ------------------------------------------------------------------

// Rompe un bloque enviando golpes hasta que cae (los bloques duros requieren
// varios, ver Objetos.GolpesPara). Devuelve el BloqueCambio de la rotura, el
// ultimo Inventario recibido durante el proceso y los golpes necesarios.
static async Task<(BloqueCambio? Cambio, Inventario? Inv, int Golpes)> RomperHasta(ClientePrueba c, int x, int y, int z, int maxGolpes = 8)
{
    BloqueCambio? cambio = null; Inventario? inv = null;
    int golpes = 0;
    for (int i = 0; i < maxGolpes && cambio == null; i++)
    {
        await c.Enviar(new RomperBloque { X = x, Y = y, Z = z });
        golpes = i + 1;
        var fin = DateTime.UtcNow.AddMilliseconds(1600);
        while (cambio == null && DateTime.UtcNow < fin)
        {
            var m = await c.LeerCualquiera(160);
            if (m == null) break;
            if (m is BloqueCambio bc && bc.X == x && bc.Y == y && bc.Z == z) cambio = bc;
            else if (m is Inventario invm) inv = invm;
        }
    }
    return (cambio, inv, golpes);
}

/// <summary>Espera un Chat en la cola del lector dedicado (p. ej. Bruno),
/// descartando cualquier otro mensaje que llegue antes.</summary>
static async Task<Chat?> EsperarChat(System.Collections.Concurrent.ConcurrentQueue<Mensaje> cola, int timeoutMs = 20000)
{
    var fin = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (DateTime.UtcNow < fin)
    {
        while (cola.TryDequeue(out var m))
        {
            if (m is Chat ch) return ch;
        }
        await Task.Delay(50);
    }
    return null;
}

/// <summary>Lee un Unido completo: el servidor envia primero el Unido sin datos
/// y detras los MundoChunk; reensambla el mundo y devuelve el mensaje con los
/// datos ya unidos (trozos = cantidad de trozos recibidos).</summary>
static async Task<(Unido? Unido, int Trozos)> LeerUnidoCompleto(ClientePrueba c, int timeoutMs = 30000)
{
    var unido = await c.LeerHasta<Unido>(timeoutMs);
    var partes = new List<byte[]>();
    int total = -1;
    var fin = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (unido != null && DateTime.UtcNow < fin)
    {
        var m = await c.LeerHasta<MundoChunk>(timeoutMs);
        if (m == null) break;
        total = m.Total;
        while (partes.Count < m.Indice) partes.Add(Array.Empty<byte>());
        partes.Add(m.Datos);
        if (total > 0 && partes.Count >= total) break;
    }
    if (unido != null && partes.Count > 0) unido.MundoComprimido = partes.SelectMany(p => p).ToArray();
    return (unido, partes.Count);
}

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
    /// <summary>Lee el siguiente frame sin timeout: para lectores dedicados en
    /// segundo plano (un cancel a mitad de frame desincronizaria el stream).</summary>
    public async Task<Mensaje?> LeerSinTimeout()
    {
        try { return await Frames.LeerAsync(_flujo, CancellationToken.None); }
        catch { return null; }
    }
    public bool Conectado => _tcp?.Connected == true;

    public void Cerrar() { try { _tcp.Close(); } catch { } }
}

