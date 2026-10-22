using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration
    .GetConnectionString("BancoJogo");

if (conexao == null)
{
    throw new Exception("Não achei a conexão BancoJogo no appsettings.json");
}

var app = builder.Build();

// Aplicacao idempotente antes de aceitar requisicoes. O SQL e incorporado ao
// assembly para funcionar tambem na imagem Docker publicada pelo Railway.
await using (var recurso = typeof(Progresso).Assembly.GetManifestResourceStream(
    "Migrations.003-receitas-bugigangas.sql")
    ?? throw new InvalidOperationException("Migration de receitas ausente no assembly."))
using (var leitorSql = new StreamReader(recurso))
await using (var bancoReceitas = new MySqlConnection(conexao))
{
    var sqlReceitas = await leitorSql.ReadToEndAsync();
    await bancoReceitas.OpenAsync();
    await using var comandoReceitas = new MySqlCommand(sqlReceitas, bancoReceitas);
    await comandoReceitas.ExecuteNonQueryAsync();
    app.Logger.LogInformation("Cadastro das receitas das quatro fases atualizado.");
}

// GET /api/progresso
// Carrega fase, checkpoint, vida e itens do save principal.
app.MapGet("/api/progresso", async () =>
{
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();

    const string sql = """
        SELECT jogador_id, fase, checkpoint, vida, itens
        FROM progresso
        WHERE jogador_id = 1
        """;

    await using var comando = new MySqlCommand(sql, banco);
    await using var leitor = await comando.ExecuteReaderAsync();

    if (!await leitor.ReadAsync())
    {
        return Results.NotFound("Ainda não há progresso salvo.");
    }

    var progresso = new
    {
        jogadorId = leitor.GetInt32("jogador_id"),
        fase = leitor.GetString("fase"),
        checkpoint = leitor.IsDBNull(leitor.GetOrdinal("checkpoint"))
            ? null
            : leitor.GetString("checkpoint"),
        vida = leitor.GetInt32("vida"),
        itens = leitor.IsDBNull(leitor.GetOrdinal("itens"))
            ? ""
            : leitor.GetString("itens")
    };

    return Results.Ok(progresso);
});

// PUT /api/progresso
// Cria ou atualiza o save principal.
app.MapPut("/api/progresso", async (Progresso dados) =>
{
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();

    const string sql = """
        INSERT INTO progresso (jogador_id, fase, checkpoint, vida, itens)
        VALUES (1, @fase, @checkpoint, @vida, @itens)
        ON DUPLICATE KEY UPDATE
            fase = @fase,
            checkpoint = @checkpoint,
            vida = @vida,
            itens = @itens
        """;

    await using var comando = new MySqlCommand(sql, banco);

    comando.Parameters.AddWithValue("@fase", dados.Fase);
    comando.Parameters.AddWithValue(
        "@checkpoint",
        (object?)dados.Checkpoint ?? DBNull.Value
    );
    comando.Parameters.AddWithValue("@vida", dados.Vida);
    comando.Parameters.AddWithValue("@itens", dados.Itens);

    await comando.ExecuteNonQueryAsync();

    return Results.Ok("Progresso salvo.");
});

