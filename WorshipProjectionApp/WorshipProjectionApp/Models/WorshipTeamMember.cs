using System.IO;
using System.Text.Json;

namespace WorshipProjectionApp.Models;

public class WorshipTeamMember
{
    public string Name { get; set; } = string.Empty;
    public string Microphone { get; set; } = "1";
    public string InEarReturn { get; set; } = "1";
    public string Role { get; set; } = "Voz"; // Voz, Violão, Teclado, Baixo, Bateria, etc.
}

public static class WorshipTeamCatalog
{
    private static readonly string CatalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worship_team_catalog.json");

    public static List<WorshipTeamMember> LoadCatalog()
    {
        try
        {
            if (File.Exists(CatalogPath))
            {
                string json = File.ReadAllText(CatalogPath);
                return JsonSerializer.Deserialize<List<WorshipTeamMember>>(json) ?? GetDefaults();
            }
        }
        catch { }

        var def = GetDefaults();
        SaveCatalog(def);
        return def;
    }

    public static void SaveCatalog(List<WorshipTeamMember> members)
    {
        try
        {
            string json = JsonSerializer.Serialize(members, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CatalogPath, json);
        }
        catch { }
    }

    private static List<WorshipTeamMember> GetDefaults()
    {
        return new List<WorshipTeamMember>
        {
            new() { Name = "Vocal Principal", Microphone = "Mic 1", InEarReturn = "Aux 1", Role = "Líder" },
            new() { Name = "Backing Vocal 1", Microphone = "Mic 2", InEarReturn = "Aux 2", Role = "Voz" },
            new() { Name = "Guitarra Acústica", Microphone = "Line 1", InEarReturn = "Aux 3", Role = "Violão" },
            new() { Name = "Teclado", Microphone = "Line 2", InEarReturn = "Aux 4", Role = "Teclas" }
        };
    }
}