namespace NovaMac.Models;

public class MacOSSubVersion
{
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string InstallerPackageUrl { get; set; } = string.Empty;
    public bool IsVerifiedAvailable { get; set; } = false;
    public string OsType { get; set; } = "default";
    public bool IsBeta { get; set; } = false;
    public string Description { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

public class MacOSRelease
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Codename { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string Mlb { get; set; } = "00000000000000000";
    public string OsType { get; set; } = "default";
    public string CpuProfile { get; set; } = "Cascadelake-Server";
    public string SmbiosModel { get; set; } = "MacPro7,1";
    public int RecommendedRamGb { get; set; } = 8;
    public int RecommendedDiskGb { get; set; } = 80;
    public string BadgeText { get; set; } = "Estável";
    public string AccentHex { get; set; } = "#0A84FF";
    public string SecondaryHex { get; set; } = "#5E5CE6";
    public bool RequiresAvx2 { get; set; } = true;
    public bool IsFutureOrBeta { get; set; } = false;
    public List<MacOSSubVersion> SubVersions { get; set; } = [];

    public static List<MacOSSubVersion> GetKnownSubVersionsForRelease(string releaseId) => releaseId switch
    {
        "macos-27" =>
        [
            new MacOSSubVersion { Version = "27.0", DisplayName = "🟢 macOS 27.0 (Preview Oficial Estável)", OsType = "default", IsBeta = false, Description = "Versão experimental para desenvolvimento de próxima geração" },
            new MacOSSubVersion { Version = "27.0 Beta", DisplayName = "🟡 macOS 27.0 Beta (Developer Seed • Latest)", OsType = "latest", IsBeta = true, Description = "Preview experimental da próxima geração do macOS" }
        ],
        "macos-26-tahoe" =>
        [
            new MacOSSubVersion { Version = "26.0", DisplayName = "🟢 macOS 26.0 (Estável Final • Liquid Glass)", OsType = "default", IsBeta = false, Description = "Build final estável com nova interface Liquid Glass" },
            new MacOSSubVersion { Version = "26.0 Beta", DisplayName = "🟡 macOS 26.0 Beta (Developer Seed / Beta • Latest)", OsType = "latest", IsBeta = true, Description = "Build de teste com recursos experimentais ativados" }
        ],
        "macos-15-sequoia" =>
        [
            new MacOSSubVersion { Version = "15.6", DisplayName = "🟢 macOS 15.6 (Final Estável • Build 24G84)", OsType = "default", IsBeta = false, Description = "Última versão estável do macOS Sequoia recomendada" },
            new MacOSSubVersion { Version = "15.5", DisplayName = "🟢 macOS 15.5 (Estável • Build 24F79)", OsType = "default", IsBeta = false, Description = "Atualização de segurança e recursos estáveis" },
            new MacOSSubVersion { Version = "15.4.1", DisplayName = "🟢 macOS 15.4.1 (Estável • Build 24E263)", OsType = "default", IsBeta = false, Description = "Versão oficial estável com correções de segurança recentes" },
            new MacOSSubVersion { Version = "15.4", DisplayName = "🟢 macOS 15.4 (Estável • Build 24E248)", OsType = "default", IsBeta = false, Description = "Versão estável com compatibilidade comprovada" },
            new MacOSSubVersion { Version = "15.3.1", DisplayName = "🟢 macOS 15.3.1 (Estável • Build 24D70)", OsType = "default", IsBeta = false, Description = "Atualização estável intermediária" },
            new MacOSSubVersion { Version = "15.3", DisplayName = "🟢 macOS 15.3 (Estável • Build 24D60)", OsType = "default", IsBeta = false, Description = "Versão estável" },
            new MacOSSubVersion { Version = "15.2", DisplayName = "🟢 macOS 15.2 (Estável • Build 24C101)", OsType = "default", IsBeta = false, Description = "Suporte a recursos estendidos de sistema" },
            new MacOSSubVersion { Version = "15.1.1", DisplayName = "🟢 macOS 15.1.1 (Estável • Build 24B2083)", OsType = "default", IsBeta = false, Description = "Patch de correções pontuais" },
            new MacOSSubVersion { Version = "15.1", DisplayName = "🟢 macOS 15.1 (Estável • Build 24B83)", OsType = "default", IsBeta = false, Description = "Primeira grande atualização do Sequoia" },
            new MacOSSubVersion { Version = "15.0.1", DisplayName = "🟢 macOS 15.0.1 (Estável • Build 24A348)", OsType = "default", IsBeta = false, Description = "Correções imediatas pós-lançamento" },
            new MacOSSubVersion { Version = "15.0", DisplayName = "🟢 macOS 15.0 (Release Inicial • Build 24A335)", OsType = "default", IsBeta = false, Description = "Versão original de lançamento do macOS Sequoia" },
            new MacOSSubVersion { Version = "15.x Beta", DisplayName = "🟡 macOS 15 Sequoia Beta (Developer Seed • Latest)", OsType = "latest", IsBeta = true, Description = "Canal Developer Seed / Beta oficial da Apple" }
        ],
        "macos-14-sonoma" =>
        [
            new MacOSSubVersion { Version = "14.7.4", DisplayName = "🟢 macOS 14.7.4 (Final Estável • Build 23H420)", OsType = "default", IsBeta = false, Description = "Versão estável final recomendada do Sonoma" },
            new MacOSSubVersion { Version = "14.7.3", DisplayName = "🟢 macOS 14.7.3 (Estável • Build 23H335)", OsType = "default", IsBeta = false, Description = "Atualização de segurança e estabilidade" },
            new MacOSSubVersion { Version = "14.7.2", DisplayName = "🟢 macOS 14.7.2 (Estável • Build 23H311)", OsType = "default", IsBeta = false, Description = "Atualização cumulativa estável" },
            new MacOSSubVersion { Version = "14.7.1", DisplayName = "🟢 macOS 14.7.1 (Estável • Build 23H222)", OsType = "default", IsBeta = false, Description = "Atualização estável com correções" },
            new MacOSSubVersion { Version = "14.7", DisplayName = "🟢 macOS 14.7 (Estável • Build 23H124)", OsType = "default", IsBeta = false, Description = "Versão estável" },
            new MacOSSubVersion { Version = "14.6.1", DisplayName = "🟢 macOS 14.6.1 (Estável • Build 23G93)", OsType = "default", IsBeta = false, Description = "Versão madura e estável" },
            new MacOSSubVersion { Version = "14.6", DisplayName = "🟢 macOS 14.6 (Estável • Build 23G80)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.5", DisplayName = "🟢 macOS 14.5 (Estável • Build 23F79)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.4.1", DisplayName = "🟢 macOS 14.4.1 (Estável • Build 23E224)", OsType = "default", IsBeta = false, Description = "Correção de drivers e subsistemas" },
            new MacOSSubVersion { Version = "14.4", DisplayName = "🟢 macOS 14.4 (Estável • Build 23E214)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.3.1", DisplayName = "🟢 macOS 14.3.1 (Estável • Build 23D60)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.3", DisplayName = "🟢 macOS 14.3 (Estável • Build 23D56)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.2.1", DisplayName = "🟢 macOS 14.2.1 (Estável • Build 23C71)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.2", DisplayName = "🟢 macOS 14.2 (Estável • Build 23C64)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "14.1.2", DisplayName = "🟢 macOS 14.1.2 (Estável • Build 23B92)", OsType = "default", IsBeta = false, Description = "Correções pontuais" },
            new MacOSSubVersion { Version = "14.1.1", DisplayName = "🟢 macOS 14.1.1 (Estável • Build 23B81)", OsType = "default", IsBeta = false, Description = "Correções pontuais" },
            new MacOSSubVersion { Version = "14.1", DisplayName = "🟢 macOS 14.1 (Estável • Build 23B74)", OsType = "default", IsBeta = false, Description = "Primeira atualização estável do Sonoma" },
            new MacOSSubVersion { Version = "14.0", DisplayName = "🟢 macOS 14.0 (Release Inicial • Build 23A344)", OsType = "default", IsBeta = false, Description = "Lançamento inicial oficial do macOS Sonoma" },
            new MacOSSubVersion { Version = "14.x Beta", DisplayName = "🟡 macOS 14 Sonoma Beta (Developer Seed • Latest)", OsType = "latest", IsBeta = true, Description = "Canal Developer Seed para Sonoma" }
        ],
        "macos-13-ventura" =>
        [
            new MacOSSubVersion { Version = "13.7.4", DisplayName = "🟢 macOS 13.7.4 (Final Estável • Build 22H420)", OsType = "default", IsBeta = false, Description = "Versão estável final do Ventura" },
            new MacOSSubVersion { Version = "13.7.3", DisplayName = "🟢 macOS 13.7.3 (Estável • Build 22H335)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "13.7.2", DisplayName = "🟢 macOS 13.7.2 (Estável • Build 22H311)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "13.7.1", DisplayName = "🟢 macOS 13.7.1 (Estável • Build 22H221)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "13.7", DisplayName = "🟢 macOS 13.7 (Estável • Build 22H123)", OsType = "default", IsBeta = false, Description = "Versão estável" },
            new MacOSSubVersion { Version = "13.6.9", DisplayName = "🟢 macOS 13.6.9 (Estável • Build 22G830)", OsType = "default", IsBeta = false, Description = "Alta estabilidade e compatibilidade" },
            new MacOSSubVersion { Version = "13.6.8", DisplayName = "🟢 macOS 13.6.8 (Estável • Build 22G820)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.7", DisplayName = "🟢 macOS 13.6.7 (Estável • Build 22G720)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.6", DisplayName = "🟢 macOS 13.6.6 (Estável • Build 22G630)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.5", DisplayName = "🟢 macOS 13.6.5 (Estável • Build 22G621)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.4", DisplayName = "🟢 macOS 13.6.4 (Estável • Build 22G513)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.3", DisplayName = "🟢 macOS 13.6.3 (Estável • Build 22G436)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.2", DisplayName = "🟢 macOS 13.6.2 (Estável • Build 22G320)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6.1", DisplayName = "🟢 macOS 13.6.1 (Estável • Build 22G313)", OsType = "default", IsBeta = false, Description = "Atualização estável" },
            new MacOSSubVersion { Version = "13.6", DisplayName = "🟢 macOS 13.6 (Estável • Build 22G120)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.5.2", DisplayName = "🟢 macOS 13.5.2 (Estável • Build 22G91)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.5.1", DisplayName = "🟢 macOS 13.5.1 (Estável • Build 22G90)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.5", DisplayName = "🟢 macOS 13.5 (Estável • Build 22G74)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.4.1", DisplayName = "🟢 macOS 13.4.1 (Estável • Build 22F82)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.4", DisplayName = "🟢 macOS 13.4 (Estável • Build 22F66)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.3.1", DisplayName = "🟢 macOS 13.3.1 (Estável • Build 22E261)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.3", DisplayName = "🟢 macOS 13.3 (Estável • Build 22E252)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.2.1", DisplayName = "🟢 macOS 13.2.1 (Estável • Build 22D68)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.2", DisplayName = "🟢 macOS 13.2 (Estável • Build 22D49)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.1", DisplayName = "🟢 macOS 13.1 (Estável • Build 22C65)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "13.0.1", DisplayName = "🟢 macOS 13.0.1 (Estável • Build 22A400)", OsType = "default", IsBeta = false, Description = "Correção pós-lançamento" },
            new MacOSSubVersion { Version = "13.0", DisplayName = "🟢 macOS 13.0 (Release Inicial • Build 22A380)", OsType = "default", IsBeta = false, Description = "Lançamento inicial oficial do Ventura" },
            new MacOSSubVersion { Version = "13.x Beta", DisplayName = "🟡 macOS 13 Ventura Beta (Developer Seed)", OsType = "latest", IsBeta = true, Description = "Canal Developer Seed para Ventura" }
        ],
        "macos-12-monterey" =>
        [
            new MacOSSubVersion { Version = "12.7.6", DisplayName = "🟢 macOS 12.7.6 (Final Estável • Build 21H1320)", OsType = "default", IsBeta = false, Description = "Versão definitiva e altamente recomendada" },
            new MacOSSubVersion { Version = "12.7.5", DisplayName = "🟢 macOS 12.7.5 (Estável • Build 21H1222)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.7.4", DisplayName = "🟢 macOS 12.7.4 (Estável • Build 21H1123)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.7.3", DisplayName = "🟢 macOS 12.7.3 (Estável • Build 21H1015)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.7.2", DisplayName = "🟢 macOS 12.7.2 (Estável • Build 21H1006)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.7.1", DisplayName = "🟢 macOS 12.7.1 (Estável • Build 21H906)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.7", DisplayName = "🟢 macOS 12.7 (Estável • Build 21H818)", OsType = "default", IsBeta = false, Description = "Versão estável" },
            new MacOSSubVersion { Version = "12.6.9", DisplayName = "🟢 macOS 12.6.9 (Estável • Build 21G726)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "12.6.8", DisplayName = "🟢 macOS 12.6.8 (Estável • Build 21G725)", OsType = "default", IsBeta = false, Description = "Versão intermediária estável" },
            new MacOSSubVersion { Version = "12.6.7", DisplayName = "🟢 macOS 12.6.7 (Estável • Build 21G651)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.6", DisplayName = "🟢 macOS 12.6.6 (Estável • Build 21G646)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.5", DisplayName = "🟢 macOS 12.6.5 (Estável • Build 21G531)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.4", DisplayName = "🟢 macOS 12.6.4 (Estável • Build 21G526)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.3", DisplayName = "🟢 macOS 12.6.3 (Estável • Build 21G419)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.2", DisplayName = "🟢 macOS 12.6.2 (Estável • Build 21G320)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6.1", DisplayName = "🟢 macOS 12.6.1 (Estável • Build 21G217)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.6", DisplayName = "🟢 macOS 12.6 (Estável • Build 21G115)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.5.1", DisplayName = "🟢 macOS 12.5.1 (Estável • Build 21G83)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "12.5", DisplayName = "🟢 macOS 12.5 (Estável • Build 21G72)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "12.4", DisplayName = "🟢 macOS 12.4 (Estável • Build 21F79)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "12.3.1", DisplayName = "🟢 macOS 12.3.1 (Estável • Build 21E258)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.3", DisplayName = "🟢 macOS 12.3 (Estável • Build 21E230)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.2.1", DisplayName = "🟢 macOS 12.2.1 (Estável • Build 21D62)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.2", DisplayName = "🟢 macOS 12.2 (Estável • Build 21D49)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "12.1", DisplayName = "🟢 macOS 12.1 (Estável • Build 21C52)", OsType = "default", IsBeta = false, Description = "Primeira atualização do Monterey" },
            new MacOSSubVersion { Version = "12.0.1", DisplayName = "🟢 macOS 12.0.1 (Release Inicial • Build 21A559)", OsType = "default", IsBeta = false, Description = "Lançamento inicial do macOS Monterey" }
        ],
        "macos-11-bigsur" =>
        [
            new MacOSSubVersion { Version = "11.7.10", DisplayName = "🟢 macOS 11.7.10 (Final Estável • Build 20G1427)", OsType = "default", IsBeta = false, Description = "Versão final leve para PCs com hardware modesto" },
            new MacOSSubVersion { Version = "11.7.9", DisplayName = "🟢 macOS 11.7.9 (Estável • Build 20G1426)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.8", DisplayName = "🟢 macOS 11.7.8 (Estável • Build 20G1351)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.7", DisplayName = "🟢 macOS 11.7.7 (Estável • Build 20G1345)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.6", DisplayName = "🟢 macOS 11.7.6 (Estável • Build 20G1231)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.5", DisplayName = "🟢 macOS 11.7.5 (Estável • Build 20G1225)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.4", DisplayName = "🟢 macOS 11.7.4 (Estável • Build 20G1120)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.3", DisplayName = "🟢 macOS 11.7.3 (Estável • Build 20G1116)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.2", DisplayName = "🟢 macOS 11.7.2 (Estável • Build 20G1020)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7.1", DisplayName = "🟢 macOS 11.7.1 (Estável • Build 20G918)", OsType = "default", IsBeta = false, Description = "Atualização de segurança" },
            new MacOSSubVersion { Version = "11.7", DisplayName = "🟢 macOS 11.7 (Estável • Build 20G817)", OsType = "default", IsBeta = false, Description = "Versão estável" },
            new MacOSSubVersion { Version = "11.6.8", DisplayName = "🟢 macOS 11.6.8 (Estável • Build 20G730)", OsType = "default", IsBeta = false, Description = "Versão intermediária estável" },
            new MacOSSubVersion { Version = "11.6.7", DisplayName = "🟢 macOS 11.6.7 (Estável • Build 20G630)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.6", DisplayName = "🟢 macOS 11.6.6 (Estável • Build 20G624)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.5", DisplayName = "🟢 macOS 11.6.5 (Estável • Build 20G417)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.4", DisplayName = "🟢 macOS 11.6.4 (Estável • Build 20G415)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.3", DisplayName = "🟢 macOS 11.6.3 (Estável • Build 20G413)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.2", DisplayName = "🟢 macOS 11.6.2 (Estável • Build 20G314)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6.1", DisplayName = "🟢 macOS 11.6.1 (Estável • Build 20G224)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.6", DisplayName = "🟢 macOS 11.6 (Estável • Build 20G165)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.5.2", DisplayName = "🟢 macOS 11.5.2 (Estável • Build 20G95)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "11.5.1", DisplayName = "🟢 macOS 11.5.1 (Estável • Build 20G80)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "11.5", DisplayName = "🟢 macOS 11.5 (Estável • Build 20G71)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "11.4", DisplayName = "🟢 macOS 11.4 (Estável • Build 20F71)", OsType = "default", IsBeta = false, Description = "Versão intermediária" },
            new MacOSSubVersion { Version = "11.3.1", DisplayName = "🟢 macOS 11.3.1 (Estável • Build 20E241)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.3", DisplayName = "🟢 macOS 11.3 (Estável • Build 20E232)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.2.3", DisplayName = "🟢 macOS 11.2.3 (Estável • Build 20D91)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.2.2", DisplayName = "🟢 macOS 11.2.2 (Estável • Build 20D80)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.2.1", DisplayName = "🟢 macOS 11.2.1 (Estável • Build 20D75)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.2", DisplayName = "🟢 macOS 11.2 (Estável • Build 20D64)", OsType = "default", IsBeta = false, Description = "Atualização intermediária" },
            new MacOSSubVersion { Version = "11.1", DisplayName = "🟢 macOS 11.1 (Estável • Build 20C69)", OsType = "default", IsBeta = false, Description = "Primeira atualização do Big Sur" },
            new MacOSSubVersion { Version = "11.0.1", DisplayName = "🟢 macOS 11.0.1 (Release Inicial • Build 20B29)", OsType = "default", IsBeta = false, Description = "Lançamento inicial do macOS Big Sur" }
        ],
        _ => []
    };

    public static List<MacOSRelease> GetCatalog() =>
    [
        new MacOSRelease
        {
            Id = "macos-27",
            Name = "macOS 27",
            Version = "27.0",
            Codename = "Next-Gen Release",
            Subtitle = "Suporte experimental x86_64/OpenCore 1.0.4+ com SMBIOS MacPro7,1 e patches AVX2/Cryptex",
            BoardId = "Mac-937A206F2EE63C01",
            Mlb = "00000000000000000",
            OsType = "latest",
            CpuProfile = "Cascadelake-Server",
            SmbiosModel = "MacPro7,1",
            RecommendedRamGb = 12,
            RecommendedDiskGb = 100,
            BadgeText = "Preview 27",
            AccentHex = "#BF5AF2",
            SecondaryHex = "#FF375F",
            RequiresAvx2 = true,
            IsFutureOrBeta = true,
            SubVersions = GetKnownSubVersionsForRelease("macos-27")
        },
        new MacOSRelease
        {
            Id = "macos-26-tahoe",
            Name = "macOS 26 Tahoe",
            Version = "26.0",
            Codename = "Tahoe",
            Subtitle = "Design Liquid Glass • Requer CPU x86_64 com AVX2 (MacPro7,1 SMBIOS + OpenCore)",
            BoardId = "Mac-937A206F2EE63C01",
            Mlb = "00000000000000000",
            OsType = "latest",
            CpuProfile = "Cascadelake-Server",
            SmbiosModel = "MacPro7,1",
            RecommendedRamGb = 8,
            RecommendedDiskGb = 80,
            BadgeText = "Novo • v26",
            AccentHex = "#00C7BE",
            SecondaryHex = "#0A84FF",
            RequiresAvx2 = true,
            IsFutureOrBeta = false,
            SubVersions = GetKnownSubVersionsForRelease("macos-26-tahoe")
        },
        new MacOSRelease
        {
            Id = "macos-15-sequoia",
            Name = "macOS Sequoia",
            Version = "15.6",
            Codename = "Sequoia",
            Subtitle = "Altamente estável no QEMU + WHPX • Ideal para desenvolvimento Xcode e uso diário",
            BoardId = "Mac-937A206F2EE63C01",
            Mlb = "00000000000000000",
            OsType = "default",
            CpuProfile = "Cascadelake-Server",
            SmbiosModel = "MacPro7,1",
            RecommendedRamGb = 8,
            RecommendedDiskGb = 80,
            BadgeText = "Recomendado • v15",
            AccentHex = "#30D158",
            SecondaryHex = "#0A84FF",
            RequiresAvx2 = true,
            SubVersions = GetKnownSubVersionsForRelease("macos-15-sequoia")
        },
        new MacOSRelease
        {
            Id = "macos-14-sonoma",
            Name = "macOS Sonoma",
            Version = "14.7",
            Codename = "Sonoma",
            Subtitle = "Excelente desempenho em Intel e AMD Ryzen no Windows via WHPX",
            BoardId = "Mac-827FAC58A8FDFA22",
            Mlb = "00000000000000000",
            OsType = "default",
            CpuProfile = "Haswell-noTSX",
            SmbiosModel = "iMacPro1,1",
            RecommendedRamGb = 8,
            RecommendedDiskGb = 64,
            BadgeText = "Estável • v14",
            AccentHex = "#FF9F0A",
            SecondaryHex = "#FF375F",
            RequiresAvx2 = true,
            SubVersions = GetKnownSubVersionsForRelease("macos-14-sonoma")
        },
        new MacOSRelease
        {
            Id = "macos-13-ventura",
            Name = "macOS Ventura",
            Version = "13.7",
            Codename = "Ventura",
            Subtitle = "Leve e rápido • Ótima compatibilidade com CPUs mais antigas",
            BoardId = "Mac-B4831CEBD52A0C4C",
            Mlb = "00000000000000000",
            OsType = "default",
            CpuProfile = "Haswell-noTSX",
            SmbiosModel = "iMacPro1,1",
            RecommendedRamGb = 6,
            RecommendedDiskGb = 64,
            BadgeText = "Leve • v13",
            AccentHex = "#FF6961",
            SecondaryHex = "#FF9F0A",
            RequiresAvx2 = true,
            SubVersions = GetKnownSubVersionsForRelease("macos-13-ventura")
        },
        new MacOSRelease
        {
            Id = "macos-12-monterey",
            Name = "macOS Monterey",
            Version = "12.7.6",
            Codename = "Monterey",
            Subtitle = "Não exige AVX2 • Máxima compatibilidade de hardware em qualquer PC Windows",
            BoardId = "Mac-FFE5EF870D7BA81A",
            Mlb = "00000000000000000",
            OsType = "default",
            CpuProfile = "Penryn",
            SmbiosModel = "iMacPro1,1",
            RecommendedRamGb = 4,
            RecommendedDiskGb = 60,
            BadgeText = "Clássico • v12",
            AccentHex = "#BF5AF2",
            SecondaryHex = "#5E5CE6",
            RequiresAvx2 = false,
            SubVersions = GetKnownSubVersionsForRelease("macos-12-monterey")
        },
        new MacOSRelease
        {
            Id = "macos-11-bigsur",
            Name = "macOS Big Sur",
            Version = "11.7.10",
            Codename = "Big Sur",
            Subtitle = "Baixo consumo de RAM e CPU • Executa perfeitamente mesmo sem AVX2",
            BoardId = "Mac-42FD25EABCABB274",
            Mlb = "00000000000000000",
            OsType = "default",
            CpuProfile = "Penryn",
            SmbiosModel = "iMacPro1,1",
            RecommendedRamGb = 4,
            RecommendedDiskGb = 50,
            BadgeText = "Legado • v11",
            AccentHex = "#64D2FF",
            SecondaryHex = "#0A84FF",
            RequiresAvx2 = false,
            SubVersions = GetKnownSubVersionsForRelease("macos-11-bigsur")
        }
    ];
}
