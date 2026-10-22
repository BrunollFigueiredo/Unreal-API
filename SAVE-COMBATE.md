# Salvamento da vida durante a luta

Atualizacao de 22/10/2026. A migration 004 e incorporada ao assembly e aplicada na inicializacao da API, como a migration 003. Nao requer executar o script 002 do autosave adiado.

## Dados e rotas

- `sessao_save`: identificacao da partida compartilhada atual (jogador 1).
- `combate_fase`: um estado por fase e jogador, contendo vida atual/maxima dos dois personagens, posicoes, rotacoes, identificacao do chefe e indicador de furia.
- `GET /api/progresso/{fase}/combate`: sessao atual e estado da fase, ou estado nulo.
- `PUT /api/progresso/{fase}/combate`: recebe um snapshot completo. Rejeita valores invalidos e sessao antiga (409); revisoes antigas nao sobrescrevem as mais recentes.
- `GET /api/progresso/continuar-combate`: ultima fase com registro na sessao atual.
- `POST /api/progresso/novo-jogo`: alem das limpezas anteriores, troca a sessao e limpa os registros de luta na mesma transacao.

## Unreal

`BP_SalvamentoLuta` le/aplica os componentes `AC_HealthSystem`, com `Max Health` antes de `Current Health`. A captura e acionada depois da alteracao da vida, por timer de 0,25 segundo e ao encerrar a fase. Sem Event Tick. O C++ em `CombateHttpActor` fornece HTTP, serializacao e copia local; a captura/restauracao de propriedades fica nos nos Blueprint.

A copia local e gravada em `Saved/Combate`. Escrita temporaria seguida de substituicao preserva o ultimo arquivo completo. O envio HTTP e serializado, com no maximo um novo pedido por segundo e novas tentativas apos falha. Fechar o jogo nao depende de aguardar um pedido HTTP terminar: Continuar pode usar a copia local mais recente da mesma partida. Sem nenhuma copia local e sem resposta da API, o salvamento fica bloqueado para preservar os dados anteriores.

Na retomada, o personagem volta a posicao salva e o chefe recebe sua vida salva. Nao e um replay do frame da luta: ataques, projeteis e montagens em andamento nao sao serializados. Um registro com vida do jogador zero inicia uma nova tentativa com os valores iniciais, sem restaurar o personagem morto. Novo Jogo limpa tambem a copia local apos confirmacao do servidor.

O sistema mantem o save compartilhado do projeto. Nao cria contas individuais. Se o banco for reiniciado por outro dispositivo, uma copia local offline nao consegue saber disso ate voltar a consultar o servidor.

## Publicacao

Enviar estes arquivos ao GitHub aciona o deploy se a integracao automatica estiver habilitada. Os logs do Railway devem confirmar a migration 004 e a inicializacao. O push nao confirma que o deploy terminou. A API e o modulo Unreal devem ser compilados; a nova build do jogo precisa incluir os assets Blueprint modificados. Nao foram executados testes de gameplay nesta alteracao.
