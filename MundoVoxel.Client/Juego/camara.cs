using System.Numerics;

namespace MundoVoxel.Client.Juego;

public sealed class Camara
{
    public Vector3 Pos { get; set; }
    public float Yaw { get; set; }      // radianes (rotación horizontal)
    public float Pitch { get; set; }    // radianes (rotación vertical)

    public Vector3 Adelante => new(
        MathF.Sin(Yaw) * MathF.Cos(Pitch),
        MathF.Sin(Pitch),
        -MathF.Cos(Yaw) * MathF.Cos(Pitch));

    public Vector3 Derecha => new(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
}
