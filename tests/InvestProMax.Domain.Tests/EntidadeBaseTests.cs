using FluentAssertions;
using InvestProMax.Domain.Entidades;
using Xunit;

namespace InvestProMax.Domain.Tests;

/// <summary>
/// Testes reais das regras de negócio (preço médio, desdobramento, geração de fatura, isolamento
/// por usuário — seção 11) chegam na Fase 2/5, junto com a camada Application onde essas regras
/// vivem (seção 2). Este arquivo cobre só o que já existe nesta fase: as entidades em si.
/// </summary>
public class EntidadeBaseTests
{
    [Fact]
    public void Cada_entidade_recebe_um_Id_unico_na_criacao()
    {
        var usuario1 = new Usuario();
        var usuario2 = new Usuario();

        usuario1.Id.Should().NotBe(Guid.Empty);
        usuario2.Id.Should().NotBe(Guid.Empty);
        usuario1.Id.Should().NotBe(usuario2.Id);
    }
}
