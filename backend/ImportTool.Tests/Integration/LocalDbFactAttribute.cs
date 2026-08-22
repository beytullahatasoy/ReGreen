using Xunit;

namespace ImportTool.Tests.Integration;

/// <summary>
/// LocalDB testleri geçici bir SQL Server veritabanı oluşturduğu için açıkça opt-in ister.
/// PowerShell: $env:REGREEN_RUN_LOCALDB_TESTS='1'; dotnet test ...
/// </summary>
public sealed class LocalDbFactAttribute : FactAttribute
{
    public static bool Enabled => OperatingSystem.IsWindows()
                                  && Environment.GetEnvironmentVariable("REGREEN_RUN_LOCALDB_TESTS") == "1";

    public LocalDbFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "SQL Server LocalDB yalnızca Windows'ta desteklenir.";
        else if (Environment.GetEnvironmentVariable("REGREEN_RUN_LOCALDB_TESTS") != "1")
            Skip = "Çalıştırmak için REGREEN_RUN_LOCALDB_TESTS=1 ayarlayın; testler geçici bir LocalDB veritabanı oluşturur.";
    }
}
