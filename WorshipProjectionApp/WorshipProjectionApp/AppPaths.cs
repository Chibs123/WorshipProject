using System.IO;

namespace WorshipProjectionApp;

/// <summary>
/// Pasta de dados persistente (%AppData%\WorshipProjectionApp). Sobrevive a rebuilds/clean
/// e funciona mesmo quando a app está instalada numa pasta sem permissões de escrita.
/// Migra automaticamente os ficheiros antigos que estavam junto ao executável.
/// </summary>
public static class AppPaths
{
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorshipProjectionApp");

    private static readonly string LegacyDir = AppDomain.CurrentDomain.BaseDirectory;

    static AppPaths()
    {
        try { Directory.CreateDirectory(DataDir); } catch { }
    }

    public static string File(string name)
    {
        string target = Path.Combine(DataDir, name);
        try
        {
            string legacy = Path.Combine(LegacyDir, name);
            if (!System.IO.File.Exists(target) && System.IO.File.Exists(legacy))
                System.IO.File.Copy(legacy, target);
        }
        catch (Exception ex) { Log($"Migração de {name}", ex); }
        return target;
    }

    public static string Folder(string name)
    {
        string target = Path.Combine(DataDir, name);
        try
        {
            string legacy = Path.Combine(LegacyDir, name);
            if (!Directory.Exists(target) && Directory.Exists(legacy))
                Directory.Move(legacy, target);
        }
        catch (Exception ex) { Log($"Migração da pasta {name}", ex); }
        try { Directory.CreateDirectory(target); } catch { }
        return target;
    }

    public static void Log(string context, Exception ex)
    {
        try
        {
            System.IO.File.AppendAllText(Path.Combine(DataDir, "errors.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}{Environment.NewLine}");
        }
        catch { }
    }
}
