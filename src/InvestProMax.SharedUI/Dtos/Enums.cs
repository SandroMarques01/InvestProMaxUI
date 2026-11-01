namespace InvestProMax.SharedUI.Dtos;

// Cópias explícitas dos enums do Domain do back-end (mesmo padrão já usado no início da
// Fase 6 anterior — ver README): a SharedUI não pode referenciar InvestProMax.Domain (seção 3:
// "SharedUI... nunca referencia Infrastructure ou Domain diretamente"), e o Program.cs da Api
// não registra JsonStringEnumConverter, então os enums trafegam como número no JSON — os
// valores aqui têm que bater exatamente com src/InvestProMax.Domain/Enums.

public enum TipoPapel { Acao = 1, FII = 2, BDR = 3, ETF = 4 }

public enum TipoTransacao { Compra = 1, Venda = 2 }

public enum TipoDividendo { Dividendo = 1, JSCP = 2, Rendimento = 3, Outro = 4, Aluguel = 5 }

public enum TipoAtivo { RendaFixa = 1, RendaVariavel = 2 }

public enum TipoOrigemAtivo { Manual = 1, Calculado = 2 }

public enum Recorrencia { Mensal = 1, Anual = 2, Parcelada = 3, Unica = 4 }

public enum StatusFatura { Pendente = 1, Paga = 2, Atrasada = 3, Cancelada = 4 }

public enum TipoEventoPapel { Desdobramento = 1, Grupamento = 2 }
