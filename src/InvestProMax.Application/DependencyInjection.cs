using FluentValidation;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Cotacoes;
using InvestProMax.Application.Dashboard;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Faturas;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using Microsoft.Extensions.DependencyInjection;

namespace InvestProMax.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPapelService, PapelService>();
        services.AddScoped<ITransacaoService, TransacaoService>();
        services.AddScoped<IDividendoService, DividendoService>();
        services.AddScoped<ISegmentoService, SegmentoService>();
        services.AddScoped<IImportacaoB3Service, ImportacaoB3Service>();
        services.AddScoped<IImportacaoCotacoesService, ImportacaoCotacoesService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IConfiguracaoService, ConfiguracaoService>();
        services.AddScoped<IAtivoService, AtivoService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IContaService, ContaService>();
        services.AddScoped<IFaturaService, FaturaService>();
        services.AddScoped<IFaturaGeracaoJobService, FaturaGeracaoJobService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAtualizacaoCotacoesService, AtualizacaoCotacoesService>();

        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));

        return services;
    }
}
