namespace InvestProMax.SharedUI.Common;

public static class DataHoraHelper
{
    private static readonly TimeZoneInfo FusoBrasilia = ObterFusoBrasilia();

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    public static string FormatarUltimaAtualizacaoCotacoes(DateTime? dataUtc)
    {
        if (dataUtc is null)
        {
            return "Sem atualização automática ainda";
        }

        var utc = DateTime.SpecifyKind(dataUtc.Value, DateTimeKind.Utc);
        var brasilia = TimeZoneInfo.ConvertTimeFromUtc(utc, FusoBrasilia);
        return $"Cotações atualizadas em {brasilia:dd/MM/yyyy HH:mm}";
    }

    public static string FormatarDataHoraBrasilia(DateTime? dataUtc)
    {
        if (dataUtc is null)
        {
            return "Não informada";
        }

        var utc = DateTime.SpecifyKind(dataUtc.Value, DateTimeKind.Utc);
        var brasilia = TimeZoneInfo.ConvertTimeFromUtc(utc, FusoBrasilia);
        return brasilia.ToString("dd/MM/yyyy HH:mm");
    }
}
