namespace Toolbox.Data.Models;

public class SqlCmdRequest
{
    public string Server { get; init; } = ".";
    public string? Database { get; init; }
    public string ScriptPath { get; init; } = "";
    public IDictionary<string, string>? Variables { get; init; }

    public bool UseIntegratedSecurity { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }

    public bool AbortOnError { get; init; } = true;        
    public bool QuotedIdentifierOn { get; init; } = true;   
    public bool TrustServerCertificate { get; init; } = false; 
    public int? CodePage { get; init; } = 65001;            
    public int? LoginTimeoutSeconds { get; init; } = null;  
    public int? QueryTimeoutSeconds { get; init; } = null;  
    public string? AdditionalArgs { get; init; } = null;   

    public string SqlCmdPath { get; init; } = "sqlcmd";
}