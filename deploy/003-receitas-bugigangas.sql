-- Aplicado automaticamente na inicializacao da API; pode ser executado manualmente.
-- Cadastro de receitas; nao altera inventarios nem objetos ja coletados.
CREATE TABLE IF NOT EXISTS receitas_bugigangas (
    fase VARCHAR(50) NOT NULL PRIMARY KEY,
    item_a_nome VARCHAR(100) NOT NULL,
    item_b_nome VARCHAR(100) NOT NULL,
    bugiganga_nome VARCHAR(100) NOT NULL,
    quantidade_a INT NOT NULL DEFAULT 1,
    quantidade_b INT NOT NULL DEFAULT 1
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO receitas_bugigangas
    (fase, item_a_nome, item_b_nome, bugiganga_nome, quantidade_a, quantidade_b)
VALUES
    ('Cena_Fase1', 'Vidro', 'Pneu', 'Lupa de calor', 1, 1),
    ('Cena_Fase2', 'Canudo', 'Tesoura', 'Bugiganga sonora', 1, 1),
    ('Cena_Fase3_Nova', 'Garrafa plástica', 'Lata de desodorante aerossol', 'Bugiganga congelante', 1, 1),
    ('Cena_Fase4_Nova', 'Pote plástico de medicamento', 'Pó de toner', 'Bugiganga luminosa', 1, 1)
ON DUPLICATE KEY UPDATE
    item_a_nome = VALUES(item_a_nome),
    item_b_nome = VALUES(item_b_nome),
    bugiganga_nome = VALUES(bugiganga_nome),
    quantidade_a = VALUES(quantidade_a),
    quantidade_b = VALUES(quantidade_b);
