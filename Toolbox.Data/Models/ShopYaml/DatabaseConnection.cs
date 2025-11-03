using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Toolbox.Data.Models.ShopYaml;

public class DatabaseConnection : IEquatable<DatabaseConnection>
{
    [Key]
    [YamlIgnore]
    public int Id { get; set; }
    
    [YamlMember(Alias = "server")]
    public string Server { get; set; }
    
    [YamlMember(Alias = "database")]
    public string Database { get; set; }
    
    [YamlMember(Alias = "user")]
    public string User { get; set; } 
    
    [YamlMember(Alias = "password")]
    public string Password { get; set; } 
    
    [YamlMember(Alias = "maxPoolSize")]
    public int MaxPoolSize { get; set; }
    
    [YamlMember(Alias = "encrypt")]
    public bool Encrypt { get; set; }
    
    [YamlMember(Alias = "trustServerCertificate")]
    public bool TrustServerCertificate { get; set; }

    public DatabaseConnection()
    {
        Server = "(localdb)\\MSSQLLocalDB";
        Database = "Shopsystem";
        User = "shopsystem";
        Password = "Test123!";
        Encrypt = true;
        MaxPoolSize = 1000;
        TrustServerCertificate = true;
    }

    public bool Equals(DatabaseConnection? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        var sc = StringComparer.OrdinalIgnoreCase;
        return sc.Equals(Server ?? string.Empty, other.Server ?? string.Empty)
               && sc.Equals(Database ?? string.Empty, other.Database ?? string.Empty)
               && sc.Equals(User ?? string.Empty, other.User ?? string.Empty)
               && string.Equals(Password ?? string.Empty, other.Password ?? string.Empty, StringComparison.Ordinal)
               && MaxPoolSize == other.MaxPoolSize
               && Encrypt == other.Encrypt
               && TrustServerCertificate == other.TrustServerCertificate;
    }

    public override bool Equals(object? obj) => obj is DatabaseConnection other && Equals(other);

    public override int GetHashCode()
    {
        var sc = StringComparer.OrdinalIgnoreCase;
        var hc = new HashCode();
        hc.Add(Server ?? string.Empty, sc);
        hc.Add(Database ?? string.Empty, sc);
        hc.Add(User ?? string.Empty, sc);
        hc.Add(Password ?? string.Empty, StringComparer.Ordinal);
        hc.Add(MaxPoolSize);
        hc.Add(Encrypt);
        hc.Add(TrustServerCertificate);
        return hc.ToHashCode();
    }

    public static bool operator ==(DatabaseConnection? left, DatabaseConnection? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(DatabaseConnection? left, DatabaseConnection? right)
        => !(left == right);

    public override string ToString()
    {
        return $"{Server} / {Database} / {User} / {(Encrypt ? "Encrypted" : "Decrypted")}";
    }
    
    public DatabaseConnection(DatabaseConnection other)
    {
        Id = other.Id;
        Server = other.Server;
        Database = other.Database;
        User = other.User;
        Password = other.Password;
        MaxPoolSize = other.MaxPoolSize;
        Encrypt = other.Encrypt;
        TrustServerCertificate = other.TrustServerCertificate;
    }

    public DatabaseConnection DeepClone() => new DatabaseConnection(this);

    public string GetConnectionString()
    {
        return $"Server={Server};Database={Database};User Id={User};Password={Password};Encrypt={Encrypt};TrustServerCertificate={TrustServerCertificate};";
    }
}

