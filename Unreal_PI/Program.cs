using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration
    .GetConnectionString("BancoJogo");

if (conexao == null)
{
    throw new Exception("Não achei a conexão BancoJogo no appsettings.json");
}

var app = builder.Build();

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

app.Run();

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