// POST /api/progresso/coletar
// Salva as quantidades de itens e marca um objeto do cenário como coletado.
app.MapPost("/api/progresso/coletar", async (ColetaItem dados) =>
{
    if (string.IsNullOrWhiteSpace(dados.Fase) || dados.Fase.Length > 50
        || string.IsNullOrWhiteSpace(dados.IdItem) || dados.IdItem.Length > 100
        || dados.ItemA < 0 || dados.ItemB < 0 || dados.Bugigangas < 0)
        return Results.BadRequest("Informe fase, ID do objeto e quantidades não negativas.");

    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();

    await using var transacao = await banco.BeginTransactionAsync();

    const string sqlInventario = """
        INSERT INTO inventario_fase (fase, item_a, item_b, bugigangas)
        VALUES (@fase, @itemA, @itemB, @bugigangas)
        ON DUPLICATE KEY UPDATE
            item_a = @itemA,
            item_b = @itemB,
            bugigangas = @bugigangas
        """;

    await using var comandoInventario =
        new MySqlCommand(sqlInventario, banco, transacao);

    comandoInventario.Parameters.AddWithValue("@fase", dados.Fase);
    comandoInventario.Parameters.AddWithValue("@itemA", dados.ItemA);
    comandoInventario.Parameters.AddWithValue("@itemB", dados.ItemB);
    comandoInventario.Parameters.AddWithValue(
        "@bugigangas",
        dados.Bugigangas
    );

    await comandoInventario.ExecuteNonQueryAsync();

    const string sqlColetado = """
        INSERT INTO itens_coletados (fase, id_item, coletado)
        VALUES (@fase, @idItem, TRUE)
        ON DUPLICATE KEY UPDATE coletado = TRUE
        """;

    await using var comandoColetado =
        new MySqlCommand(sqlColetado, banco, transacao);

    comandoColetado.Parameters.AddWithValue("@fase", dados.Fase);
    comandoColetado.Parameters.AddWithValue("@idItem", dados.IdItem);

    await comandoColetado.ExecuteNonQueryAsync();

    await transacao.CommitAsync();

    return Results.Ok("Coleta e inventário salvos.");
});

// GET /api/progresso/{fase}/inventario
// Consulta as quantidades de itens de uma fase.
app.MapGet("/api/progresso/{fase}/inventario", async (string fase) =>
{
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();

    const string sql = """
        SELECT item_a, item_b, bugigangas
        FROM inventario_fase
        WHERE fase = @fase
        """;

    await using var comando = new MySqlCommand(sql, banco);
    comando.Parameters.AddWithValue("@fase", fase);

    await using var leitor = await comando.ExecuteReaderAsync();

    if (!await leitor.ReadAsync())
    {
        return Results.Ok(new
        {
            fase,
            itemA = 0,
            itemB = 0,
            bugigangas = 0
        });
    }

    return Results.Ok(new
    {
        fase,
        itemA = leitor.GetInt32("item_a"),
        itemB = leitor.GetInt32("item_b"),
        bugigangas = leitor.GetInt32("bugigangas")
    });
});

// GET /api/progresso/{fase}/coletados
// Consulta os IDs dos objetos coletados em uma fase.
app.MapGet("/api/progresso/{fase}/coletados", async (string fase) =>
{
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();

    const string sql = """
        SELECT id_item
        FROM itens_coletados
        WHERE fase = @fase AND coletado = TRUE
        """;

    await using var comando = new MySqlCommand(sql, banco);
    comando.Parameters.AddWithValue("@fase", fase);

    await using var leitor = await comando.ExecuteReaderAsync();

    var idsColetados = new List<string>();

    while (await leitor.ReadAsync())
    {
        idsColetados.Add(leitor.GetString("id_item"));
    }

    return Results.Ok(idsColetados);
});

