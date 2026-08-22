using Microsoft.EntityFrameworkCore;
using Npgsql;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Common;

public class UniqueConstraintViolationDetector : IUniqueConstraintViolationDetector
{
    public bool IsUniqueViolation(Exception ex, string constraintName)
    {
        // DbUpdateException envuelve la excepción real del driver; Npgsql expone el
        // nombre del constraint violado en PostgresException.ConstraintName cuando el
        // SqlState es 23505 (unique_violation).
        if (ex is DbUpdateException { InnerException: PostgresException pgEx })
        {
            return pgEx.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(pgEx.ConstraintName, constraintName, StringComparison.Ordinal);
        }
        return false;
    }
}
