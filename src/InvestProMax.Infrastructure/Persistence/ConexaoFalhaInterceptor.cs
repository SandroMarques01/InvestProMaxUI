using System;
using System.Data.Common;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Infrastructure.Persistence;

public sealed class ConexaoFalhaInterceptor : DbCommandInterceptor
{
    private readonly IDatabaseProviderService _dbProvider;
    private readonly ILogger<ConexaoFalhaInterceptor> _logger;

    public ConexaoFalhaInterceptor(
        IDatabaseProviderService dbProvider,
        ILogger<ConexaoFalhaInterceptor> logger)
    {
        _dbProvider = dbProvider;
        _logger = logger;
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TratarFalha(eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        TratarFalha(eventData);
        base.CommandFailed(command, eventData);
    }

    private void TratarFalha(CommandErrorEventData eventData)
    {
        if (eventData.Context is SupabaseDbContext && EhErroDeConectividade(eventData.Exception))
        {
            _logger.LogWarning(eventData.Exception, "Falha de conectividade detectada no Supabase. Alternando fallback para LocalDB (localhost).");
            _dbProvider.MarcarSupabaseInacessivel(eventData.Exception);
        }
    }

    public static bool EhErroDeConectividade(Exception? ex)
    {
        if (ex is null) return false;

        if (ex is SocketException or TimeoutException or IOException)
            return true;

        if (ex is Npgsql.NpgsqlException npgEx && (npgEx.IsTransient || npgEx.InnerException is SocketException or TimeoutException or IOException))
            return true;

        var msg = ex.Message.ToLowerInvariant();
        return msg.Contains("connection refused")
            || msg.Contains("network is unreachable")
            || msg.Contains("host is unreachable")
            || msg.Contains("connection reset")
            || msg.Contains("operation timed out")
            || msg.Contains("timeout")
            || msg.Contains("server is not listening");
    }
}

