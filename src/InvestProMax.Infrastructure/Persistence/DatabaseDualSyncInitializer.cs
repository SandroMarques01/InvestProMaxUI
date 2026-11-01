using System.Data;
using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Infrastructure.Persistence;

public static class DatabaseDualSyncInitializer
{
    public static bool SincronizacaoEmAndamento { get; private set; }

    public static async Task EnsureSupabaseSchemaAsync(
        SupabaseDbContext supabase,
        ILogger logger,
        CancellationToken ct = default)
    {
        var creator = supabase.GetService<IRelationalDatabaseCreator>();
        if (!await creator.HasTablesAsync(ct))
        {
            logger.LogInformation("Criando tabelas iniciais no banco PostgreSQL (Supabase)...");
            await creator.CreateTablesAsync(ct);
        }

        // Garante as tabelas e colunas adicionadas pelas migrations incrementais de forma idempotente no PostgreSQL:
        const string sqlMigracaoIdempotente = """
            CREATE TABLE IF NOT EXISTS "MovimentosAluguel" (
                "Id" uuid NOT NULL,
                "PapelId" uuid NOT NULL,
                "Tipo" character varying(20) NOT NULL,
                "Quantidade" numeric(18,6) NOT NULL,
                "Data" date NOT NULL,
                "Descricao" character varying(500) NULL,
                "OrigemImportacaoHash" character varying(64) NULL,
                CONSTRAINT "PK_MovimentosAluguel" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MovimentosAluguel_Papeis_PapelId" FOREIGN KEY ("PapelId") REFERENCES "Papeis" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_MovimentosAluguel_OrigemImportacaoHash" ON "MovimentosAluguel" ("OrigemImportacaoHash");
            CREATE INDEX IF NOT EXISTS "IX_MovimentosAluguel_PapelId_Data" ON "MovimentosAluguel" ("PapelId", "Data");

            ALTER TABLE "Papeis" ADD COLUMN IF NOT EXISTS "CotacaoAtualizadaEm" timestamp with time zone NULL;
            ALTER TABLE "Papeis" ADD COLUMN IF NOT EXISTS "FonteCotacao" character varying(30) NULL;

            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "TelegramId" bigint NULL;
            """;

        await supabase.Database.ExecuteSqlRawAsync(sqlMigracaoIdempotente, ct);
    }

