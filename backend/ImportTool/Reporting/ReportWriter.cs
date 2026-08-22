using System.Text.Json;

namespace ImportTool.Reporting;

/// <summary>
/// docs/import-flow.md §6: rapor önce geçici dosyaya yazılır, flush edilir, sonra
/// atomik rename ile hedefe taşınır. Hedef zaten varsa üzerine yazılmaz.
/// </summary>
public static class ReportWriter
{
    // docs/import-flow.md §6 rapor şeması, boş alanları `null` olarak GÖSTERİR (anahtarı
    // atlamaz) — tüketici kodun "anahtar var mı" yerine "değeri null mı" kontrolü
    // yapabilmesi için WriteIndented dışında bir ignore condition YOK, bilerek.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Başarılıysa true ve gerçek hedef yolu, yazılamadıysa false döner.</summary>
    public static bool TryWrite(ImportReport report, string targetPath, out string writtenPath)
    {
        writtenPath = targetPath;
        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath))!;
            Directory.CreateDirectory(directory);

            tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
            var json = JsonSerializer.Serialize(report, JsonOptions);

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(targetPath))
            {
                // Aynı isim zaten var: run_id benzersiz olduğu için pratikte olmamalı,
                // ama olursa üzerine YAZMA — yeni bir ad üret.
                targetPath = Path.Combine(directory,
                    $"{Path.GetFileNameWithoutExtension(targetPath)}-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}");
            }

            File.Move(tempPath, targetPath);
            writtenPath = targetPath;
            tempPath = null;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    public static string DefaultPath(string? explicitPath, Guid runId)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        return $"import-report-{timestamp}-{runId:N}.json";
    }
}
