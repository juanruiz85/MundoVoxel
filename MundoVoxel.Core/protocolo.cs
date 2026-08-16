using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MundoVoxel.Core;

/// <summary>Mensajes del protocolo multijugador (JSON con discriminador "Tipo").</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Tipo")]
[JsonDerivedType(typeof(Hola), "Hola")]
[JsonDerivedType(typeof(Bienvenido), "Bienvenido")]
[JsonDerivedType(typeof(ListarMundos), "ListarMundos")]
[JsonDerivedType(typeof(ListaMundos), "ListaMundos")]
[JsonDerivedType(typeof(InfoMundo), "InfoMundo")]
[JsonDerivedType(typeof(CrearMundo), "CrearMundo")]
[JsonDerivedType(typeof(MundoCreado), "MundoCreado")]
[JsonDerivedType(typeof(Unirse), "Unirse")]
[JsonDerivedType(typeof(Unido), "Unido")]
[JsonDerivedType(typeof(ErrorServidor), "Error")]
[JsonDerivedType(typeof(Salir), "Salir")]
[JsonDerivedType(typeof(JugadorEntro), "JugadorEntro")]
[JsonDerivedType(typeof(JugadorSalio), "JugadorSalio")]
[JsonDerivedType(typeof(Posicion), "Posicion")]
[JsonDerivedType(typeof(Posiciones), "Posiciones")]
[JsonDerivedType(typeof(RomperBloque), "RomperBloque")]
[JsonDerivedType(typeof(ColocarBloque), "ColocarBloque")]
[JsonDerivedType(typeof(BloqueCambio), "BloqueCambio")]
[JsonDerivedType(typeof(Chat), "Chat")]
[JsonDerivedType(typeof(BorrarMundo), "BorrarMundo")]
[JsonDerivedType(typeof(MundoBorrado), "MundoBorrado")]
[JsonDerivedType(typeof(Mobs), "Mobs")]
[JsonDerivedType(typeof(GolpearMob), "GolpearMob")]
[JsonDerivedType(typeof(Drops), "Drops")]
[JsonDerivedType(typeof(Inventario), "Inventario")]
[JsonDerivedType(typeof(Craftear), "Craftear")]
[JsonDerivedType(typeof(Cocinar), "Cocinar")]
[JsonDerivedType(typeof(SoltarItem), "SoltarItem")]
[JsonDerivedType(typeof(UsarBloque), "UsarBloque")]
[JsonDerivedType(typeof(SeleccionarSlot), "SeleccionarSlot")]
[JsonDerivedType(typeof(TiempoMundo), "TiempoMundo")]
[JsonDerivedType(typeof(JugadorSalud), "JugadorSalud")]
[JsonDerivedType(typeof(OxigenoMsg), "Oxigeno")]
[JsonDerivedType(typeof(MuerteInfo), "MuerteInfo")]
[JsonDerivedType(typeof(ModoEspectador), "ModoEspectador")]
[JsonDerivedType(typeof(Respawn), "Respawn")]
[JsonDerivedType(typeof(AbrirCofre), "AbrirCofre")]
[JsonDerivedType(typeof(CofreAbierto), "CofreAbierto")]
[JsonDerivedType(typeof(PonerEnCofre), "PonerEnCofre")]
[JsonDerivedType(typeof(SacarDeCofre), "SacarDeCofre")]
public abstract class Mensaje
{
}

