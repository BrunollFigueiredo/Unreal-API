# Receitas e inventario das quatro fases

Atualizacao preparada em 22/10/2026. A fase 2 usa **canudo + tesoura**, conforme confirmacao do autor; substitui a receita divergente PET + sino no GDD.

| Mapa atual | Item A | Item B | Resultado |
| --- | --- | --- | --- |
| Cena_Fase1 | Vidro | Pneu | Lupa de calor |
| Cena_Fase2 | Canudo | Tesoura | Bugiganga sonora |
| Cena_Fase3_Nova | Garrafa plastica | Lata de desodorante aerossol | Bugiganga congelante |
| Cena_Fase4_Nova | Pote plastico de medicamento | Po de toner | Bugiganga luminosa |

Cada receita consome uma unidade de A e uma de B. O cadastro descreve a receita; a fabricacao continua na logica Blueprint existente. Nao altera automaticamente os efeitos de combate, a quantidade de usos ou os nomes na interface.

## Banco e API

1. Publicar a API pelo GitHub/Railway. O SQL `deploy/003-receitas-bugigangas.sql` esta incorporado ao assembly e e aplicado automaticamente na inicializacao, antes de atender requisicoes. A conexao precisa ter permissao CREATE no banco existente. Se falhar, a API nao inicia e o motivo fica nos logs do Railway.
2. O SQL e repetivel e nao apaga progresso. Nao executar o schema original em banco existente. O script 002 pertence ao autosave adiado e nao e requisito desta atualizacao.
3. A ligacao com Unreal fica para a proxima etapa. A atualizacao nao instala coletaveis nem altera as Blueprints.

- `GET /api/progresso/{fase}/receita`: retorna nomes dos dois componentes, nome do resultado e quantidades necessarias.
- `GET /api/progresso/{fase}/inventario`: rota anterior, mantida.
- `POST /api/progresso/coletar`: rota anterior, mantida, com validacao de campos/contagens.
- `PUT /api/progresso/{fase}/inventario`: grava as contagens apos fabricar ou usar, sem inserir um ID de coleta ficticio. Corpo: `{"itemA":0,"itemB":0,"bugigangas":1}`.

Novo Jogo limpa inventarios e IDs coletados, preservando o cadastro de receitas.
O sistema anterior continua sendo um save compartilhado (jogador 1); esta alteracao nao introduz contas individuais.

## Contrato para futura ligacao em Blueprint

Usar exatamente o mesmo nome do mapa em todas as operacoes (Get Current Level Name com Remove Prefix String).
As fases 3 e 4 atualmente possuem o sufixo _Nova. Nao gravar Cena_Fase3 e carregar Cena_Fase3_Nova.

- No carregamento, Carregar Fase -> AoCarregarFase -> aplicar ItemA, ItemB, QTD Bugigangas; remover apenas os IDs coletados da fase atual.
- Na coleta, ItemA e ItemB representam os componentes da tabela acima. Executar CraftAutomatico antes de Registrar Coleta, enviando as contagens finais e o ID unico do objeto naquela fase.
- Na fabricacao separada ou uso durante o combate, integrar o PUT de inventario com as contagens finais e tratar a confirmacao ou falha HTTP. O no correspondente ainda nao foi instalado na Unreal.
- Nao enviar Registrar Coleta e Salvar Inventario Fase simultaneamente para a mesma alteracao: os endpoints recebem snapshots completos, e respostas fora de ordem podem sobrescrever estado recente. Se for permitir novas alteracoes durante um envio, adicionar fila de salvamento no Blueprint.
- O limite de duas utilizacoes por fase descrito pelo GDD precisa ser controlado pela logica de combate; nao confundir uma bugiganga fabricada com dois objetos fabricados.

## Estado da entrega

Fontes da API e SQL preparados para publicacao pelo GitHub. A API compilou com zero avisos e zero erros. O cadastro sera aplicado quando o Railway iniciar esta versao; o envio ao GitHub sozinho nao confirma a conclusao do deploy. Nao foram executados testes HTTP ou de jogo nesta alteracao. As Blueprints e os coletaveis continuam como estavam.
