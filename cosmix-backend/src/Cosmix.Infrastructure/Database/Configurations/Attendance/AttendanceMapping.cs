using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

/// <summary>Shared storage conventions for these attendance records, not a persistence/repository abstraction.</summary>
internal static class AttendanceMapping
{
    public static void Record<T>(EntityTypeBuilder<T> builder, string tableName, params (string Name, string Sql)[] checks) where T : class
    {
        builder.HasKey("Id"); builder.Property<Guid>("Id").ValueGeneratedNever();
        var instants = typeof(T).GetProperties().Where(x => x.PropertyType == typeof(DateTimeOffset) || x.PropertyType == typeof(DateTimeOffset?)).ToArray();
        builder.ToTable(tableName, "attendance", table =>
        {
            foreach (var (name, sql) in checks) table.HasCheckConstraint($"CK_{tableName}_{name}", sql);
            foreach (var instant in instants)
                table.HasCheckConstraint($"CK_{tableName}_{instant.Name}_Utc", $"[{instant.Name}] IS NULL OR DATEPART(TZOFFSET, [{instant.Name}]) = 0");
        });
        foreach (var instant in instants) builder.Property(instant.Name).HasColumnType("datetimeoffset(7)");
        foreach (var property in typeof(T).GetProperties().Where(x => x.PropertyType == typeof(DateOnly) || x.PropertyType == typeof(DateOnly?)))
            builder.Property(property.Name).HasColumnType("date");
        if (typeof(T).GetProperty("RowVersion") is not null) builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
    public static void Strings<T>(EntityTypeBuilder<T> builder, params (string Name, int Length)[] properties) where T : class
    {
        foreach (var (name, length) in properties) builder.Property<string>(name).HasMaxLength(length);
    }
    public static void Coordinates<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property("Latitude").HasPrecision(10, 7); builder.Property("Longitude").HasPrecision(10, 7);
        if (typeof(T).GetProperty("AccuracyMeters") is not null) builder.Property("AccuracyMeters").HasPrecision(10, 2);
    }
    public const string CoordinatesCheck = "(([Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0)))";
}