// POST /api/progresso/novo-jogo
// Reinicia o save do jogador 1 e os itens de todas as fases.
app.MapPost("/api/progresso/novo-jogo", async (NovoJogo dados) =>
{
    if (string.IsNullOrWhiteSpace(dados.FaseInicial) || dados.VidaInicial <= 0)
    {
        return Results.BadRequest("Informe a fase inicial e uma vida inicial maior que zero.");
    }

    var faseInicial = dados.FaseInicial.Trim();
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();
    await using var transacao = await banco.BeginTransactionAsync();

    await using var limparColetados = new MySqlCommand(
        "DELETE FROM itens_coletados", banco, transacao);
    await limparColetados.ExecuteNonQueryAsync();

    await using var limparInventarios = new MySqlCommand(
        "DELETE FROM inventario_fase", banco, transacao);
    await limparInventarios.ExecuteNonQueryAsync();

    const string sqlProgresso = """
        INSERT INTO progresso (jogador_id, fase, checkpoint, vida, itens)
        VALUES (1, @fase, NULL, @vida, '')
        ON DUPLICATE KEY UPDATE
            fase = @fase,
            checkpoint = NULL,
            vida = @vida,
            itens = ''
        """;

    await using var reiniciarProgresso = new MySqlCommand(sqlProgresso, banco, transacao);
    reiniciarProgresso.Parameters.AddWithValue("@fase", faseInicial);
    reiniciarProgresso.Parameters.AddWithValue("@vida", dados.VidaInicial);
    await reiniciarProgresso.ExecuteNonQueryAsync();

    // A limpeza inteira so e confirmada se todos os comandos derem certo.
    await transacao.CommitAsync();

    return Results.Ok(new
    {
        mensagem = "Novo jogo iniciado.",
        faseInicial,
        vidaInicial = dados.VidaInicial
    });
});

// O catalogo nao guarda progresso: Novo Jogo preserva estas receitas.
app.MapGet("/api/progresso/{fase}/receita", async (string fase) =>
{
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();
    await using var comando = new MySqlCommand("""
        SELECT item_a_nome, item_b_nome, bugiganga_nome, quantidade_a, quantidade_b
        FROM receitas_bugigangas WHERE fase = @fase
        """, banco);
    comando.Parameters.AddWithValue("@fase", fase.Trim());
    await using var leitor = await comando.ExecuteReaderAsync();
    if (!await leitor.ReadAsync()) return Results.NotFound("Receita não cadastrada para esta fase.");
    return Results.Ok(new {
        fase = fase.Trim(),
        itemANome = leitor.GetString("item_a_nome"),
        itemBNome = leitor.GetString("item_b_nome"),
        bugigangaNome = leitor.GetString("bugiganga_nome"),
        quantidadeA = leitor.GetInt32("quantidade_a"),
        quantidadeB = leitor.GetInt32("quantidade_b")
    });
});

// Grava o resultado da fabricacao/uso feito em Blueprint sem inventar uma coleta.
// Mantem o contrato itemA/itemB/bugigangas usado pela fase 1.
app.MapPut("/api/progresso/{fase}/inventario", async (string fase, InventarioFase dados) =>
{
    if (string.IsNullOrWhiteSpace(fase) || fase.Length > 50
        || dados.ItemA < 0 || dados.ItemB < 0 || dados.Bugigangas < 0)
        return Results.BadRequest("Informe uma fase e quantidades não negativas.");
    await using var banco = new MySqlConnection(conexao);
    await banco.OpenAsync();
    await using var comando = new MySqlCommand("""
        INSERT INTO inventario_fase (fase, item_a, item_b, bugigangas)
        VALUES (@fase, @a, @b, @qtd)
        ON DUPLICATE KEY UPDATE item_a = @a, item_b = @b, bugigangas = @qtd
        """, banco);
    comando.Parameters.AddWithValue("@fase", fase.Trim());
    comando.Parameters.AddWithValue("@a", dados.ItemA);
    comando.Parameters.AddWithValue("@b", dados.ItemB);
    comando.Parameters.AddWithValue("@qtd", dados.Bugigangas);
    await comando.ExecuteNonQueryAsync();
    return Results.Ok(new { fase = fase.Trim(), dados.ItemA, dados.ItemB, dados.Bugigangas });
});

app.Run();

record InventarioFase(int ItemA, int ItemB, int Bugigangas);

record Progresso(
    string Fase,
    string? Checkpoint,
    int Vida,
    string Itens
);

record ColetaItem(
    string Fase,
    string IdItem,
    int ItemA,
    int ItemB,
    int Bugigangas
);

record NovoJogo(string FaseInicial, int VidaInicial);
