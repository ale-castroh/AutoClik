using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Autoclicker.Persistence;

namespace Autoclicker.Interop;

/// <summary>
/// Buscar actualizaciones (2.7). Apagado por defecto; con él apagado AutoClik no se conecta a nada.
/// Seguridad:
/// - Solo pregunta a la API de GitHub (HTTPS, certificado comprobado por Windows) cuál es la última versión.
/// - De la respuesta solo lee "tag_name", y solo si es un número de versión (v1.2.3). Nada más se usa.
/// - Nunca descarga ni instala nada: el aviso abre una dirección FIJA (<see cref="ReleasesPage"/>), escrita aquí,
///   no tomada de la respuesta.
/// - No envía ningún dato del usuario: solo una petición GET con "AutoClik/x.y.z" como agente.
/// </summary>
public static class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/ale-castroh/AutoClik/releases/latest";
    public const string ReleasesPage = "https://github.com/ale-castroh/AutoClik/releases";
    private const int MaxBytes = 512 * 1024;

    /// <summary>Solo pruebas (--update-test=archivo.json): se lee ese archivo local en vez de preguntar a GitHub.</summary>
    public static string? TestFile { get; set; }

    private static readonly Regex VersionPattern = new(@"^v?(\d{1,4})\.(\d{1,4})\.(\d{1,4})$", RegexOptions.CultureInvariant);

    public static Version Current
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>"v2.8.0" o "2.8.0" → 2.8.0; cualquier otra cosa → null.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (text == null) return null;
        var m = VersionPattern.Match(text.Trim());
        if (!m.Success) return null;
        return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
    }

    /// <summary>
    /// La última versión publicada (en un hilo aparte). Null si no se pudo saber (sin internet, respuesta rara...):
    /// nunca lanza.
    /// </summary>
    public static Task<Version?> LatestAsync() => Task.Run(() =>
    {
        try
        {
            string json = TestFile != null ? ReadLimited(File.OpenRead(TestFile)) : Download();
            var j = JsonObj.From(MiniJson.Parse(json));
            if (j.Bool("draft", false) || j.Bool("prerelease", false)) return null;
            return ParseVersion(j.Str("tag_name"));
        }
        catch (Exception ex) when (ex is WebException or IOException or FormatException or InvalidCastException
                                       or UnauthorizedAccessException or NotSupportedException or OverflowException
                                       or ArgumentException or InvalidOperationException)
        {
            ErrorLog.Write("buscar actualizaciones", ex);
            return null;
        }
    });

    private static string Download()
    {
        // .NET Framework 4.8 puede no ofrecer TLS 1.2 por defecto en algunos Windows: pedirlo explícitamente
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
        request.Method = "GET";
        request.UserAgent = "AutoClik/" + Current.ToString(3);
        request.Accept = "application/vnd.github+json";
        request.Timeout = 10_000;
        request.ReadWriteTimeout = 10_000;
        request.AllowAutoRedirect = false;  // solo la dirección de arriba
        request.CookieContainer = null;
        request.UseDefaultCredentials = false;
        using var response = (HttpWebResponse)request.GetResponse();
        if (response.StatusCode != HttpStatusCode.OK || response.ResponseUri.Host != "api.github.com")
            throw new InvalidOperationException("Respuesta inesperada: " + response.StatusCode);
        return ReadLimited(response.GetResponseStream()!);
    }

    /// <summary>Lee como mucho <see cref="MaxBytes"/>: una respuesta enorme no puede llenar la memoria.</summary>
    private static string ReadLimited(Stream stream)
    {
        using (stream)
        {
            var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) throw new InvalidOperationException("Respuesta demasiado grande");
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
