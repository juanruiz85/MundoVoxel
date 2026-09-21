namespace MundoVoxel.Core;

/// <summary>
/// Mallas (chunks de render) pendientes de reconstruir, con presupuesto por frame.
///
/// El streaming por proximidad toca regiones enteras de golpe: reconstruirlas
/// todas en el mismo frame da un tiron (sobre todo en Android). En vez de eso se
/// apuntan aqui las mallas afectadas y el bucle de dibujo reconstruye unas pocas
/// por frame, repartiendo el trabajo sin que se note.
///
/// Se puede usar desde dos hilos (uno marca al recibir red, otro saca al
/// dibujar): todas las operaciones van bajo cerrojo.
/// </summary>
public sealed class MallasSucias
{
    readonly object _cerrojo = new();
    readonly HashSet<(int Cx, int Cz)> _pendientes = new();

    public int Pendientes { get { lock (_cerrojo) return _pendientes.Count; } }

    public bool Vacia { get { lock (_cerrojo) return _pendientes.Count == 0; } }

    public void Vaciar() { lock (_cerrojo) _pendientes.Clear(); }

    /// <summary>
    /// Apunta las mallas que tocan el rectangulo de bloques [x0..x1] x [z0..z1],
    /// ensanchado una malla por lado: los vecinos del borde cambian de caras al
    /// aparecer o desaparecer los bloques de al lado.
    /// </summary>
    public void MarcarRectangulo(int x0, int z0, int x1, int z1, int tam, int ancho, int profundo)
    {
        if (tam <= 0 || ancho <= 0 || profundo <= 0) return;
        if (x1 < x0) { int t = x0; x0 = x1; x1 = t; }
        if (z1 < z0) { int t = z0; z0 = z1; z1 = t; }
        int ncx = (ancho + tam - 1) / tam;   // mallas a lo ancho
        int ncz = (profundo + tam - 1) / tam;
        int cx0 = Math.Max(0, x0 / tam - 1), cx1 = Math.Min(ncx - 1, x1 / tam + 1);
        int cz0 = Math.Max(0, z0 / tam - 1), cz1 = Math.Min(ncz - 1, z1 / tam + 1);
        lock (_cerrojo)
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cz = cz0; cz <= cz1; cz++)
                    _pendientes.Add((cx, cz));
    }

    /// <summary>Saca hasta `max` mallas para reconstruir en este frame (las quita de la cola).</summary>
    public List<(int Cx, int Cz)> Sacar(int max)
    {
        var lista = new List<(int Cx, int Cz)>();
        if (max <= 0) return lista;
        lock (_cerrojo)
        {
            foreach (var m in _pendientes)
            {
                lista.Add(m);
                if (lista.Count >= max) break;
            }
            foreach (var m in lista) _pendientes.Remove(m);
        }
        return lista;
    }
}