    public static async Task SincronizarBasesAsync(
        InvestProMaxDbContext localDb,
        SupabaseDbContext supabaseDb,
        ILogger logger,
        CancellationToken ct = default)
    {
        SincronizacaoEmAndamento = true;
        try
        {
            logger.LogInformation("Iniciando sincronização de dados entre a base local (LocalDB) e o Supabase...");

        // 1. Usuarios
        var usuariosLocais = await localDb.Usuarios.AsNoTracking().ToListAsync(ct);
        var usuariosRemotos = await supabaseDb.Usuarios.ToDictionaryAsync(u => u.Id, ct);
        var usuariosRemotosPorUsername = usuariosRemotos.Values.ToDictionary(u => u.NomeUsuario, StringComparer.OrdinalIgnoreCase);
        var usuariosRemotosPorEmail = usuariosRemotos.Values.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);

        foreach (var u in usuariosLocais)
        {
            if (usuariosRemotos.TryGetValue(u.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(u);
            }
            else
            {
                // Se já existir no remoto um usuário com mesmo NomeUsuario ou Email mas Id diferente (ex.: criado por seed anterior sem dados),
                // remove se não tiver vínculos para manter o mesmo Id da base local.
                if (usuariosRemotosPorUsername.TryGetValue(u.NomeUsuario, out var conflitoUser) && conflitoUser.Id != u.Id)
                {
                    supabaseDb.Usuarios.Remove(conflitoUser);
                }
                else if (usuariosRemotosPorEmail.TryGetValue(u.Email, out var conflitoEmail) && conflitoEmail.Id != u.Id)
                {
                    supabaseDb.Usuarios.Remove(conflitoEmail);
                }

                await supabaseDb.Usuarios.AddAsync((Usuario)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(u)!, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        // 2. Segmentos
        var segmentosLocais = await localDb.Segmentos.AsNoTracking().ToListAsync(ct);
        var segmentosRemotos = await supabaseDb.Segmentos.ToDictionaryAsync(s => s.Id, ct);
        foreach (var s in segmentosLocais)
        {
            if (segmentosRemotos.TryGetValue(s.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(s);
            }
            else
            {
                await supabaseDb.Segmentos.AddAsync((Segmento)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(s)!, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        // 3. Papeis (com conciliação bidirecional de CotacaoAtual se o Supabase tiver uma cotação mais recente vinda do n8n)
        var papeisLocais = await localDb.Papeis.ToListAsync(ct);
        var papeisRemotos = await supabaseDb.Papeis.ToDictionaryAsync(p => p.Id, ct);
        var houveAtualizacaoCotacaoLocal = false;

        foreach (var p in papeisLocais)
        {
            var clone = (Papel)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(p)!;
            if (papeisRemotos.TryGetValue(p.Id, out var remoto))
            {
                var dataLocal = ReplicaSaveChangesInterceptor.NormalizarUtc(p.CotacaoAtualizadaEm);
                var dataRemota = ReplicaSaveChangesInterceptor.NormalizarUtc(remoto.CotacaoAtualizadaEm);

                // Se o Supabase tiver cotação mais recente (ou cotação preenchida quando local estava zerada), traz para o LocalDB
                if ((dataRemota.HasValue && (!dataLocal.HasValue || dataRemota.Value > dataLocal.Value) && remoto.CotacaoAtual > 0)
                    || (p.CotacaoAtual == 0 && remoto.CotacaoAtual > 0))
                {
                    p.CotacaoAtual = remoto.CotacaoAtual;
                    p.CotacaoAtualizadaEm = dataRemota ?? DateTime.UtcNow;
                    p.FonteCotacao = remoto.FonteCotacao ?? p.FonteCotacao;
                    clone.CotacaoAtual = p.CotacaoAtual;
                    clone.CotacaoAtualizadaEm = ReplicaSaveChangesInterceptor.NormalizarUtc(p.CotacaoAtualizadaEm);
                    clone.FonteCotacao = p.FonteCotacao;
                    houveAtualizacaoCotacaoLocal = true;
                }

                supabaseDb.Entry(remoto).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Papeis.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        if (houveAtualizacaoCotacaoLocal)
        {
            await localDb.SaveChangesAsync(ct);
        }

        // 4. PapelSegmentos
        var psLocais = await localDb.PapelSegmentos.AsNoTracking().ToListAsync(ct);
        var psRemotos = await supabaseDb.PapelSegmentos.AsNoTracking()
            .Select(ps => new { ps.PapelId, ps.SegmentoId })
            .ToListAsync(ct);
        var setPsRemotos = psRemotos.Select(x => (x.PapelId, x.SegmentoId)).ToHashSet();

        foreach (var ps in psLocais)
        {
            if (!setPsRemotos.Contains((ps.PapelId, ps.SegmentoId)))
            {
                await supabaseDb.PapelSegmentos.AddAsync(new PapelSegmento
                {
                    PapelId = ps.PapelId,
                    SegmentoId = ps.SegmentoId
                }, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        // 5. Transacoes
        var transacoesLocais = await localDb.Transacoes.AsNoTracking().ToListAsync(ct);
        var transacoesRemotas = await supabaseDb.Transacoes.ToDictionaryAsync(t => t.Id, ct);
        foreach (var t in transacoesLocais)
        {
            var clone = (Transacao)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(t)!;
            if (transacoesRemotas.TryGetValue(t.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Transacoes.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsTransacoesLocais = transacoesLocais.Select(t => t.Id).ToHashSet();
        var novosTransacoesLocal = false;
        foreach (var (id, r) in transacoesRemotas)
        {
            if (!idsTransacoesLocais.Contains(id))
            {
                await localDb.Transacoes.AddAsync((Transacao)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosTransacoesLocal = true;
            }
        }
        if (novosTransacoesLocal) await localDb.SaveChangesAsync(ct);

        // 6. Dividendos
        var dividendosLocais = await localDb.Dividendos.AsNoTracking().ToListAsync(ct);
        var dividendosRemotos = await supabaseDb.Dividendos.ToDictionaryAsync(d => d.Id, ct);
        foreach (var d in dividendosLocais)
        {
            var clone = (Dividendo)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(d)!;
            if (dividendosRemotos.TryGetValue(d.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Dividendos.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsDivLocais = dividendosLocais.Select(d => d.Id).ToHashSet();
        var novosDivLocal = false;
        foreach (var (id, r) in dividendosRemotos)
        {
            if (!idsDivLocais.Contains(id))
            {
                await localDb.Dividendos.AddAsync((Dividendo)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosDivLocal = true;
            }
        }
        if (novosDivLocal) await localDb.SaveChangesAsync(ct);

        // 7. MovimentosAluguel
        var alugueisLocais = await localDb.MovimentosAluguel.AsNoTracking().ToListAsync(ct);
        var alugueisRemotos = await supabaseDb.MovimentosAluguel.ToDictionaryAsync(m => m.Id, ct);
        foreach (var m in alugueisLocais)
        {
            var clone = (MovimentoAluguel)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(m)!;
            if (alugueisRemotos.TryGetValue(m.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.MovimentosAluguel.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsAlugueisLocais = alugueisLocais.Select(m => m.Id).ToHashSet();
        var novosAlugueisLocal = false;
        foreach (var (id, r) in alugueisRemotos)
        {
            if (!idsAlugueisLocais.Contains(id))
            {
                await localDb.MovimentosAluguel.AddAsync((MovimentoAluguel)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosAlugueisLocal = true;
            }
        }
        if (novosAlugueisLocal) await localDb.SaveChangesAsync(ct);

        // 8. Ativos
        var ativosLocais = await localDb.Ativos.AsNoTracking().ToListAsync(ct);
        var ativosRemotos = await supabaseDb.Ativos.ToDictionaryAsync(a => a.Id, ct);
        foreach (var a in ativosLocais)
        {
            var clone = (Ativo)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(a)!;
            if (ativosRemotos.TryGetValue(a.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Ativos.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsAtivosLocais = ativosLocais.Select(a => a.Id).ToHashSet();
        var novosAtivosLocal = false;
        foreach (var (id, r) in ativosRemotos)
        {
            if (!idsAtivosLocais.Contains(id))
            {
                await localDb.Ativos.AddAsync((Ativo)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosAtivosLocal = true;
            }
        }
        if (novosAtivosLocal) await localDb.SaveChangesAsync(ct);

        // 9. Inventarios
        var inventariosLocais = await localDb.Inventarios.AsNoTracking().ToListAsync(ct);
        var inventariosRemotos = await supabaseDb.Inventarios.ToDictionaryAsync(i => i.Id, ct);
        foreach (var i in inventariosLocais)
        {
            var clone = (Inventario)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(i)!;
            if (inventariosRemotos.TryGetValue(i.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Inventarios.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsInvLocais = inventariosLocais.Select(i => i.Id).ToHashSet();
        var novosInvLocal = false;
        foreach (var (id, r) in inventariosRemotos)
        {
            if (!idsInvLocais.Contains(id))
            {
                await localDb.Inventarios.AddAsync((Inventario)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosInvLocal = true;
            }
        }
        if (novosInvLocal) await localDb.SaveChangesAsync(ct);

        // 10. Contas
        var contasLocais = await localDb.Contas.AsNoTracking().ToListAsync(ct);
        var contasRemotas = await supabaseDb.Contas.ToDictionaryAsync(c => c.Id, ct);
        foreach (var c in contasLocais)
        {
            var clone = (Conta)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(c)!;
            if (contasRemotas.TryGetValue(c.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Contas.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        var idsContasLocais = contasLocais.Select(c => c.Id).ToHashSet();
        var novosContasLocal = false;
        foreach (var (id, r) in contasRemotas)
        {
            if (!idsContasLocais.Contains(id))
            {
                await localDb.Contas.AddAsync((Conta)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(r)!, ct);
                novosContasLocal = true;
            }
        }
        if (novosContasLocal) await localDb.SaveChangesAsync(ct);

        // 11. Faturas
        var faturasLocais = await localDb.Faturas.AsNoTracking().ToListAsync(ct);
        var faturasRemotas = await supabaseDb.Faturas.ToDictionaryAsync(f => f.Id, ct);
        foreach (var f in faturasLocais)
        {
            var clone = (Fatura)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(f)!;
            if (faturasRemotas.TryGetValue(f.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Faturas.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        // 12. Pagamentos
        var pagamentosLocais = await localDb.Pagamentos.AsNoTracking().ToListAsync(ct);
        var pagamentosRemotos = await supabaseDb.Pagamentos.ToDictionaryAsync(pg => pg.Id, ct);
        foreach (var pg in pagamentosLocais)
        {
            var clone = (Pagamento)ReplicaSaveChangesInterceptor.ClonarEntidadeSemNavegacao(pg)!;
            if (pagamentosRemotos.TryGetValue(pg.Id, out var existente))
            {
                supabaseDb.Entry(existente).CurrentValues.SetValues(clone);
            }
            else
            {
                await supabaseDb.Pagamentos.AddAsync(clone, ct);
            }
        }
        await supabaseDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "Sincronização concluída com sucesso: {Usuarios} usuários, {Papeis} papéis, {Transacoes} transações, {Dividendos} dividendos, {Alugueis} aluguéis, {Ativos} ativos, {Inventarios} inventários.",
            usuariosLocais.Count, papeisLocais.Count, transacoesLocais.Count, dividendosLocais.Count, alugueisLocais.Count, ativosLocais.Count, inventariosLocais.Count);
        }
        finally
        {
            SincronizacaoEmAndamento = false;
        }
    }
}