public sealed class Hola : Mensaje { public string Nombre { get; set; } = ""; public string Version { get; set; } = "1.0"; }
public sealed class Bienvenido : Mensaje { public int IdJugador { get; set; } public string NombreServidor { get; set; } = ""; }
public sealed class ListarMundos : Mensaje { }
public sealed class ListaMundos : Mensaje { public List<InfoMundo> Mundos { get; set; } = new(); }
public sealed class InfoMundo : Mensaje
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Dueno { get; set; } = "";
    public int IdDueno { get; set; }
    public bool Abierto { get; set; }
    public int Jugadores { get; set; }
    public int MaxJugadores { get; set; }
}
public sealed class CrearMundo : Mensaje { public string Nombre { get; set; } = ""; public string? Pin { get; set; } public bool Abierto { get; set; } public int Semilla { get; set; } public float HoraInicial { get; set; } = -1f; }
public sealed class MundoCreado : Mensaje { public string Id { get; set; } = ""; }
public sealed class Unirse : Mensaje { public string Id { get; set; } = ""; public string? Pin { get; set; } }
public sealed class Unido : Mensaje
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Dueno { get; set; } = "";
    public int IdDueno { get; set; }
    public byte[] MundoComprimido { get; set; } = Array.Empty<byte>();
    public float Ax { get; set; } public float Ay { get; set; } public float Az { get; set; }
}
public sealed class ErrorServidor : Mensaje { public string Codigo { get; set; } = ""; public string Mensaje { get; set; } = ""; }
public sealed class Salir : Mensaje { }
public sealed class JugadorEntro : Mensaje { public int Id { get; set; } public string Nombre { get; set; } = ""; public float Px { get; set; } public float Py { get; set; } public float Pz { get; set; } }
public sealed class JugadorSalio : Mensaje { public int Id { get; set; } public string Nombre { get; set; } = ""; }
public sealed class Posicion : Mensaje { public int Id { get; set; } public float Px { get; set; } public float Py { get; set; } public float Pz { get; set; } public float Ry { get; set; } public float Pitch { get; set; } }
public sealed class Posiciones : Mensaje { public List<Posicion> Jugadores { get; set; } = new(); }
public sealed class RomperBloque : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } }
public sealed class ColocarBloque : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } public ushort Bloque { get; set; } }
public sealed class BloqueCambio : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } public ushort Bloque { get; set; } }
public sealed class Chat : Mensaje { public string Nombre { get; set; } = ""; public string Texto { get; set; } = ""; }
public sealed class BorrarMundo : Mensaje { public string Id { get; set; } = ""; }
public sealed class MundoBorrado : Mensaje { public string Id { get; set; } = ""; }
public sealed class MobEstado { public int Id { get; set; } public byte Tipo { get; set; } public float Px { get; set; } public float Py { get; set; } public float Pz { get; set; } public float Ry { get; set; } public int Salud { get; set; } public int MaxSalud { get; set; } }
public sealed class Mobs : Mensaje { public List<MobEstado> Lista { get; set; } = new(); }
public sealed class GolpearMob : Mensaje { public int Id { get; set; } }
public sealed class DropEstado { public int Id { get; set; } public ushort Material { get; set; } public float Px { get; set; } public float Py { get; set; } public float Pz { get; set; } }
public sealed class Drops : Mensaje { public List<DropEstado> Lista { get; set; } = new(); }
public sealed class SlotEstado { public ushort Material { get; set; } public int Cantidad { get; set; } }
public sealed class Inventario : Mensaje { public List<SlotEstado> Slots { get; set; } = new(); }
public sealed class Craftear : Mensaje { public int Receta { get; set; } }
public sealed class Cocinar : Mensaje { public int Receta { get; set; } }
public sealed class SoltarItem : Mensaje { public int Slot { get; set; } }
public sealed class UsarBloque : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } }
public sealed class SeleccionarSlot : Mensaje { public int Slot { get; set; } public ushort Material { get; set; } }
public sealed class TiempoMundo : Mensaje { public float Hora { get; set; } }
public sealed class JugadorSalud : Mensaje { public int Salud { get; set; } public int MaxSalud { get; set; } }
public sealed class OxigenoMsg : Mensaje { public float Oxigeno { get; set; } public float MaxOxigeno { get; set; } }
public sealed class MuerteInfo : Mensaje { public string Causa { get; set; } = ""; }
public sealed class ModoEspectador : Mensaje { public bool Activo { get; set; } }
public sealed class Respawn : Mensaje { public float Px { get; set; } public float Py { get; set; } public float Pz { get; set; } }
// Cofres: abrir (uso) y mover items entre el inventario y el cofre
public sealed class AbrirCofre : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } }
public sealed class CofreAbierto : Mensaje { public List<SlotEstado> Slots { get; set; } = new(); }
public sealed class PonerEnCofre : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } public ushort Material { get; set; } public int Cantidad { get; set; } }
public sealed class SacarDeCofre : Mensaje { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } public int Slot { get; set; } }

public static class Protocolo
{
    static readonly JsonSerializerOptions Opciones = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static byte[] Codificar(Mensaje m)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(m, Opciones);
        var frame = new byte[4 + json.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, json.Length);
        json.CopyTo(frame, 4);
        return frame;
    }

    public static Mensaje? Decodificar(byte[] frame) => JsonSerializer.Deserialize<Mensaje>(frame, Opciones);
}

/// <summary>Lectura de tramas con prefijo de longitud desde un NetworkStream.</summary>
public static class Frames
{
    const int MaxTrama = 64 * 1024 * 1024;

    public static async Task<Mensaje?> LeerAsync(NetworkStream flujo, CancellationToken ct)
    {
        var cab = new byte[4];
        int leidos = 0;
        while (leidos < 4)
        {
            int n = await flujo.ReadAsync(cab.AsMemory(leidos, 4 - leidos), ct);
            if (n <= 0) return null;
            leidos += n;
        }
        int len = BinaryPrimitives.ReadInt32LittleEndian(cab);
        if (len < 0 || len > MaxTrama) return null;
        var cuerpo = new byte[len];
        leidos = 0;
        while (leidos < len)
        {
            int n = await flujo.ReadAsync(cuerpo.AsMemory(leidos, len - leidos), ct);
            if (n <= 0) return null;
            leidos += n;
        }
        return Protocolo.Decodificar(cuerpo);
    }
}

