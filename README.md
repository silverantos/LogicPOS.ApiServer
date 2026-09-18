# LogicPOS.ApiServer

## Compatibilidade com base LogicPOS existente

Esta API foi ajustada para trabalhar diretamente com uma base SQLite LogicPOS já existente (`Data Source=logicpos.db`) sem criar tabelas paralelas `Api*`.

### Alterações principais

- O `ApplicationDbContext` passou a mapear as entidades para tabelas reais do schema LogicPOS (ex.: `Articles`, `Orders`, `Documents`, `DocumentDetails`, `PaymentMethods`, `WorkSessionPeriods`, etc.).
- Foram adicionados aliases de colunas para compatibilidade com nomes reais (ex.: `Button_Image`, `Price1_Value`, `Customer_Name`, `Tax_Percentage`, `CreatedAt`, `UpdatedAt`).
- Foi aplicado filtro global de soft-delete (`IsDeleted = 0`) e conversão automática de `Delete` EF para soft-delete em entidades com `IsDeleted`.
- O `DatabaseInitializer` deteta schema LogicPOS existente e **não** executa migrations nesse caso (evitando criação de tabelas `Api*`).
- O `ApplicationDbContextFactory` usa o mesmo fallback de runtime: `Data Source=logicpos.db`.

### Arranque

1. Garantir que o ficheiro SQLite existente está disponível como `logicpos.db`.
2. Configurar `ConnectionStrings:DefaultConnection` (se necessário) para esse ficheiro.
3. Configurar `Jwt__SigningKey` e `Cors:AllowedOrigins`.
4. Iniciar a API normalmente.

### Limitações conhecidas

- A API mantém os contratos HTTP atuais e aliases de rota, mas continua dependente dos campos suportados pelo modelo atual.
- Módulos sem equivalência direta no modelo atual podem exigir evolução adicional para cobertura funcional total do schema LogicPOS.
