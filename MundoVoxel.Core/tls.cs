using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MundoVoxel.Core;

/// <summary>
/// Cifrado opcional de la conexion (TLS). El servidor usa un certificado
/// autofirmado que se genera solo la primera vez (no hay que comprar ni
/// configurar nada) y el cliente lo acepta la primera vez y recuerda su huella
/// (trust-on-first-use): si el certificado cambia despues, el cliente lo
/// rechaza en vez de aceptarlo en silencio (asi una interceptacion no pasa
/// desapercibida). Sin TLS activado nada de esto se usa y el protocolo sigue
/// siendo el de siempre.
/// </summary>
public static class Tls
{
    /// <summary>Ruta del certificado del servidor (junto a los mundos guardados).</summary>
    public static string RutaPorDefecto => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MundoVoxel", "servidor.pfx");

    /// <summary>Carga el certificado de disco o crea uno autofirmado si no existe
    /// (o si el guardado esta danado). Si no se puede escribir, se queda solo en
    /// memoria: el servidor arranca igual.</summary>
    public static X509Certificate2 CargarOCrear(string? ruta = null)
    {
        ruta ??= RutaPorDefecto;
        if (File.Exists(ruta))
        {
            try { return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(ruta), null); }
            catch { /* danado: se genera uno nuevo */ }
        }
        var cert = CrearAutofirmado();
        try
        {
            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(ruta, cert.Export(X509ContentType.Pkcs12));
        }
        catch { /* sin permiso de escritura: se usa en memoria */ }
        return cert;
    }

    /// <summary>Certificado autofirmado (2048 bits, SHA-256) para localhost, el
    /// nombre del equipo y la IP de bucle local. Vale 2 anios.</summary>
    public static X509Certificate2 CrearAutofirmado(string nombre = "MundoVoxel")
    {
        using var rsa = RSA.Create(2048);
        var peticion = new CertificateRequest($"CN={nombre}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        peticion.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        peticion.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        peticion.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // autenticacion de servidor
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        try { san.AddDnsName(Environment.MachineName); } catch { }
        san.AddIpAddress(IPAddress.Loopback);
        peticion.CertificateExtensions.Add(san.Build());

        var ahora = DateTimeOffset.UtcNow;
        var temporal = peticion.CreateSelfSigned(ahora.AddDays(-1), ahora.AddYears(2));
        // Exportar y volver a importar: la clave privada queda ligada al objeto
        // certificado (no a la instancia de RSA, que se libera al salir).
        var pfx = temporal.Export(X509ContentType.Pkcs12);
        temporal.Dispose();
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }

    /// <summary>Huella SHA-256 en hexadecimal "AA:BB:...": es lo que el cliente
    /// recuerda para detectar un cambio de certificado.</summary>
    public static string Huella(X509Certificate2 certificado) => Huella(certificado.RawData);

    public static string Huella(byte[] datos)
    {
        var hash = SHA256.HashData(datos);
        return string.Join(":", hash.Select(b => b.ToString("X2")));
    }

    /// <summary>Comparacion del trust-on-first-use: no hay huella guardada
    /// (primera vez) o coincide con la vista. Devuelve false solo si la huella
    /// CAMBIO, que es lo que delata una suplantacion.</summary>
    public static bool HuellaAceptable(string? huellaEsperada, string huellaVista)
        => string.IsNullOrEmpty(huellaEsperada) || string.Equals(huellaEsperada, huellaVista, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nombre de la preferencia donde el cliente recuerda la huella de
    /// cada servidor.</summary>
    public static string ClaveHuella(string ip, int puerto) => $"huella_tls_{ip}_{puerto}";
}