-- Acrescenta estado da luta, sem modificar vidas/progresso ja existentes.
CREATE TABLE IF NOT EXISTS sessao_save (
    jogador_id INT NOT NULL PRIMARY KEY,
    sessao VARCHAR(36) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT IGNORE INTO sessao_save (jogador_id, sessao) VALUES (1, UUID());

CREATE TABLE IF NOT EXISTS combate_fase (
    jogador_id INT NOT NULL,
    fase VARCHAR(50) NOT NULL,
    sessao VARCHAR(36) NOT NULL,
    revisao BIGINT NOT NULL,
    estado JSON NOT NULL,
    atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (jogador_id, fase)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
